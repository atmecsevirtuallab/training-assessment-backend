namespace TrainingAndAssessmentWebAPI.Models;

public sealed record CreateLiveQuizDto(string SessionId, string Batch, string TrainerName, List<SessionQuizQuestionDto> Questions);
public sealed record JoinLiveQuizDto(string Code, int StudentId, string Usn, string Name);
public sealed record SubmitLiveQuizAnswerDto(string Code, int StudentId, int OptionIndex);

public sealed record LiveQuizQuestionView(
    int Index, string QuestionLabel, string QuestionText, List<string> Options, int AnswerTimeSeconds, int Marks);

public sealed record LiveQuizParticipantView(
    int StudentId, string Usn, string Name, bool Answered, int CurrentScore, int CumulativeScore,
    long? ResponseMilliseconds, long TotalResponseMilliseconds);

public sealed record LiveQuizSnapshot(
    string RunId, string Code, string SessionId, string Batch, string Stage, string Status,
    int CurrentQuestionIndex, int SecondsRemaining, int AnsweredCount,
    LiveQuizQuestionView? Question, List<LiveQuizParticipantView> Participants);
