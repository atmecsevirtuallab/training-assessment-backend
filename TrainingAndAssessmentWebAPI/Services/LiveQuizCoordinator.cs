using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using TrainingAndAssessmentWebAPI.Hubs;
using TrainingAndAssessmentWebAPI.Models;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class LiveQuizCoordinator : IDisposable
{
    private sealed class Participant(int studentId, string usn, string name)
    {
        public int StudentId { get; } = studentId;
        public string Usn { get; } = usn;
        public string Name { get; } = name;
        public int? SelectedOption { get; set; }
        public int CurrentScore { get; set; }
        public int CumulativeScore { get; set; }
    }

    private sealed class Run(string runId, string code, CreateLiveQuizDto request)
    {
        public string RunId { get; } = runId;
        public string Code { get; } = code;
        public CreateLiveQuizDto Request { get; } = request;
        public string Stage { get; set; } = "Ready";
        public string Status { get; set; } = "Waiting";
        public int CurrentQuestionIndex { get; set; }
        public int SecondsRemaining { get; set; }
        public ConcurrentDictionary<int, Participant> Participants { get; } = new();
        public CancellationTokenSource? TimerCancellation { get; set; }
        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Run> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHubContext<LiveQuizHub> _hub;
    private readonly string _connectionString;

    public LiveQuizCoordinator(IHubContext<LiveQuizHub> hub, IConfiguration configuration)
    {
        _hub = hub;
        _connectionString = configuration.GetConnectionString("TrainingAssessmentPortal")
            ?? throw new InvalidOperationException("ConnectionStrings:TrainingAssessmentPortal is not configured.");
    }

    public static string Group(string code) => $"live-quiz:{code.Trim().ToUpperInvariant()}";

    public async Task<LiveQuizSnapshot> CreateAsync(CreateLiveQuizDto request)
    {
        if (request.Questions.Count == 0) throw new InvalidOperationException("At least one quiz question is required.");
        string code;
        do code = Random.Shared.Next(100000, 1000000).ToString(); while (_runs.ContainsKey(code));
        var run = new Run(Guid.NewGuid().ToString("N"), code, request);
        if (!_runs.TryAdd(code, run)) throw new InvalidOperationException("Unable to create live quiz.");
        await EnsureTablesAsync();
        await ExecuteAsync(@"INSERT INTO LiveQuizRuns(RunId,QuizCode,SessionId,Batch,TrainerName,QuestionsJson,Stage,Status,CurrentQuestionIndex,SecondsRemaining,CreatedAt)
            VALUES(@runId,@code,@sessionId,@batch,@trainer,@questions,'Ready','Waiting',0,0,SYSUTCDATETIME())",
            new("@runId", run.RunId), new("@code", code), new("@sessionId", request.SessionId),
            new("@batch", request.Batch), new("@trainer", request.TrainerName),
            new("@questions", JsonSerializer.Serialize(request.Questions)));
        return Snapshot(run);
    }

    public LiveQuizSnapshot? GetSnapshot(string code) => _runs.TryGetValue(code, out var run) ? Snapshot(run) : null;

    public async Task<LiveQuizSnapshot> JoinAsync(JoinLiveQuizDto dto)
    {
        if (!_runs.TryGetValue(dto.Code.Trim(), out var run)) throw new HubException("Invalid or expired quiz code.");
        if (run.Stage is "Completed") throw new HubException("This quiz has ended.");
        var participant = run.Participants.GetOrAdd(dto.StudentId, _ => new Participant(dto.StudentId, dto.Usn, dto.Name));
        await EnsureTablesAsync();
        await ExecuteAsync(@"IF NOT EXISTS(SELECT 1 FROM LiveQuizParticipants WHERE RunId=@runId AND StudentId=@studentId)
            INSERT INTO LiveQuizParticipants(RunId,StudentId,Usn,StudentName,JoinedAt,CumulativeScore) VALUES(@runId,@studentId,@usn,@name,SYSUTCDATETIME(),0)",
            new("@runId", run.RunId), new("@studentId", dto.StudentId), new("@usn", dto.Usn), new("@name", dto.Name));
        await BroadcastAsync(run);
        return Snapshot(run);
    }

    public async Task StartLobbyAsync(string code)
    {
        var run = Required(code);
        lock (run.SyncRoot) { run.Stage = "Joining"; run.Status = "Waiting"; run.SecondsRemaining = 120; }
        await PersistStateAsync(run);
        await BroadcastAsync(run);
        StartTimer(run, () => StartQuestionAsync(code));
    }

    public async Task StartQuestionAsync(string code)
    {
        var run = Required(code);
        CancelTimer(run);
        lock (run.SyncRoot)
        {
            run.Stage = "Question"; run.Status = "Running";
            run.SecondsRemaining = CurrentQuestion(run).AnswerTimeSeconds;
            foreach (var participant in run.Participants.Values) { participant.SelectedOption = null; participant.CurrentScore = 0; }
        }
        await PersistStateAsync(run);
        await BroadcastAsync(run);
        StartTimer(run, () => CompleteQuestionAsync(run));
    }

    public async Task SubmitAnswerAsync(SubmitLiveQuizAnswerDto dto)
    {
        var run = Required(dto.Code);
        if (run.Stage != "Question" || !run.Participants.TryGetValue(dto.StudentId, out var participant))
            throw new HubException("The question is not accepting answers.");
        lock (run.SyncRoot)
        {
            if (participant.SelectedOption.HasValue) return;
            participant.SelectedOption = dto.OptionIndex;
        }
        await BroadcastAsync(run);
    }

    public async Task PauseAsync(string code)
    {
        var run = Required(code); CancelTimer(run); run.Status = "Paused";
        await PersistStateAsync(run); await BroadcastAsync(run);
    }

    public async Task NextQuestionAsync(string code)
    {
        var run = Required(code);
        if (run.CurrentQuestionIndex + 1 >= run.Request.Questions.Count) { await EndAsync(run); return; }
        run.CurrentQuestionIndex++;
        await StartQuestionAsync(code);
    }

    private async Task CompleteQuestionAsync(Run run)
    {
        CancelTimer(run);
        var question = CurrentQuestion(run);
        foreach (var participant in run.Participants.Values)
        {
            var correct = question.QuestionType.Equals("Multiple Choice", StringComparison.OrdinalIgnoreCase)
                ? question.CorrectOptionIndexes.Contains(participant.SelectedOption ?? -1)
                : participant.SelectedOption == question.CorrectOptionIndex;
            participant.CurrentScore = correct ? question.Marks : 0;
            participant.CumulativeScore += participant.CurrentScore;
            await ExecuteAsync(@"INSERT INTO LiveQuizAnswers(RunId,StudentId,QuestionIndex,SelectedOptionIndex,IsCorrect,Score,SubmittedAt)
                VALUES(@runId,@studentId,@question,@option,@correct,@score,SYSUTCDATETIME())",
                new("@runId", run.RunId), new("@studentId", participant.StudentId), new("@question", run.CurrentQuestionIndex),
                new("@option", (object?)participant.SelectedOption ?? DBNull.Value), new("@correct", correct), new("@score", participant.CurrentScore));
            await ExecuteAsync("UPDATE LiveQuizParticipants SET CumulativeScore=@score WHERE RunId=@runId AND StudentId=@studentId",
                new("@score", participant.CumulativeScore), new("@runId", run.RunId), new("@studentId", participant.StudentId));
        }
        run.Stage = "Results"; run.Status = "Completed"; run.SecondsRemaining = 0;
        await PersistStateAsync(run); await BroadcastAsync(run);
        await _hub.Clients.Group(Group(run.Code)).SendAsync("QuestionEnded", new {
            correctOptionIndex = question.CorrectOptionIndex, correctOptionIndexes = question.CorrectOptionIndexes,
            participants = Snapshot(run).Participants
        });
    }

    private async Task EndAsync(Run run)
    {
        CancelTimer(run); run.Stage = "Completed"; run.Status = "Completed"; run.SecondsRemaining = 0;
        await PersistStateAsync(run); await BroadcastAsync(run);
    }

    private void StartTimer(Run run, Func<Task> whenExpired)
    {
        CancelTimer(run); var cts = new CancellationTokenSource(); run.TimerCancellation = cts;
        _ = Task.Run(async () => {
            try {
                while (run.SecondsRemaining > 0) {
                    await Task.Delay(1000, cts.Token); run.SecondsRemaining--;
                    await _hub.Clients.Group(Group(run.Code)).SendAsync("TimerTick", run.SecondsRemaining, cts.Token);
                }
                if (!cts.IsCancellationRequested) await whenExpired();
            } catch (OperationCanceledException) { }
        });
    }

    private static void CancelTimer(Run run) { run.TimerCancellation?.Cancel(); run.TimerCancellation?.Dispose(); run.TimerCancellation = null; }
    private Run Required(string code) => _runs.TryGetValue(code.Trim(), out var run) ? run : throw new HubException("Live quiz not found.");
    private static SessionQuizQuestionDto CurrentQuestion(Run run) => run.Request.Questions[run.CurrentQuestionIndex];

    private static LiveQuizSnapshot Snapshot(Run run)
    {
        var question = run.Stage is "Question" or "Results" ? CurrentQuestion(run) : null;
        var view = question is null ? null : new LiveQuizQuestionView(run.CurrentQuestionIndex, question.QuestionLabel,
            question.QuestionText, question.Options, question.AnswerTimeSeconds, question.Marks);
        var participants = run.Participants.Values.OrderByDescending(x => x.CumulativeScore).ThenBy(x => x.Name)
            .Select(x => new LiveQuizParticipantView(x.StudentId, x.Usn, x.Name, x.SelectedOption.HasValue, x.CurrentScore, x.CumulativeScore)).ToList();
        return new(run.RunId, run.Code, run.Request.SessionId, run.Request.Batch, run.Stage, run.Status,
            run.CurrentQuestionIndex, run.SecondsRemaining, participants.Count(x => x.Answered), view, participants);
    }

    private Task BroadcastAsync(Run run) => _hub.Clients.Group(Group(run.Code)).SendAsync("QuizStateChanged", Snapshot(run));
    private Task PersistStateAsync(Run run) => ExecuteAsync(@"UPDATE LiveQuizRuns SET Stage=@stage,Status=@status,CurrentQuestionIndex=@question,SecondsRemaining=@seconds,UpdatedAt=SYSUTCDATETIME() WHERE RunId=@runId",
        new("@stage", run.Stage), new("@status", run.Status), new("@question", run.CurrentQuestionIndex), new("@seconds", run.SecondsRemaining), new("@runId", run.RunId));

    private Task EnsureTablesAsync() => ExecuteAsync(@"
        IF OBJECT_ID('dbo.LiveQuizRuns','U') IS NULL CREATE TABLE dbo.LiveQuizRuns(RunId NVARCHAR(32) PRIMARY KEY,QuizCode NVARCHAR(6) NOT NULL UNIQUE,SessionId NVARCHAR(30) NOT NULL,Batch NVARCHAR(100) NOT NULL,TrainerName NVARCHAR(150) NOT NULL,QuestionsJson NVARCHAR(MAX) NOT NULL,Stage NVARCHAR(20) NOT NULL,Status NVARCHAR(20) NOT NULL,CurrentQuestionIndex INT NOT NULL,SecondsRemaining INT NOT NULL,CreatedAt DATETIME2 NOT NULL,UpdatedAt DATETIME2 NULL);
        IF OBJECT_ID('dbo.LiveQuizParticipants','U') IS NULL CREATE TABLE dbo.LiveQuizParticipants(RunId NVARCHAR(32) NOT NULL,StudentId INT NOT NULL,Usn NVARCHAR(30) NOT NULL,StudentName NVARCHAR(150) NOT NULL,JoinedAt DATETIME2 NOT NULL,CumulativeScore INT NOT NULL,CONSTRAINT PK_LiveQuizParticipants PRIMARY KEY(RunId,StudentId));
        IF OBJECT_ID('dbo.LiveQuizAnswers','U') IS NULL CREATE TABLE dbo.LiveQuizAnswers(AnswerId BIGINT IDENTITY(1,1) PRIMARY KEY,RunId NVARCHAR(32) NOT NULL,StudentId INT NOT NULL,QuestionIndex INT NOT NULL,SelectedOptionIndex INT NULL,IsCorrect BIT NOT NULL,Score INT NOT NULL,SubmittedAt DATETIME2 NOT NULL,CONSTRAINT UQ_LiveQuizAnswers UNIQUE(RunId,StudentId,QuestionIndex));");

    private async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddRange(parameters); await command.ExecuteNonQueryAsync();
    }

    public void Dispose() { foreach (var run in _runs.Values) CancelTimer(run); }
}
