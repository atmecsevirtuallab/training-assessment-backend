using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using TrainingAndAssessmentWebAPI.Models;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class PortalRepository
{
    private readonly string _connectionString;

    public PortalRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TrainingAssessmentPortal")
            ?? throw new InvalidOperationException("ConnectionStrings:TrainingAssessmentPortal is not configured.");
    }

    public async Task<DashboardSummaryDto?> GetSummaryAsync() =>
        (await QueryAsync("SELECT * FROM [vw_DashboardSummary]", reader => new DashboardSummaryDto(
            reader.GetInt32("TotalTrainings"),
            reader.GetInt32("ActiveTrainings"),
            reader.GetInt32("ClosedTrainings"),
            reader.GetInt32("TotalStudents"),
            reader.GetInt32("ActiveStudents"),
            reader.GetInt32("TotalTrainers"),
            reader.GetInt32("ActiveTrainers"),
            reader.GetInt32("TotalBatches"),
            reader.GetInt32("ActiveBatches"),
            reader.GetInt32("TotalReports"),
            reader.GetInt32("TotalAnnouncements"),
            reader.GetInt32("TotalFeedbacks")))).FirstOrDefault();

    public Task<List<AccountDto>> GetAccountsAsync() => QueryAsync(@"
        SELECT a.AccountId, r.RoleName, a.Name, a.DepartmentOrBatch, a.Email, a.ContactNo, a.Status, a.LastLoginAt
        FROM Accounts a INNER JOIN Roles r ON r.RoleId = a.RoleId
        ORDER BY r.RoleId, a.AccountId", reader => new AccountDto(
            reader.GetString("AccountId"),
            reader.GetString("RoleName"),
            reader.GetString("Name"),
            reader.GetString("DepartmentOrBatch"),
            reader.GetString("Email"),
            reader.GetString("ContactNo"),
            reader.GetString("Status"),
            reader.GetNullableDateTime("LastLoginAt")));

    public Task<List<StudentDto>> GetStudentsAsync() => QueryAsync(@"
        SELECT StudentId, USN, Name, CurrentSemester, EmailId, ContactNo, Status
        FROM Students ORDER BY StudentId", reader => new StudentDto(
            reader.GetInt32("StudentId"),
            reader.GetString("USN"),
            reader.GetString("Name"),
            reader.GetString("CurrentSemester"),
            reader.GetString("EmailId"),
            reader.GetString("ContactNo"),
            reader.GetString("Status")));

    public Task<List<TrainerDto>> GetTrainersAsync() => QueryAsync(@"
        SELECT TrainerId, Name, Qualification, Designation, TeachingExperience, IndustryExperience, TotalExperience, EmailId, ContactNo, IsActive
        FROM Trainers ORDER BY TrainerId", reader => new TrainerDto(
            reader.GetInt32("TrainerId"),
            reader.GetString("Name"),
            reader.GetString("Qualification"),
            reader.GetString("Designation"),
            reader.GetString("TeachingExperience"),
            reader.GetString("IndustryExperience"),
            reader.GetString("TotalExperience"),
            reader.GetString("EmailId"),
            reader.GetString("ContactNo"),
            reader.GetBoolean("IsActive")));

    public Task<List<TrainingProgramDto>> GetTrainingsAsync() => QueryAsync(@"
        SELECT tp.TrainingId, tp.TrainingType, tp.Objectives, tp.Outcome, tp.TargetAudience, tp.AcademicYear, tp.Duration, tp.Mode,
               tp.StartDate, tp.EndDate, tp.Status,
               ISNULL(STRING_AGG(CAST(tr.Name AS varchar(max)), ', '), '') AS Trainers,
               ISNULL((SELECT STRING_AGG(CAST(b.BatchName AS varchar(max)), ', ') FROM Batches b WHERE b.TrainingId = tp.TrainingId), '') AS Batches
        FROM TrainingPrograms tp
        LEFT JOIN TrainingProgramTrainers tpt ON tpt.TrainingId = tp.TrainingId
        LEFT JOIN Trainers tr ON tr.TrainerId = tpt.TrainerId
        GROUP BY tp.TrainingId, tp.TrainingType, tp.Objectives, tp.Outcome, tp.TargetAudience, tp.AcademicYear, tp.Duration, tp.Mode, tp.StartDate, tp.EndDate, tp.Status
        ORDER BY tp.TrainingId", reader => new TrainingProgramDto(
            reader.GetInt32("TrainingId"),
            reader.GetString("TrainingType"),
            reader.GetString("Objectives"),
            reader.GetString("Outcome"),
            reader.GetString("TargetAudience"),
            reader.GetString("AcademicYear"),
            reader.GetString("Duration"),
            reader.GetString("Mode"),
            DateOnly.FromDateTime(reader.GetDateTime("StartDate")),
            DateOnly.FromDateTime(reader.GetDateTime("EndDate")),
            reader.GetString("Status"),
            reader.GetString("Trainers"),
            reader.GetString("Batches")));

    public Task<List<BatchDto>> GetBatchesAsync() => QueryAsync(@"
        SELECT b.BatchId, b.BatchCode, b.BatchName, tp.TrainingType,
               COUNT(bs.StudentId) AS Strength, b.IsActive,
               ISNULL(STRING_AGG(CAST(s.Name AS varchar(max)), ', '), '') AS Students
        FROM Batches b
        INNER JOIN TrainingPrograms tp ON tp.TrainingId = b.TrainingId
        LEFT JOIN BatchStudents bs ON bs.BatchId = b.BatchId
        LEFT JOIN Students s ON s.StudentId = bs.StudentId
        GROUP BY b.BatchId, b.BatchCode, b.BatchName, tp.TrainingType, b.IsActive
        ORDER BY b.BatchId", reader => new BatchDto(
            reader.GetInt32("BatchId"),
            reader.GetString("BatchCode"),
            reader.GetString("BatchName"),
            reader.GetString("TrainingType"),
            reader.GetInt32("Strength"),
            reader.GetBoolean("IsActive"),
            reader.GetString("Students")));

    public Task<List<AnnouncementDto>> GetAnnouncementsAsync() => QueryAsync(@"
        SELECT AnnouncementId, Subject, Message, PublishedBy, PublishedTo, PublishedAt
        FROM Announcements ORDER BY PublishedAt DESC", reader => new AnnouncementDto(
            reader.GetString("AnnouncementId"),
            reader.GetString("Subject"),
            reader.GetString("Message"),
            reader.GetString("PublishedBy"),
            reader.GetString("PublishedTo"),
            reader.GetDateTime("PublishedAt")));

    public async Task<string> CreateAnnouncementAsync(SaveAnnouncementDto dto)
    {
        var trainingIds = await QueryAsync(
            "SELECT TOP 1 TrainingId FROM TrainingPrograms WHERE TrainingType = @program",
            r => r.GetInt32("TrainingId"), new SqlParameter("@program", dto.TrainingProgram));
        var announcementId = $"ANN-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var publishedTo = $"{dto.TrainingProgram} -> {string.Join(", ", dto.Batches)}";
        await ExecuteNonQueryAsync(@"
            INSERT INTO Announcements (AnnouncementId, TrainingId, Subject, Message, PublishedBy, PublishedTo, PublishedAt)
            VALUES (@id, @trainingId, @subject, @message, @by, @to, SYSUTCDATETIME())",
            new SqlParameter("@id", announcementId),
            new SqlParameter("@trainingId", trainingIds.Count == 0 ? DBNull.Value : trainingIds[0]),
            new SqlParameter("@subject", dto.Subject.Trim()),
            new SqlParameter("@message", dto.Message.Trim()),
            new SqlParameter("@by", dto.PublishedBy.Trim()),
            new SqlParameter("@to", publishedTo));
        return announcementId;
    }

    public async Task<List<FeedbackFormDto>> GetFeedbacksAsync()
    {
        await EnsureFeedbackExtensionsAsync();
        return await QueryAsync(@"
        SELECT ff.FeedbackId, tp.TrainingType, b.BatchName, ff.PublishedAt, ff.DeadlineAt,
               COUNT(DISTINCT bs.StudentId) AS Assigned,
               COUNT(DISTINCT CASE WHEN fr.SubmittedAt IS NOT NULL THEN fr.StudentId END) AS Submitted,
               COUNT(DISTINCT bs.StudentId) - COUNT(DISTINCT CASE WHEN fr.SubmittedAt IS NOT NULL THEN fr.StudentId END) AS NotSubmitted,
               ff.Suggestions
        FROM FeedbackForms ff
        INNER JOIN TrainingPrograms tp ON tp.TrainingId = ff.TrainingId
        INNER JOIN Batches b ON b.BatchId = ff.BatchId
        LEFT JOIN BatchStudents bs ON bs.BatchId = ff.BatchId
        LEFT JOIN FeedbackResponses fr ON fr.FeedbackId = ff.FeedbackId AND fr.StudentId = bs.StudentId
        GROUP BY ff.FeedbackId, tp.TrainingType, b.BatchName, ff.PublishedAt, ff.DeadlineAt, ff.Suggestions
        ORDER BY ff.PublishedAt DESC", reader =>
        {
            var submitted = reader.GetInt32("Submitted");
            var rating = submitted > 0 ? "4.8 / 5" : "-";
            return new FeedbackFormDto(
                reader.GetString("FeedbackId"),
                reader.GetString("TrainingType"),
                reader.GetString("BatchName"),
                reader.GetDateTime("PublishedAt"),
                reader.GetDateTime("DeadlineAt"),
                reader.GetInt32("Assigned"),
                submitted,
                reader.GetInt32("NotSubmitted"),
                rating,
                reader.GetNullableString("Suggestions"));
        });
    }

    public async Task<string> CreateFeedbackAsync(SaveFeedbackFormDto dto)
    {
        await EnsureFeedbackExtensionsAsync();
        var trainingId = Convert.ToInt32(await ExecuteScalarAsync(
            "SELECT TOP 1 TrainingId FROM TrainingPrograms WHERE TrainingType=@program",
            new SqlParameter("@program", dto.Program)));
        var batchId = Convert.ToInt32(await ExecuteScalarAsync(
            "SELECT TOP 1 BatchId FROM Batches WHERE BatchName=@batch OR BatchCode=@batch",
            new SqlParameter("@batch", dto.Batch)));
        var feedbackId = $"FB-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        await ExecuteNonQueryAsync(@"
            INSERT INTO FeedbackForms(FeedbackId,TrainingId,BatchId,SessionId,PublishedAt,DeadlineAt,Suggestions)
            VALUES(@id,@trainingId,@batchId,@sessionId,SYSUTCDATETIME(),@deadline,@suggestions)",
            new SqlParameter("@id", feedbackId), new SqlParameter("@trainingId", trainingId),
            new SqlParameter("@batchId", batchId), new SqlParameter("@sessionId", dto.SessionId),
            new SqlParameter("@deadline", dto.DeadlineAt),
            new SqlParameter("@suggestions", (object?)dto.Suggestions ?? DBNull.Value));
        foreach (var question in dto.Questions)
        {
            await ExecuteNonQueryAsync(@"
                INSERT INTO FeedbackQuestions(FeedbackId,QuestionNo,QuestionText,OptionsJson)
                VALUES(@id,@number,@text,@options)",
                new SqlParameter("@id", feedbackId), new SqlParameter("@number", question.QuestionNo),
                new SqlParameter("@text", question.QuestionText),
                new SqlParameter("@options", JsonSerializer.Serialize(question.Options)));
        }
        return feedbackId;
    }

    public async Task<List<AvailableFeedbackDto>> GetAvailableFeedbacksAsync(string batch, int studentId)
    {
        await EnsureFeedbackExtensionsAsync();
        var forms = await QueryAsync(@"
            SELECT ff.FeedbackId,tp.TrainingType,b.BatchName,ff.SessionId,
                   COALESCE(ts.SessionName,ff.SessionId) SessionName,ff.PublishedAt,ff.DeadlineAt,
                   CASE WHEN EXISTS(SELECT 1 FROM FeedbackResponses fr WHERE fr.FeedbackId=ff.FeedbackId AND fr.StudentId=@studentId AND fr.SubmittedAt IS NOT NULL) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END Submitted
            FROM FeedbackForms ff
            INNER JOIN TrainingPrograms tp ON tp.TrainingId=ff.TrainingId
            INNER JOIN Batches b ON b.BatchId=ff.BatchId
            LEFT JOIN TrainingSessions ts ON ts.SessionId=ff.SessionId
            WHERE (b.BatchName=@batch OR b.BatchCode=@batch) AND ff.DeadlineAt >= SYSUTCDATETIME()
            ORDER BY ff.PublishedAt DESC", reader => new {
                FeedbackId = reader.GetString("FeedbackId"), Program = reader.GetString("TrainingType"),
                Batch = reader.GetString("BatchName"), SessionId = reader.GetNullableString("SessionId") ?? string.Empty,
                SessionName = reader.GetString("SessionName"), PublishedAt = reader.GetDateTime("PublishedAt"),
                DeadlineAt = reader.GetDateTime("DeadlineAt"), Submitted = reader.GetBoolean("Submitted")
            }, new SqlParameter("@studentId", studentId), new SqlParameter("@batch", batch));
        var result = new List<AvailableFeedbackDto>();
        foreach (var form in forms)
        {
            var questions = await QueryAsync(@"
                SELECT QuestionNo,QuestionText,OptionsJson FROM FeedbackQuestions
                WHERE FeedbackId=@id ORDER BY QuestionNo", reader => new SaveFeedbackQuestionDto(
                    reader.GetInt32("QuestionNo"), reader.GetString("QuestionText"),
                    JsonSerializer.Deserialize<List<string>>(reader.GetString("OptionsJson")) ?? []),
                new SqlParameter("@id", form.FeedbackId));
            result.Add(new AvailableFeedbackDto(form.FeedbackId, form.Program, form.Batch, form.SessionId,
                form.SessionName, form.PublishedAt, form.DeadlineAt, form.Submitted, questions));
        }
        return result;
    }

    public async Task SubmitFeedbackAsync(string feedbackId, SubmitFeedbackDto dto)
    {
        await EnsureFeedbackExtensionsAsync();
        await ExecuteNonQueryAsync("DELETE FROM FeedbackResponses WHERE FeedbackId=@id AND StudentId=@studentId",
            new SqlParameter("@id", feedbackId), new SqlParameter("@studentId", dto.StudentId));
        foreach (var response in dto.Responses)
        {
            await ExecuteNonQueryAsync(@"
                INSERT INTO FeedbackResponses(FeedbackId,StudentId,QuestionNo,SelectedOption,SubmittedAt,Attendance,IsConsidered,StudentSuggestions)
                VALUES(@id,@studentId,@number,@option,SYSUTCDATETIME(),'Present',1,@suggestions)",
                new SqlParameter("@id", feedbackId), new SqlParameter("@studentId", dto.StudentId),
                new SqlParameter("@number", response.QuestionNo), new SqlParameter("@option", response.SelectedOption),
                new SqlParameter("@suggestions", (object?)dto.Suggestions ?? DBNull.Value));
        }
    }

    private Task<int> EnsureFeedbackExtensionsAsync() => ExecuteNonQueryAsync(@"
        IF COL_LENGTH('dbo.FeedbackForms','SessionId') IS NULL
            ALTER TABLE dbo.FeedbackForms ADD SessionId NVARCHAR(30) NULL;
        IF COL_LENGTH('dbo.FeedbackQuestions','OptionsJson') IS NULL
            ALTER TABLE dbo.FeedbackQuestions ADD OptionsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_FeedbackQuestions_OptionsJson DEFAULT('[""Strongly Agree"",""Agree"",""Disagree"",""Strongly Disagree""]');
        IF COL_LENGTH('dbo.FeedbackResponses','StudentSuggestions') IS NULL
            ALTER TABLE dbo.FeedbackResponses ADD StudentSuggestions NVARCHAR(1000) NULL;");

    public Task<List<LoginHistoryDto>> GetLoginHistoryAsync(string? role = null, string? accountId = null)
    {
        var where = new List<string>();
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrWhiteSpace(role) && !role.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("AccountType = @role");
            parameters.Add(new SqlParameter("@role", role));
        }
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            where.Add("AccountId = @accountId");
            parameters.Add(new SqlParameter("@accountId", accountId));
        }

        var sql = "SELECT * FROM vw_LoginHistoryWithTotals"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY LastLoggedIn DESC";

        return QueryAsync(sql, reader => new LoginHistoryDto(
            reader.GetInt64("LoginHistoryId"),
            reader.GetString("AccountType"),
            reader.GetString("Name"),
            reader.GetDateTime("LastLoggedIn"),
            reader.GetNullableDateTime("LastLoggedOut"),
            reader.GetInt32("StayOnPortalMinutes"),
            reader.GetInt32("TotalStayOnPortalMinutes")), parameters.ToArray());
    }

    public Task<List<ReportDto>> GetReportsAsync() => QueryAsync(@"
        SELECT ReportId, ReportTitle, Description, UpdatedAt
        FROM Reports ORDER BY UpdatedAt DESC", reader => new ReportDto(
            reader.GetInt32("ReportId"),
            reader.GetString("ReportTitle"),
            reader.GetString("Description"),
            reader.GetDateTime("UpdatedAt")));

    public async Task<SessionNotesDto> GetSessionNotesAsync(string sessionId)
    {
        await EnsureSessionNotesTableAsync();
        var rows = await QueryAsync(@"
            SELECT SessionId, ContentHtml, UpdatedAt
            FROM SessionNotes
            WHERE SessionId = @SessionId", reader => new SessionNotesDto(
                reader.GetString("SessionId"),
                reader.GetString("ContentHtml"),
                reader.GetNullableDateTime("UpdatedAt")),
            new SqlParameter("@SessionId", sessionId));

        return rows.FirstOrDefault() ?? new SessionNotesDto(sessionId, string.Empty, null);
    }

    public async Task SaveSessionNotesAsync(string sessionId, string contentHtml)
    {
        await EnsureSessionNotesTableAsync();
        await ExecuteNonQueryAsync(@"
            MERGE SessionNotes AS target
            USING (SELECT @SessionId AS SessionId) AS source
            ON target.SessionId = source.SessionId
            WHEN MATCHED THEN
                UPDATE SET ContentHtml = @ContentHtml, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (SessionId, ContentHtml, UpdatedAt)
                VALUES (@SessionId, @ContentHtml, SYSUTCDATETIME());",
            new SqlParameter("@SessionId", sessionId),
            new SqlParameter("@ContentHtml", contentHtml));
    }

    private Task<int> EnsureSessionNotesTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionNotes', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SessionNotes
            (
                SessionId NVARCHAR(30) NOT NULL PRIMARY KEY,
                ContentHtml NVARCHAR(MAX) NOT NULL,
                UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SessionNotes_UpdatedAt DEFAULT SYSUTCDATETIME()
            );
        END;");

    public async Task<List<SessionQuizQuestionDto>> GetSessionQuizAsync(string sessionId, string quizType)
    {
        await EnsureSessionQuizQuestionsTableAsync();
        return await QueryAsync(@"
            SELECT QuestionLabel, QuestionText, OptionsJson, AnswerTimeSeconds, Marks, CorrectOptionIndex,
                   QuestionType, CorrectOptionIndexesJson
            FROM SessionQuizQuestions
            WHERE SessionId = @SessionId AND QuizType = @QuizType
            ORDER BY DisplayOrder", reader => new SessionQuizQuestionDto(
                reader.GetString("QuestionLabel"),
                reader.GetString("QuestionText"),
                JsonSerializer.Deserialize<List<string>>(reader.GetString("OptionsJson")) ?? new List<string>(),
                reader.GetInt32("AnswerTimeSeconds"),
                reader.GetInt32("Marks"),
                reader["CorrectOptionIndex"] == DBNull.Value ? null : Convert.ToInt32(reader["CorrectOptionIndex"]),
                reader.GetString("QuestionType"),
                JsonSerializer.Deserialize<List<int>>(reader.GetString("CorrectOptionIndexesJson")) ?? new List<int>()),
            new SqlParameter("@SessionId", sessionId),
            new SqlParameter("@QuizType", quizType));
    }

    public async Task SaveSessionQuizAsync(string sessionId, string quizType, List<SessionQuizQuestionDto> questions)
    {
        await EnsureSessionQuizQuestionsTableAsync();
        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using (var delete = new SqlCommand(
                "DELETE FROM SessionQuizQuestions WHERE SessionId = @SessionId AND QuizType = @QuizType", connection, transaction))
            {
                delete.Parameters.AddWithValue("@SessionId", sessionId);
                delete.Parameters.AddWithValue("@QuizType", quizType);
                await delete.ExecuteNonQueryAsync();
            }

            for (var index = 0; index < questions.Count; index++)
            {
                var question = questions[index];
                await using var insert = new SqlCommand(@"
                    INSERT INTO SessionQuizQuestions
                        (SessionId, QuizType, DisplayOrder, QuestionLabel, QuestionText, OptionsJson, AnswerTimeSeconds, Marks, CorrectOptionIndex, QuestionType, CorrectOptionIndexesJson, UpdatedAt)
                    VALUES
                        (@SessionId, @QuizType, @DisplayOrder, @QuestionLabel, @QuestionText, @OptionsJson, @AnswerTimeSeconds, @Marks, @CorrectOptionIndex, @QuestionType, @CorrectOptionIndexesJson, SYSUTCDATETIME())", connection, transaction);
                insert.Parameters.AddWithValue("@SessionId", sessionId);
                insert.Parameters.AddWithValue("@QuizType", quizType);
                insert.Parameters.AddWithValue("@DisplayOrder", index + 1);
                insert.Parameters.AddWithValue("@QuestionLabel", question.QuestionLabel);
                insert.Parameters.AddWithValue("@QuestionText", question.QuestionText);
                insert.Parameters.AddWithValue("@OptionsJson", JsonSerializer.Serialize(question.Options));
                insert.Parameters.AddWithValue("@AnswerTimeSeconds", question.AnswerTimeSeconds);
                insert.Parameters.AddWithValue("@Marks", question.Marks);
                insert.Parameters.AddWithValue("@CorrectOptionIndex", (object?)question.CorrectOptionIndex ?? DBNull.Value);
                insert.Parameters.AddWithValue("@QuestionType", string.IsNullOrWhiteSpace(question.QuestionType) ? "Single Choice" : question.QuestionType);
                insert.Parameters.AddWithValue("@CorrectOptionIndexesJson", JsonSerializer.Serialize(question.CorrectOptionIndexes));
                await insert.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private Task<int> EnsureSessionQuizQuestionsTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionQuizQuestions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SessionQuizQuestions
            (
                SessionQuizQuestionId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                SessionId NVARCHAR(30) NOT NULL,
                QuizType NVARCHAR(30) NOT NULL,
                DisplayOrder INT NOT NULL,
                QuestionLabel NVARCHAR(100) NOT NULL,
                QuestionText NVARCHAR(MAX) NOT NULL,
                OptionsJson NVARCHAR(MAX) NOT NULL,
                AnswerTimeSeconds INT NOT NULL,
                Marks INT NOT NULL,
                CorrectOptionIndex INT NULL,
                QuestionType NVARCHAR(30) NOT NULL CONSTRAINT DF_SessionQuizQuestions_QuestionType DEFAULT 'Single Choice',
                CorrectOptionIndexesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionQuizQuestions_CorrectOptionIndexesJson DEFAULT '[]',
                UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SessionQuizQuestions_UpdatedAt DEFAULT SYSUTCDATETIME(),
                CONSTRAINT UQ_SessionQuizQuestions UNIQUE (SessionId, QuizType, DisplayOrder)
            );
        END;
        ELSE
        BEGIN
            IF COL_LENGTH('dbo.SessionQuizQuestions', 'QuestionType') IS NULL
                ALTER TABLE dbo.SessionQuizQuestions ADD QuestionType NVARCHAR(30) NOT NULL CONSTRAINT DF_SessionQuizQuestions_QuestionType DEFAULT 'Single Choice';
            IF COL_LENGTH('dbo.SessionQuizQuestions', 'CorrectOptionIndexesJson') IS NULL
                ALTER TABLE dbo.SessionQuizQuestions ADD CorrectOptionIndexesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionQuizQuestions_CorrectOptionIndexesJson DEFAULT '[]';
        END;");

    public async Task<ProgrammingExerciseDto> GetProgrammingExerciseAsync(string sessionId, string itemLabel)
    {
        await EnsureProgrammingExercisesTableAsync();
        var rows = await QueryAsync(@"
            SELECT ItemLabel, Question, TestCasesJson FROM SessionProgrammingExercises
            WHERE SessionId=@SessionId AND ItemLabel=@ItemLabel", reader => new ProgrammingExerciseDto(
                reader.GetString("ItemLabel"), reader.GetString("Question"),
                JsonSerializer.Deserialize<List<ProgrammingTestCaseDto>>(reader.GetString("TestCasesJson")) ?? new()),
            new SqlParameter("@SessionId", sessionId), new SqlParameter("@ItemLabel", itemLabel));
        return rows.FirstOrDefault() ?? new ProgrammingExerciseDto(itemLabel, string.Empty, new());
    }

    public async Task SaveProgrammingExerciseAsync(string sessionId, ProgrammingExerciseDto exercise)
    {
        await EnsureProgrammingExercisesTableAsync();
        await ExecuteNonQueryAsync(@"
            MERGE SessionProgrammingExercises AS target
            USING (SELECT @SessionId SessionId, @ItemLabel ItemLabel) source
            ON target.SessionId=source.SessionId AND target.ItemLabel=source.ItemLabel
            WHEN MATCHED THEN UPDATE SET Question=@Question, TestCasesJson=@TestCasesJson, UpdatedAt=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(SessionId,ItemLabel,Question,TestCasesJson,UpdatedAt)
            VALUES(@SessionId,@ItemLabel,@Question,@TestCasesJson,SYSUTCDATETIME());",
            new SqlParameter("@SessionId", sessionId), new SqlParameter("@ItemLabel", exercise.ItemLabel),
            new SqlParameter("@Question", exercise.Question),
            new SqlParameter("@TestCasesJson", JsonSerializer.Serialize(exercise.TestCases)));
    }

    private Task<int> EnsureProgrammingExercisesTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionProgrammingExercises', N'U') IS NULL
        CREATE TABLE dbo.SessionProgrammingExercises(
            SessionProgrammingExerciseId INT IDENTITY(1,1) PRIMARY KEY,
            SessionId NVARCHAR(30) NOT NULL, ItemLabel NVARCHAR(100) NOT NULL,
            Question NVARCHAR(MAX) NOT NULL, TestCasesJson NVARCHAR(MAX) NOT NULL,
            UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UQ_SessionProgrammingExercises UNIQUE(SessionId,ItemLabel));");

    public async Task<long> SaveProgrammingSubmissionAsync(SubmitProgramDto submission, ProgramExecutionResponseDto result)
    {
        await EnsureProgrammingSubmissionsTableAsync();
        var value = await ExecuteScalarAsync(@"
            INSERT INTO ProgrammingExecutionSubmissions
                (SessionId,ItemLabel,Language,SourceCode,TestCasesJson,ResultsJson,CompileError,StudentId,Usn,StudentName,SubmittedAt)
            VALUES(@SessionId,@ItemLabel,@Language,@SourceCode,@TestCasesJson,@ResultsJson,@CompileError,@StudentId,@Usn,@StudentName,SYSUTCDATETIME());
            SELECT SCOPE_IDENTITY();",
            new SqlParameter("@SessionId", submission.SessionId), new SqlParameter("@ItemLabel", submission.ItemLabel),
            new SqlParameter("@Language", submission.Language), new SqlParameter("@SourceCode", submission.Code),
            new SqlParameter("@TestCasesJson", JsonSerializer.Serialize(submission.TestCases)),
            new SqlParameter("@ResultsJson", JsonSerializer.Serialize(result.Results)),
            new SqlParameter("@CompileError", (object?)result.CompileError ?? DBNull.Value),
            new SqlParameter("@StudentId", (object?)submission.StudentId ?? DBNull.Value),
            new SqlParameter("@Usn", (object?)submission.Usn ?? DBNull.Value),
            new SqlParameter("@StudentName", (object?)submission.StudentName ?? DBNull.Value));
        return Convert.ToInt64(value);
    }

    private Task<int> EnsureProgrammingSubmissionsTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.ProgrammingExecutionSubmissions', N'U') IS NULL
        CREATE TABLE dbo.ProgrammingExecutionSubmissions(
            SubmissionId BIGINT IDENTITY(1,1) PRIMARY KEY, SessionId NVARCHAR(30) NOT NULL,
            ItemLabel NVARCHAR(100) NOT NULL, Language NVARCHAR(20) NOT NULL,
            SourceCode NVARCHAR(MAX) NOT NULL, TestCasesJson NVARCHAR(MAX) NOT NULL,
            ResultsJson NVARCHAR(MAX) NOT NULL, CompileError NVARCHAR(MAX) NULL,
            StudentId INT NULL, Usn NVARCHAR(30) NULL, StudentName NVARCHAR(150) NULL,
            SubmittedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
        IF COL_LENGTH('dbo.ProgrammingExecutionSubmissions','StudentId') IS NULL
            ALTER TABLE dbo.ProgrammingExecutionSubmissions ADD StudentId INT NULL;
        IF COL_LENGTH('dbo.ProgrammingExecutionSubmissions','Usn') IS NULL
            ALTER TABLE dbo.ProgrammingExecutionSubmissions ADD Usn NVARCHAR(30) NULL;
        IF COL_LENGTH('dbo.ProgrammingExecutionSubmissions','StudentName') IS NULL
            ALTER TABLE dbo.ProgrammingExecutionSubmissions ADD StudentName NVARCHAR(150) NULL;");

    public async Task<int> CreateTrainingProgramAsync(SaveTrainingProgramDto dto)
    {
        const string sql = @"
            INSERT INTO TrainingPrograms (TrainingType, Objectives, Outcome, TargetAudience, AcademicYear, Duration, Mode, StartDate, EndDate, Status)
            VALUES (@type, @obj, @out, @audience, @year, @dur, @mode, @start, @end, @status);
            SELECT SCOPE_IDENTITY();";
        
        var id = Convert.ToInt32(await ExecuteScalarAsync(sql,
            new SqlParameter("@type", dto.TrainingType),
            new SqlParameter("@obj", dto.Objectives),
            new SqlParameter("@out", dto.Outcome),
            new SqlParameter("@audience", dto.TargetAudience),
            new SqlParameter("@year", dto.AcademicYear),
            new SqlParameter("@dur", dto.Duration),
            new SqlParameter("@mode", dto.Mode),
            new SqlParameter("@start", dto.StartDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@end", dto.EndDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status)));

        if (dto.TrainerIds != null && dto.TrainerIds.Count > 0)
        {
            foreach (var trainerId in dto.TrainerIds)
            {
                await ExecuteNonQueryAsync("INSERT INTO TrainingProgramTrainers (TrainingId, TrainerId) VALUES (@tId, @trId)",
                    new SqlParameter("@tId", id), new SqlParameter("@trId", trainerId));
            }
        }
        return id;
    }

    public async Task<bool> UpdateTrainingProgramAsync(int id, SaveTrainingProgramDto dto)
    {
        const string sql = @"
            UPDATE TrainingPrograms
            SET TrainingType = @type, Objectives = @obj, Outcome = @out, TargetAudience = @audience,
                AcademicYear = @year, Duration = @dur, Mode = @mode, StartDate = @start, EndDate = @end, Status = @status
            WHERE TrainingId = @id";
        
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@id", id),
            new SqlParameter("@type", dto.TrainingType),
            new SqlParameter("@obj", dto.Objectives),
            new SqlParameter("@out", dto.Outcome),
            new SqlParameter("@audience", dto.TargetAudience),
            new SqlParameter("@year", dto.AcademicYear),
            new SqlParameter("@dur", dto.Duration),
            new SqlParameter("@mode", dto.Mode),
            new SqlParameter("@start", dto.StartDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@end", dto.EndDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status));

        if (dto.TrainerIds != null)
        {
            await ExecuteNonQueryAsync("DELETE FROM TrainingProgramTrainers WHERE TrainingId = @id", new SqlParameter("@id", id));
            foreach (var trainerId in dto.TrainerIds)
            {
                await ExecuteNonQueryAsync("INSERT INTO TrainingProgramTrainers (TrainingId, TrainerId) VALUES (@tId, @trId)",
                    new SqlParameter("@tId", id), new SqlParameter("@trId", trainerId));
            }
        }
        return rows > 0;
    }

    public async Task<bool> DeleteTrainingProgramAsync(int id)
    {
        await ExecuteNonQueryAsync("DELETE FROM TrainingProgramTrainers WHERE TrainingId = @id", new SqlParameter("@id", id));
        var rows = await ExecuteNonQueryAsync("DELETE FROM TrainingPrograms WHERE TrainingId = @id", new SqlParameter("@id", id));
        return rows > 0;
    }

    public async Task<int> CreateStudentAsync(SaveStudentDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string accountSql = @"
                DECLARE @nextId int = ISNULL((SELECT MAX(TRY_CONVERT(int, SUBSTRING(AccountId, 4, 20))) FROM Accounts WHERE AccountId LIKE 'STD%'), 0) + 1;
                DECLARE @accountId varchar(30) = 'STD' + RIGHT('000000' + CONVERT(varchar(20), @nextId), 6);
                INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
                VALUES (@accountId, 4, @name, @department, @email, @contact, @status, @passwordHash, SYSUTCDATETIME());
                SELECT @accountId;";
            await using var accountCommand = new SqlCommand(accountSql, connection, transaction);
            accountCommand.Parameters.AddRange(new[] {
                new SqlParameter("@name", dto.Name), new SqlParameter("@department", $"Student - {dto.CurrentSemester} Semester"),
                new SqlParameter("@email", dto.EmailId.Trim().ToLowerInvariant()), new SqlParameter("@contact", dto.ContactNo),
                new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status),
                new SqlParameter("@passwordHash", PasswordSecurity.Hash(dto.Usn))
            });
            var accountId = Convert.ToString(await accountCommand.ExecuteScalarAsync())!;

            const string studentSql = @"
                INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
                VALUES (@accountId, @usn, @name, @sem, @email, @contact, @status);
                SELECT SCOPE_IDENTITY();";
            await using var studentCommand = new SqlCommand(studentSql, connection, transaction);
            studentCommand.Parameters.AddRange(new[] {
                new SqlParameter("@accountId", accountId), new SqlParameter("@usn", dto.Usn),
                new SqlParameter("@name", dto.Name), new SqlParameter("@sem", dto.CurrentSemester),
                new SqlParameter("@email", dto.EmailId.Trim().ToLowerInvariant()), new SqlParameter("@contact", dto.ContactNo),
                new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status)
            });
            var studentId = Convert.ToInt32(await studentCommand.ExecuteScalarAsync());
            await transaction.CommitAsync();
            return studentId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<AuthenticatedUserDto?> AuthenticateAsync(LoginRequestDto dto)
    {
        if (dto.Role.Equals("Student", StringComparison.OrdinalIgnoreCase))
            await EnsureStudentAccountAsync(dto.Username);

        const string sql = @"
            SELECT TOP 1 a.AccountId, r.RoleName, a.Name, a.Email, a.DepartmentOrBatch, a.PasswordHash,
                   s.StudentId, s.USN, s.CurrentSemester
            FROM Accounts a
            INNER JOIN Roles r ON r.RoleId = a.RoleId
            LEFT JOIN Students s ON s.AccountId = a.AccountId
            WHERE (LOWER(a.Email) = LOWER(@username) OR LOWER(s.EmailId) = LOWER(@username))
              AND LOWER(r.RoleName) = LOWER(@role) AND a.Status = 'Active'";
        var rows = await QueryAsync(sql, reader => new {
            AccountId = reader.GetString("AccountId"), Role = reader.GetString("RoleName"), Name = reader.GetString("Name"),
            Email = reader.GetString("Email"), Department = reader.GetString("DepartmentOrBatch"), Hash = reader.GetNullableString("PasswordHash"),
            StudentId = reader["StudentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["StudentId"]),
            Usn = reader.GetNullableString("USN"), Semester = reader.GetNullableString("CurrentSemester")
        }, new SqlParameter("@username", dto.Username.Trim()), new SqlParameter("@role", dto.Role.Trim()));
        var row = rows.FirstOrDefault();
        if (row is null) return null;

        var valid = PasswordSecurity.Verify(dto.Password, row.Hash);
        // Legacy rows predate authentication. First login applies the role's configured default and upgrades it to a secure hash.
        var defaultPassword = row.Role.Equals("Student", StringComparison.OrdinalIgnoreCase) ? row.Usn
            : row.Role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? "HOD"
            : row.Role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? "TRAINER"
            : null;
        if (!valid && string.IsNullOrWhiteSpace(row.Hash) && defaultPassword is not null &&
            string.Equals(dto.Password, defaultPassword, StringComparison.Ordinal))
        {
            await ExecuteNonQueryAsync("UPDATE Accounts SET PasswordHash = @hash, LastLoginAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE AccountId = @id",
                new SqlParameter("@hash", PasswordSecurity.Hash(dto.Password)), new SqlParameter("@id", row.AccountId));
            valid = true;
        }
        else if (valid)
        {
            await ExecuteNonQueryAsync("UPDATE Accounts SET LastLoginAt = SYSUTCDATETIME() WHERE AccountId = @id", new SqlParameter("@id", row.AccountId));
        }
        return !valid ? null : new AuthenticatedUserDto(row.AccountId, row.Role, row.StudentId, row.Name, row.Email, row.Usn, row.Semester, row.Department);
    }

    private async Task EnsureStudentAccountAsync(string username)
    {
        const string findSql = @"
            SELECT TOP 1 StudentId, USN, Name, CurrentSemester, EmailId, ContactNo, Status
            FROM Students WHERE AccountId IS NULL AND LOWER(EmailId) = LOWER(@email)";
        var students = await QueryAsync(findSql, r => new {
            Id = r.GetInt32("StudentId"), Usn = r.GetString("USN"), Name = r.GetString("Name"), Semester = r.GetString("CurrentSemester"),
            Email = r.GetString("EmailId"), Contact = r.GetString("ContactNo"), Status = r.GetString("Status")
        }, new SqlParameter("@email", username.Trim()));
        var student = students.FirstOrDefault();
        if (student is null) return;

        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM Students WITH (UPDLOCK, HOLDLOCK) WHERE StudentId = @studentId AND AccountId IS NULL)
                BEGIN
                    DECLARE @nextId int = ISNULL((SELECT MAX(TRY_CONVERT(int, SUBSTRING(AccountId, 4, 20))) FROM Accounts WHERE AccountId LIKE 'STD%'), 0) + 1;
                    DECLARE @accountId varchar(30) = 'STD' + RIGHT('000000' + CONVERT(varchar(20), @nextId), 6);
                    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
                    VALUES (@accountId, 4, @name, @department, @email, @contact, @status, @hash, SYSUTCDATETIME());
                    UPDATE Students SET AccountId = @accountId WHERE StudentId = @studentId;
                END";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddRange(new[] {
                new SqlParameter("@studentId", student.Id), new SqlParameter("@name", student.Name),
                new SqlParameter("@department", $"Student - {student.Semester} Semester"), new SqlParameter("@email", student.Email.ToLowerInvariant()),
                new SqlParameter("@contact", student.Contact), new SqlParameter("@status", student.Status),
                new SqlParameter("@hash", PasswordSecurity.Hash(student.Usn))
            });
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto dto)
    {
        var rows = await QueryAsync("SELECT PasswordHash FROM Accounts WHERE AccountId = @id", r => r.GetNullableString("PasswordHash"), new SqlParameter("@id", dto.AccountId));
        if (rows.Count == 0 || !PasswordSecurity.Verify(dto.CurrentPassword, rows[0])) return false;
        return await ExecuteNonQueryAsync("UPDATE Accounts SET PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME() WHERE AccountId = @id",
            new SqlParameter("@hash", PasswordSecurity.Hash(dto.NewPassword)), new SqlParameter("@id", dto.AccountId)) > 0;
    }

    public async Task<bool> ResetPasswordByAdminAsync(string accountId, string newPassword) =>
        await ExecuteNonQueryAsync(
            "UPDATE Accounts SET PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME() WHERE AccountId = @id",
            new SqlParameter("@hash", PasswordSecurity.Hash(newPassword)),
            new SqlParameter("@id", accountId)) > 0;

    public async Task<string> CreateAccountAsync(CreateAccountDto dto)
    {
        var roleId = dto.Role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? 2
            : dto.Role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? 3
            : 4;

        var prefix = dto.Role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? "HOD"
            : dto.Role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? "TRN"
            : "STD";

        var accountId = dto.AccountId;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            var nextId = Convert.ToInt32(await ExecuteScalarAsync(
                $"SELECT ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(AccountId, {prefix.Length + 1}, 20))), 0) + 1 FROM Accounts WHERE AccountId LIKE '{prefix}%'"));
            accountId = $"{prefix}{nextId:D3}";
        }

        var defaultPass = string.IsNullOrWhiteSpace(dto.Password) ? "Atme@1234" : dto.Password;
        var hash = PasswordSecurity.Hash(defaultPass);

        const string sql = @"
            INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
            VALUES (@accountId, @roleId, @name, @dept, @email, @contact, 'Active', @hash, SYSUTCDATETIME());";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@accountId", accountId),
            new SqlParameter("@roleId", roleId),
            new SqlParameter("@name", dto.Name),
            new SqlParameter("@dept", dto.DepartmentOrBatch ?? string.Empty),
            new SqlParameter("@email", dto.Email.Trim().ToLowerInvariant()),
            new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
            new SqlParameter("@hash", hash));

        return accountId;
    }

    public async Task<int> BulkCreateStudentsAsync(List<SaveStudentDto> dtos)
    {
        int count = 0;
        foreach (var dto in dtos)
        {
            try
            {
                await CreateStudentAsync(dto);
                count++;
            }
            catch
            {
                // continue on duplicate key errors
            }
        }
        return count;
    }

    public async Task<bool> UpdateStudentAsync(int id, SaveStudentDto dto)
    {
        const string sql = @"
            UPDATE Students
            SET USN = @usn, Name = @name, CurrentSemester = @sem, EmailId = @email, ContactNo = @contact, Status = @status
            WHERE StudentId = @id;
            UPDATE a SET a.Name = @name, a.Email = @email, a.ContactNo = @contact, a.Status = @status,
                         a.DepartmentOrBatch = @department, a.UpdatedAt = SYSUTCDATETIME()
            FROM Accounts a INNER JOIN Students s ON s.AccountId = a.AccountId WHERE s.StudentId = @id;";

        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@id", id),
            new SqlParameter("@usn", dto.Usn),
            new SqlParameter("@name", dto.Name),
            new SqlParameter("@sem", dto.CurrentSemester),
            new SqlParameter("@department", $"Student - {dto.CurrentSemester} Semester"),
            new SqlParameter("@email", dto.EmailId),
            new SqlParameter("@contact", dto.ContactNo),
            new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status));

        return rows > 0;
    }

    public async Task<bool> DeleteStudentAsync(int id)
    {
        var accountIds = await QueryAsync("SELECT AccountId FROM Students WHERE StudentId = @id", r => r.GetNullableString("AccountId"), new SqlParameter("@id", id));
        await ExecuteNonQueryAsync("DELETE FROM BatchStudents WHERE StudentId = @id", new SqlParameter("@id", id));
        var rows = await ExecuteNonQueryAsync("DELETE FROM Students WHERE StudentId = @id", new SqlParameter("@id", id));
        if (rows > 0 && accountIds.FirstOrDefault() is { Length: > 0 } accountId)
            await ExecuteNonQueryAsync("DELETE FROM Accounts WHERE AccountId = @accountId", new SqlParameter("@accountId", accountId));
        return rows > 0;
    }

    public async Task<int> CreateBatchAsync(SaveBatchDto dto)
    {
        const string sql = @"
            INSERT INTO Batches (BatchCode, BatchName, TrainingId, IsActive)
            VALUES (@code, @name, @tId, @active);
            SELECT SCOPE_IDENTITY();";

        return Convert.ToInt32(await ExecuteScalarAsync(sql,
            new SqlParameter("@code", dto.BatchCode),
            new SqlParameter("@name", dto.BatchName),
            new SqlParameter("@tId", dto.TrainingId),
            new SqlParameter("@active", dto.IsActive)));
    }

    public async Task<bool> UpdateBatchAsync(int id, SaveBatchDto dto)
    {
        const string sql = @"
            UPDATE Batches
            SET BatchCode = @code, BatchName = @name, TrainingId = @tId, IsActive = @active
            WHERE BatchId = @id";

        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@id", id),
            new SqlParameter("@code", dto.BatchCode),
            new SqlParameter("@name", dto.BatchName),
            new SqlParameter("@tId", dto.TrainingId),
            new SqlParameter("@active", dto.IsActive));

        return rows > 0;
    }

    public async Task<bool> DeleteBatchAsync(int id)
    {
        await ExecuteNonQueryAsync("DELETE FROM BatchStudents WHERE BatchId = @id", new SqlParameter("@id", id));
        var rows = await ExecuteNonQueryAsync("DELETE FROM Batches WHERE BatchId = @id", new SqlParameter("@id", id));
        return rows > 0;
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await OpenConnectionWithRetryAsync(connection);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await OpenConnectionWithRetryAsync(connection);
        return await command.ExecuteScalarAsync();
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
    {
        var rows = new List<T>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await OpenConnectionWithRetryAsync(connection);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }
        return rows;
    }

    private static async Task OpenConnectionWithRetryAsync(SqlConnection connection)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await connection.OpenAsync();
                return;
            }
            catch (SqlException) when (attempt < maxAttempts)
            {
                // LocalDB uses a named pipe that changes whenever its instance restarts.
                // Discard pooled connections that still reference the previous pipe before retrying.
                SqlConnection.ClearPool(connection);
                await connection.CloseAsync();
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }
}


internal static class SqlReaderExtensions
{
    public static string GetString(this SqlDataReader reader, string name) => reader[name]?.ToString() ?? string.Empty;
    public static int GetInt32(this SqlDataReader reader, string name) => Convert.ToInt32(reader[name]);
    public static long GetInt64(this SqlDataReader reader, string name) => Convert.ToInt64(reader[name]);
    public static bool GetBoolean(this SqlDataReader reader, string name) => Convert.ToBoolean(reader[name]);
    public static DateTime GetDateTime(this SqlDataReader reader, string name) => Convert.ToDateTime(reader[name]);
    public static DateTime? GetNullableDateTime(this SqlDataReader reader, string name) => reader[name] == DBNull.Value ? null : Convert.ToDateTime(reader[name]);
    public static decimal? GetNullableDecimal(this SqlDataReader reader, string name) => reader[name] == DBNull.Value ? null : Convert.ToDecimal(reader[name]);
    public static string? GetNullableString(this SqlDataReader reader, string name) => reader[name] == DBNull.Value ? null : reader[name].ToString();
}
