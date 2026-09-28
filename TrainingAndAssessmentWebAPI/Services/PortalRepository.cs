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

    private Task<int> EnsureAssessmentBatchAccessTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID('dbo.AssessmentBatchAccess','U') IS NULL
        CREATE TABLE dbo.AssessmentBatchAccess(
            AssessmentId VARCHAR(30) NOT NULL,
            BatchId INT NOT NULL,
            GrantedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AssessmentBatchAccess_GrantedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_AssessmentBatchAccess PRIMARY KEY(AssessmentId, BatchId),
            CONSTRAINT FK_AssessmentBatchAccess_Assessment FOREIGN KEY(AssessmentId) REFERENCES Assessments(AssessmentId) ON DELETE CASCADE,
            CONSTRAINT FK_AssessmentBatchAccess_Batch FOREIGN KEY(BatchId) REFERENCES Batches(BatchId) ON DELETE CASCADE
        );");

    public async Task GrantAssessmentAccessAsync(string assessmentId, string[] batches)
    {
        await EnsureAssessmentBatchAccessTableAsync();
        foreach (var batch in batches.Distinct(StringComparer.OrdinalIgnoreCase))
            await ExecuteNonQueryAsync(@"
                INSERT INTO AssessmentBatchAccess(AssessmentId,BatchId,GrantedAt)
                SELECT @assessmentId, BatchId, SYSUTCDATETIME() FROM Batches
                WHERE BatchName=@batch AND NOT EXISTS(
                    SELECT 1 FROM AssessmentBatchAccess WHERE AssessmentId=@assessmentId AND BatchId=Batches.BatchId);",
                new SqlParameter("@assessmentId", assessmentId), new SqlParameter("@batch", batch));
        await ExecuteNonQueryAsync("UPDATE Assessments SET Status='Open', PublishedAt=COALESCE(PublishedAt,SYSUTCDATETIME()) WHERE AssessmentId=@id", new SqlParameter("@id", assessmentId));
    }

    public async Task RevokeAssessmentAccessAsync(string assessmentId, string[] batches)
    {
        await EnsureAssessmentBatchAccessTableAsync();
        foreach (var batch in batches.Distinct(StringComparer.OrdinalIgnoreCase))
            await ExecuteNonQueryAsync(@"DELETE aba FROM AssessmentBatchAccess aba INNER JOIN Batches b ON b.BatchId=aba.BatchId WHERE aba.AssessmentId=@assessmentId AND b.BatchName=@batch",
                new SqlParameter("@assessmentId", assessmentId), new SqlParameter("@batch", batch));
        await ExecuteNonQueryAsync(@"UPDATE Assessments SET Status=CASE WHEN EXISTS(SELECT 1 FROM AssessmentBatchAccess WHERE AssessmentId=@id) THEN 'Open' ELSE 'Closed' END WHERE AssessmentId=@id", new SqlParameter("@id", assessmentId));
    }

    public async Task<List<AssessmentBatchGrantDto>> GetAssessmentBatchAccessAsync()
    {
        await EnsureAssessmentBatchAccessTableAsync();
        return await QueryAsync(@"
            SELECT aba.AssessmentId,b.BatchName,tp.TrainingType,aba.GrantedAt
            FROM AssessmentBatchAccess aba
            INNER JOIN Batches b ON b.BatchId=aba.BatchId
            INNER JOIN Assessments a ON a.AssessmentId=aba.AssessmentId
            INNER JOIN TrainingPrograms tp ON tp.TrainingId=a.TrainingId
            ORDER BY aba.AssessmentId,b.BatchName",
            r => new AssessmentBatchGrantDto(r.GetString("AssessmentId"),r.GetString("BatchName"),r.GetString("TrainingType"),r.GetDateTime("GrantedAt")));
    }

    public async Task<List<StudentAssessmentDto>> GetStudentAssessmentsAsync(int studentId)
    {
        await EnsureAssessmentBatchAccessTableAsync();
        await ExecuteNonQueryAsync(@"
            DECLARE @trainingId INT=(SELECT TOP 1 TrainingId FROM TrainingPrograms WHERE TrainingType LIKE '%Data Structures%' ORDER BY TrainingId);
            IF @trainingId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Assessments WHERE AssessmentId='Assessment-1')
                INSERT Assessments VALUES('Assessment-1',@trainingId,'DSA_Pre-Assessment_Quiz','Pre-assessment quiz for evaluating DSA readiness.','Quiz','Closed','50 Marks',NULL);
            IF @trainingId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Assessments WHERE AssessmentId='Assessment-2')
                INSERT Assessments VALUES('Assessment-2',@trainingId,'DSA_Pre-Assessment_Programming','Pre-assessment programming assignment for evaluating coding readiness before training starts.','Programming Assignment','Closed','20 Marks',NULL);
            IF @trainingId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Assessments WHERE AssessmentId='Assessment-3')
                INSERT Assessments VALUES('Assessment-3',@trainingId,'DSA_Pre-Assessment_Descriptive','Pre-assessment descriptive assignment on Data Structures and Algorithms concepts before training starts.','Descriptive Assignment','Closed','30 Marks',NULL);");
        return await QueryAsync(@"
            SELECT a.AssessmentId,a.Title,a.Description,a.AssessmentType,a.Status,a.MaxScore,a.PublishedAt,
                   tp.TrainingType,
                   CAST(CASE WHEN EXISTS(
                       SELECT 1 FROM BatchStudents bs INNER JOIN AssessmentBatchAccess aba ON aba.BatchId=bs.BatchId
                       WHERE bs.StudentId=@studentId AND aba.AssessmentId=a.AssessmentId) THEN 1 ELSE 0 END AS bit) CanAttempt,
                   CAST(CASE WHEN sub.SubmissionId IS NULL THEN 0 ELSE 1 END AS bit) Attempted,
                   sub.Score, sub.SubmittedAt
            FROM Assessments a
            INNER JOIN TrainingPrograms tp ON tp.TrainingId=a.TrainingId
            OUTER APPLY (
                SELECT TOP 1 s.SubmissionId,s.Score,s.SubmittedAt
                FROM AssessmentSubmissions s
                WHERE s.AssessmentId=a.AssessmentId AND s.StudentId=@studentId
                ORDER BY s.SubmittedAt DESC,s.SubmissionId DESC
            ) sub
            ORDER BY a.AssessmentId",
            r => new StudentAssessmentDto(r.GetString("AssessmentId"),r.GetString("Title"),r.GetString("Description"),r.GetString("AssessmentType"),r.GetString("Status"),r.GetNullableString("MaxScore"),r.GetNullableDateTime("PublishedAt"),r.GetString("TrainingType"),r.GetBoolean("CanAttempt"),r.GetBoolean("Attempted"),r.GetNullableString("Score"),r.GetNullableDateTime("SubmittedAt")),
            new SqlParameter("@studentId", studentId));
    }

    public async Task<bool> SaveAssessmentSubmissionAsync(string assessmentId, SaveAssessmentSubmissionDto dto)
    {
        await EnsureAssessmentBatchAccessTableAsync();
        var affected = await ExecuteNonQueryAsync(@"
            IF EXISTS(
                SELECT 1 FROM BatchStudents bs
                INNER JOIN AssessmentBatchAccess aba ON aba.BatchId=bs.BatchId
                WHERE bs.StudentId=@studentId AND aba.AssessmentId=@assessmentId)
            BEGIN
                UPDATE AssessmentSubmissions
                SET SubmissionStatus='Submitted',SubmittedAt=SYSUTCDATETIME(),Score=@score
                WHERE AssessmentId=@assessmentId AND StudentId=@studentId;
                IF @@ROWCOUNT=0
                    INSERT INTO AssessmentSubmissions(AssessmentId,StudentId,SubmissionStatus,SubmittedAt,Score)
                    VALUES(@assessmentId,@studentId,'Submitted',SYSUTCDATETIME(),@score);
            END",
            new SqlParameter("@assessmentId", assessmentId),
            new SqlParameter("@studentId", dto.StudentId),
            new SqlParameter("@score", dto.Score));
        return affected > 0;
    }

    public async Task<List<AssessmentSubmissionDto>> GetAssessmentSubmissionsAsync(string assessmentId)
    {
        await EnsureAssessmentBatchAccessTableAsync();
        return await QueryAsync(@"
        SELECT ISNULL(sub.SubmissionId,0) AS SubmissionId,@assessmentId AS AssessmentId,s.StudentId,s.USN,s.Name AS StudentName,
               b.BatchName AS Batch,ISNULL(sub.SubmissionStatus,'Not Submitted') AS SubmissionStatus,sub.SubmittedAt,sub.Score
        FROM AssessmentBatchAccess access
        INNER JOIN Batches b ON b.BatchId=access.BatchId
        INNER JOIN BatchStudents bs ON bs.BatchId=b.BatchId
        INNER JOIN Students s ON s.StudentId=bs.StudentId
        OUTER APPLY (
            SELECT TOP 1 candidate.SubmissionId,candidate.SubmissionStatus,candidate.SubmittedAt,candidate.Score
            FROM AssessmentSubmissions candidate
            WHERE candidate.AssessmentId=@assessmentId AND candidate.StudentId=s.StudentId
            ORDER BY candidate.SubmittedAt DESC,candidate.SubmissionId DESC
        ) sub
        WHERE access.AssessmentId=@assessmentId
        ORDER BY CASE WHEN sub.SubmissionId IS NULL THEN 1 ELSE 0 END,sub.SubmittedAt DESC,s.USN",
        r => new AssessmentSubmissionDto(
            r.GetInt64("SubmissionId"),r.GetString("AssessmentId"),r.GetInt32("StudentId"),
            r.GetString("USN"),r.GetString("StudentName"),r.GetString("Batch"),
            r.GetString("SubmissionStatus"),r.GetNullableDateTime("SubmittedAt"),r.GetNullableString("Score")),
        new SqlParameter("@assessmentId", assessmentId));
    }

    public Task<int> ResetAssessmentSubmissionAsync(string assessmentId, int studentId) => ExecuteNonQueryAsync(@"
        DELETE FROM AssessmentSubmissions
        WHERE AssessmentId=@assessmentId AND StudentId=@studentId",
        new SqlParameter("@assessmentId", assessmentId),
        new SqlParameter("@studentId", studentId));

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
        SELECT a.AccountId, r.RoleName, a.Name, a.DepartmentOrBatch, a.Email, a.ContactNo, a.Status, a.LastLoginAt,
               s.USN, s.CurrentSemester,
               t.Qualification, t.Designation, t.TeachingExperience, t.IndustryExperience, t.TotalExperience
        FROM Accounts a
        INNER JOIN Roles r ON r.RoleId = a.RoleId
        LEFT JOIN Students s ON s.AccountId = a.AccountId
        LEFT JOIN Trainers t ON (LOWER(t.EmailId) = LOWER(a.Email) OR (t.EmailId = '' AND t.Name = a.Name))
        ORDER BY r.RoleId, a.AccountId", reader => new AccountDto(
            reader.GetString("AccountId"),
            reader.GetString("RoleName"),
            reader.GetString("Name"),
            reader.GetString("DepartmentOrBatch"),
            reader.GetString("Email"),
            reader.GetString("ContactNo"),
            reader.GetString("Status"),
            reader.GetNullableDateTime("LastLoginAt"),
            reader.GetNullableString("USN"),
            reader.GetNullableString("CurrentSemester"),
            reader.GetNullableString("Qualification"),
            reader.GetNullableString("Designation"),
            reader.GetNullableString("TeachingExperience"),
            reader.GetNullableString("IndustryExperience"),
            reader.GetNullableString("TotalExperience")));

    public Task<List<StudentDto>> GetStudentsAsync() => QueryAsync(@"
        SELECT s.StudentId, s.USN, s.Name, s.CurrentSemester, s.EmailId, s.ContactNo, s.Status,
               ISNULL(STRING_AGG(CAST(b.BatchName AS varchar(max)), ', '), '') AS Batches
        FROM Students s
        LEFT JOIN BatchStudents bs ON bs.StudentId=s.StudentId
        LEFT JOIN Batches b ON b.BatchId=bs.BatchId
        GROUP BY s.StudentId, s.USN, s.Name, s.CurrentSemester, s.EmailId, s.ContactNo, s.Status
        ORDER BY s.StudentId", reader => new StudentDto(
            reader.GetInt32("StudentId"),
            reader.GetString("USN"),
            reader.GetString("Name"),
            reader.GetString("CurrentSemester"),
            reader.GetString("EmailId"),
            reader.GetString("ContactNo"),
            reader.GetString("Status"),
            reader.GetString("Batches")));

    private async Task EnsureTrainingOwnershipSchemaAsync()
    {
        // Schema changes and statements that reference the new columns must be
        // executed in separate batches. SQL Server compiles the entire batch
        // before ALTER TABLE runs and otherwise reports "Invalid column name".
        await ExecuteNonQueryAsync(@"
        IF COL_LENGTH('dbo.TrainingPrograms','CreatedByAccountId') IS NULL
            ALTER TABLE dbo.TrainingPrograms ADD CreatedByAccountId VARCHAR(30) NULL;
        IF COL_LENGTH('dbo.TrainingPrograms','Department') IS NULL
            ALTER TABLE dbo.TrainingPrograms ADD Department VARCHAR(200) NULL;
        IF COL_LENGTH('dbo.Batches','Department') IS NULL
            ALTER TABLE dbo.Batches ADD Department VARCHAR(200) NULL;");

        await ExecuteNonQueryAsync(@"
        IF OBJECT_ID('dbo.TrainingProgramBatches','U') IS NULL
        BEGIN
            CREATE TABLE dbo.TrainingProgramBatches(
                TrainingId INT NOT NULL REFERENCES dbo.TrainingPrograms(TrainingId) ON DELETE CASCADE,
                BatchId INT NOT NULL REFERENCES dbo.Batches(BatchId),
                CONSTRAINT PK_TrainingProgramBatches PRIMARY KEY(TrainingId,BatchId));
        END;");

        await ExecuteNonQueryAsync(@"
        IF NOT EXISTS (SELECT 1 FROM dbo.TrainingProgramBatches)
        BEGIN
            INSERT INTO dbo.TrainingProgramBatches(TrainingId,BatchId)
            SELECT b.TrainingId,b.BatchId FROM dbo.Batches b
            WHERE b.TrainingId IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM dbo.TrainingProgramBatches tpb WHERE tpb.TrainingId=b.TrainingId AND tpb.BatchId=b.BatchId);
        END;
        DECLARE @defaultDepartment VARCHAR(200) = (
            SELECT TOP 1 a.DepartmentOrBatch FROM dbo.Accounts a
            INNER JOIN dbo.Roles r ON r.RoleId=a.RoleId
            WHERE r.RoleName='Trainer' AND a.Status='Active' ORDER BY a.AccountId);
        SET @defaultDepartment=ISNULL(NULLIF(@defaultDepartment,''),'Unassigned');
        UPDATE dbo.TrainingPrograms SET Department=@defaultDepartment WHERE Department IS NULL OR Department='';
        UPDATE dbo.Batches SET Department=@defaultDepartment WHERE Department IS NULL OR Department='';");
    }

    public async Task<List<TrainerDto>> GetTrainersAsync(string? department = null)
    {
        await EnsureTrainingOwnershipSchemaAsync();
        return await QueryAsync(@"
        SELECT DISTINCT t.TrainerId, t.Name, t.Qualification, t.Designation, t.TeachingExperience, t.IndustryExperience,
               t.TotalExperience, t.EmailId, t.ContactNo, t.IsActive, ISNULL(a.DepartmentOrBatch,'') AS Department
        FROM Trainers t LEFT JOIN Accounts a ON (a.AccountId=t.AccountId OR LOWER(a.Email)=LOWER(t.EmailId) OR a.Name=t.Name)
        WHERE t.IsActive=1 AND (@department IS NULL OR @department='' OR a.DepartmentOrBatch=@department)
        ORDER BY t.TrainerId", reader => new TrainerDto(
            reader.GetInt32("TrainerId"),
            reader.GetString("Name"),
            reader.GetString("Qualification"),
            reader.GetString("Designation"),
            reader.GetString("TeachingExperience"),
            reader.GetString("IndustryExperience"),
            reader.GetString("TotalExperience"),
            reader.GetString("EmailId"),
            reader.GetString("ContactNo"),
            reader.GetBoolean("IsActive"),
            reader.GetString("Department")), new SqlParameter("@department", (object?)department ?? DBNull.Value));
    }

    public async Task<List<TrainingProgramDto>> GetTrainingsAsync()
    {
        await EnsureTrainingOwnershipSchemaAsync();
        return await QueryAsync(@"
        SELECT tp.TrainingId, tp.TrainingType, tp.Objectives, tp.Outcome, tp.TargetAudience, tp.AcademicYear, tp.Duration, tp.Mode,
               tp.StartDate, tp.EndDate, tp.Status,
               ISNULL(STRING_AGG(CAST(tr.Name AS varchar(max)), ', '), '') AS Trainers,
               ISNULL((SELECT STRING_AGG(CAST(b.BatchName AS varchar(max)), ', ') FROM TrainingProgramBatches tpb INNER JOIN Batches b ON b.BatchId=tpb.BatchId WHERE tpb.TrainingId = tp.TrainingId), '') AS Batches
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
    }

    public async Task<List<BatchDto>> GetBatchesAsync(string? department = null)
    {
        await EnsureTrainingOwnershipSchemaAsync();
        return await QueryAsync(@"
        SELECT b.BatchId, b.BatchCode, b.BatchName, tp.TrainingType,
               COUNT(bs.StudentId) AS Strength, b.IsActive,
               ISNULL(STRING_AGG(CAST(s.Name AS varchar(max)), ', '), '') AS Students, ISNULL(b.Department,'') AS Department
        FROM Batches b
        INNER JOIN TrainingPrograms tp ON tp.TrainingId = b.TrainingId
        LEFT JOIN BatchStudents bs ON bs.BatchId = b.BatchId
        LEFT JOIN Students s ON s.StudentId = bs.StudentId
        WHERE b.IsActive=1 AND (@department IS NULL OR @department='' OR b.Department=@department)
        GROUP BY b.BatchId, b.BatchCode, b.BatchName, tp.TrainingType, b.IsActive, b.Department
        ORDER BY b.BatchId", reader => new BatchDto(
            reader.GetInt32("BatchId"),
            reader.GetString("BatchCode"),
            reader.GetString("BatchName"),
            reader.GetString("TrainingType"),
            reader.GetInt32("Strength"),
            reader.GetBoolean("IsActive"),
            reader.GetString("Students"),
            reader.GetString("Department")), new SqlParameter("@department", (object?)department ?? DBNull.Value));
    }

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

    public async Task<List<FeedbackSubmissionRowDto>> GetFeedbackSubmissionsAsync(string feedbackId, string? batch = null)
    {
        await EnsureFeedbackExtensionsAsync();
        var formInfoRows = await QueryAsync(@"
            SELECT ff.FeedbackId, ff.BatchId, b.BatchName
            FROM FeedbackForms ff
            INNER JOIN Batches b ON b.BatchId = ff.BatchId
            WHERE ff.FeedbackId = @feedbackId",
            r => new { FeedbackId = r.GetString("FeedbackId"), BatchId = r.GetInt32("BatchId"), BatchName = r.GetString("BatchName") },
            new SqlParameter("@feedbackId", feedbackId));
        var formInfo = formInfoRows.FirstOrDefault();
        var targetBatchName = !string.IsNullOrWhiteSpace(batch) ? batch : formInfo?.BatchName;

        var students = await QueryAsync(@"
            SELECT s.StudentId, s.USN, s.Name
            FROM Students s
            INNER JOIN BatchStudents bs ON bs.StudentId = s.StudentId
            INNER JOIN Batches b ON b.BatchId = bs.BatchId
            WHERE b.BatchName = @batch OR b.BatchCode = @batch
            ORDER BY s.USN",
            r => new { StudentId = r.GetInt32("StudentId"), Usn = r.GetString("USN"), Name = r.GetString("Name") },
            new SqlParameter("@batch", targetBatchName ?? ""));

        var responses = await QueryAsync(@"
            SELECT StudentId, QuestionNo, SelectedOption, SubmittedAt, Attendance, IsConsidered
            FROM FeedbackResponses
            WHERE FeedbackId = @feedbackId",
            r => new {
                StudentId = r.GetInt32("StudentId"),
                QuestionNo = r.GetInt32("QuestionNo"),
                SelectedOption = r.GetString("SelectedOption"),
                SubmittedAt = r.GetDateTime("SubmittedAt"),
                Attendance = r.GetNullableString("Attendance") ?? "Present",
                IsConsidered = r.GetBoolean("IsConsidered")
            },
            new SqlParameter("@feedbackId", feedbackId));

        var result = new List<FeedbackSubmissionRowDto>();
        long slNo = 1;

        foreach (var student in students)
        {
            var studentResp = responses.Where(r => r.StudentId == student.StudentId).ToList();
            var hasSubmitted = studentResp.Count > 0;

            var qDict = new Dictionary<string, string>();
            for (int i = 1; i <= 12; i++)
            {
                var match = studentResp.FirstOrDefault(r => r.QuestionNo == i);
                qDict[$"Q{i}"] = match != null ? match.SelectedOption : "-";
            }

            var submittedTimeStr = "-";
            if (hasSubmitted)
            {
                var maxTime = studentResp.Max(r => r.SubmittedAt);
                submittedTimeStr = maxTime.ToString("dd/MM/yyyy HH:mm");
            }

            result.Add(new FeedbackSubmissionRowDto(
                slNo++,
                student.Usn,
                student.Name,
                hasSubmitted ? studentResp.First().Attendance : "Present",
                hasSubmitted ? "Valid" : "-",
                hasSubmitted ? (studentResp.First().IsConsidered ? "Yes" : "No") : "No",
                qDict,
                submittedTimeStr
            ));
        }

        return result;
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
            SELECT ItemLabel, Question, ISNULL(StarterCode,'') StarterCode, TestCasesJson FROM SessionProgrammingExercises
            WHERE SessionId=@SessionId AND ItemLabel=@ItemLabel", reader => new ProgrammingExerciseDto(
                reader.GetString("ItemLabel"), reader.GetString("Question"), reader.GetString("StarterCode"),
                JsonSerializer.Deserialize<List<ProgrammingTestCaseDto>>(
                    reader.GetString("TestCasesJson"),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()),
            new SqlParameter("@SessionId", sessionId), new SqlParameter("@ItemLabel", itemLabel));
        return rows.FirstOrDefault() ?? new ProgrammingExerciseDto(itemLabel, string.Empty, string.Empty, new());
    }

    public async Task SaveProgrammingExerciseAsync(string sessionId, ProgrammingExerciseDto exercise)
    {
        await EnsureProgrammingExercisesTableAsync();
        await ExecuteNonQueryAsync(@"
            MERGE SessionProgrammingExercises AS target
            USING (SELECT @SessionId SessionId, @ItemLabel ItemLabel) source
            ON target.SessionId=source.SessionId AND target.ItemLabel=source.ItemLabel
            WHEN MATCHED THEN UPDATE SET Question=@Question, StarterCode=@StarterCode, TestCasesJson=@TestCasesJson, UpdatedAt=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(SessionId,ItemLabel,Question,StarterCode,TestCasesJson,UpdatedAt)
            VALUES(@SessionId,@ItemLabel,@Question,@StarterCode,@TestCasesJson,SYSUTCDATETIME());",
            new SqlParameter("@SessionId", sessionId), new SqlParameter("@ItemLabel", exercise.ItemLabel),
            new SqlParameter("@Question", exercise.Question),
            new SqlParameter("@StarterCode", exercise.StarterCode ?? string.Empty),
            new SqlParameter("@TestCasesJson", JsonSerializer.Serialize(exercise.TestCases)));
    }

    private Task<int> EnsureProgrammingExercisesTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionProgrammingExercises', N'U') IS NULL
        CREATE TABLE dbo.SessionProgrammingExercises(
            SessionProgrammingExerciseId INT IDENTITY(1,1) PRIMARY KEY,
            SessionId NVARCHAR(30) NOT NULL, ItemLabel NVARCHAR(100) NOT NULL,
            Question NVARCHAR(MAX) NOT NULL, StarterCode NVARCHAR(MAX) NOT NULL DEFAULT '', TestCasesJson NVARCHAR(MAX) NOT NULL,
            UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UQ_SessionProgrammingExercises UNIQUE(SessionId,ItemLabel));
        IF COL_LENGTH('dbo.SessionProgrammingExercises','StarterCode') IS NULL
            ALTER TABLE dbo.SessionProgrammingExercises ADD StarterCode NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionProgrammingExercises_StarterCode DEFAULT '';");

    public async Task<DescriptiveAssignmentDto> GetDescriptiveAssignmentAsync(string sessionId, string itemLabel)
    {
        await EnsureDescriptiveAssignmentsTableAsync();
        var rows = await QueryAsync(@"
            SELECT ItemLabel, QuestionsJson FROM SessionDescriptiveAssignments
            WHERE SessionId=@SessionId AND ItemLabel=@ItemLabel",
            reader => new DescriptiveAssignmentDto(
                reader.GetString("ItemLabel"),
                JsonSerializer.Deserialize<List<DescriptiveQuestionDto>>(
                    reader.GetString("QuestionsJson"),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()),
            new SqlParameter("@SessionId", sessionId), new SqlParameter("@ItemLabel", itemLabel));
        return rows.FirstOrDefault() ?? new DescriptiveAssignmentDto(itemLabel, new());
    }

    public async Task SaveDescriptiveAssignmentAsync(string sessionId, DescriptiveAssignmentDto assignment)
    {
        await EnsureDescriptiveAssignmentsTableAsync();
        await ExecuteNonQueryAsync(@"
            MERGE SessionDescriptiveAssignments AS target
            USING (SELECT @SessionId SessionId, @ItemLabel ItemLabel) source
            ON target.SessionId=source.SessionId AND target.ItemLabel=source.ItemLabel
            WHEN MATCHED THEN UPDATE SET QuestionsJson=@QuestionsJson, UpdatedAt=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(SessionId,ItemLabel,QuestionsJson,UpdatedAt)
            VALUES(@SessionId,@ItemLabel,@QuestionsJson,SYSUTCDATETIME());",
            new SqlParameter("@SessionId", sessionId),
            new SqlParameter("@ItemLabel", assignment.ItemLabel),
            new SqlParameter("@QuestionsJson", JsonSerializer.Serialize(assignment.Questions)));
    }

    private Task<int> EnsureDescriptiveAssignmentsTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionDescriptiveAssignments', N'U') IS NULL
        CREATE TABLE dbo.SessionDescriptiveAssignments(
            SessionDescriptiveAssignmentId INT IDENTITY(1,1) PRIMARY KEY,
            SessionId NVARCHAR(30) NOT NULL,
            ItemLabel NVARCHAR(100) NOT NULL,
            QuestionsJson NVARCHAR(MAX) NOT NULL,
            UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UQ_SessionDescriptiveAssignments UNIQUE(SessionId,ItemLabel));");

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

    private Task<int> EnsureDescriptiveSubmissionsTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.DescriptiveAssignmentSubmissions', N'U') IS NULL
        CREATE TABLE dbo.DescriptiveAssignmentSubmissions(
            SubmissionId BIGINT IDENTITY(1,1) PRIMARY KEY, SessionId NVARCHAR(30) NOT NULL,
            ItemLabel NVARCHAR(100) NOT NULL, StudentId INT NOT NULL, Usn NVARCHAR(30) NOT NULL,
            StudentName NVARCHAR(150) NOT NULL, SubmittedAt DATETIME2 NOT NULL,
            QuestionText NVARCHAR(MAX) NOT NULL, ReferenceAnswer NVARCHAR(MAX) NOT NULL,
            StudentAnswer NVARCHAR(MAX) NOT NULL, AiScore FLOAT NOT NULL, AiMaxScore FLOAT NOT NULL,
            AiFeedback NVARCHAR(MAX) NOT NULL,
            CONSTRAINT UQ_DescriptiveSubmission UNIQUE(SessionId,ItemLabel,StudentId));");

    public async Task<List<DescriptiveSubmissionDto>> GetDescriptiveSubmissionsAsync(string sessionId, string itemLabel)
    {
        await EnsureDescriptiveSubmissionsTableAsync();
        return await QueryAsync(@"SELECT SessionId,ItemLabel,StudentId,Usn,StudentName,SubmittedAt,QuestionText,
            ReferenceAnswer,StudentAnswer,AiScore,AiMaxScore,AiFeedback
            FROM DescriptiveAssignmentSubmissions WHERE SessionId=@sessionId AND ItemLabel=@itemLabel
            ORDER BY SubmittedAt DESC", r => new DescriptiveSubmissionDto(
                r.GetString("SessionId"), r.GetString("ItemLabel"), r.GetInt32("StudentId"), r.GetString("Usn"),
                r.GetString("StudentName"), r.GetDateTime("SubmittedAt").ToString("o"), r.GetString("QuestionText"),
                r.GetString("ReferenceAnswer"), r.GetString("StudentAnswer"), Convert.ToDouble(r["AiScore"]),
                Convert.ToDouble(r["AiMaxScore"]), r.GetString("AiFeedback")),
            new SqlParameter("@sessionId", sessionId), new SqlParameter("@itemLabel", itemLabel));
    }

    public async Task<DescriptiveSubmissionDto> SaveDescriptiveSubmissionAsync(DescriptiveSubmissionDto dto)
    {
        await EnsureDescriptiveSubmissionsTableAsync();
        var submittedAt = DateTime.TryParse(dto.SubmittedAt, out var parsed) ? parsed : DateTime.UtcNow;
        await ExecuteNonQueryAsync(@"MERGE DescriptiveAssignmentSubmissions AS target
            USING (SELECT @sessionId SessionId,@itemLabel ItemLabel,@studentId StudentId) source
            ON target.SessionId=source.SessionId AND target.ItemLabel=source.ItemLabel AND target.StudentId=source.StudentId
            WHEN MATCHED THEN UPDATE SET Usn=@usn,StudentName=@name,SubmittedAt=@submittedAt,QuestionText=@question,
                ReferenceAnswer=@reference,StudentAnswer=@answer,AiScore=@score,AiMaxScore=@maxScore,AiFeedback=@feedback
            WHEN NOT MATCHED THEN INSERT(SessionId,ItemLabel,StudentId,Usn,StudentName,SubmittedAt,QuestionText,
                ReferenceAnswer,StudentAnswer,AiScore,AiMaxScore,AiFeedback)
                VALUES(@sessionId,@itemLabel,@studentId,@usn,@name,@submittedAt,@question,@reference,@answer,@score,@maxScore,@feedback);",
            new SqlParameter("@sessionId", dto.SessionId), new SqlParameter("@itemLabel", dto.ItemLabel),
            new SqlParameter("@studentId", dto.StudentId), new SqlParameter("@usn", dto.Usn), new SqlParameter("@name", dto.Name),
            new SqlParameter("@submittedAt", submittedAt), new SqlParameter("@question", dto.QuestionText),
            new SqlParameter("@reference", dto.ReferenceAnswer), new SqlParameter("@answer", dto.StudentAnswer),
            new SqlParameter("@score", dto.AiScore), new SqlParameter("@maxScore", dto.AiMaxScore),
            new SqlParameter("@feedback", dto.AiFeedback));
        return dto with { SubmittedAt = submittedAt.ToString("o") };
    }

    public async Task<List<SessionSubmissionDto>> GetSessionSubmissionsAsync(string sessionId, string? batch = null)
    {
        await EnsureProgrammingSubmissionsTableAsync();
        await EnsureDescriptiveSubmissionsTableAsync();
        await EnsureLiveQuizSubmissionTablesAsync();
        await EnsureKonnectQuizModeTablesAsync();
        await EnsureFollowUpQuizSubmissionsTableAsync();
        var programming = await QueryAsync(@"WITH latest AS (
            SELECT *,ROW_NUMBER() OVER(PARTITION BY ItemLabel,StudentId ORDER BY SubmittedAt DESC,SubmissionId DESC) rn
            FROM ProgrammingExecutionSubmissions WHERE SessionId=@sessionId AND StudentId IS NOT NULL)
            SELECT ItemLabel,StudentId,ISNULL(Usn,'') Usn,ISNULL(StudentName,'') StudentName,SubmittedAt,ResultsJson,CompileError
            FROM latest WHERE rn=1", r => {
                var results = JsonSerializer.Deserialize<List<ExecutionResultDto>>(r.GetString("ResultsJson"),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var passed = results.Count(x => string.Equals(x.Status, "Passed", StringComparison.OrdinalIgnoreCase));
                var label = r.GetString("ItemLabel");
                return new SessionSubmissionDto(label.Contains("Assignment", StringComparison.OrdinalIgnoreCase) ? "Programming Assignments" : "Programming Exercises",
                    label, r.GetInt32("StudentId"), r.GetString("Usn"), r.GetString("StudentName"), "Submitted",
                    r.GetDateTime("SubmittedAt"), results.Count == 0 ? "-" : $"{passed}/{results.Count}");
            }, new SqlParameter("@sessionId", sessionId));
        var descriptive = await QueryAsync(@"SELECT ItemLabel,StudentId,Usn,StudentName,SubmittedAt,AiScore,AiMaxScore
            FROM DescriptiveAssignmentSubmissions WHERE SessionId=@sessionId", r => new SessionSubmissionDto(
                "Descriptive Assignments", r.GetString("ItemLabel"), r.GetInt32("StudentId"), r.GetString("Usn"),
                r.GetString("StudentName"), "Submitted", r.GetDateTime("SubmittedAt"),
                $"{Convert.ToDouble(r["AiScore"]):0.##}/{Convert.ToDouble(r["AiMaxScore"]):0.##}"),
            new SqlParameter("@sessionId", sessionId));
        var quizzes = await QueryAsync(@"WITH latest AS (
            SELECT p.StudentId,p.Usn,p.StudentName,p.CumulativeScore,p.JoinedAt,r.RunId,r.QuestionsJson,r.CreatedAt,
                ROW_NUMBER() OVER(PARTITION BY p.StudentId ORDER BY r.CreatedAt DESC) rn
            FROM LiveQuizRuns r INNER JOIN LiveQuizParticipants p ON p.RunId=r.RunId WHERE r.SessionId=@sessionId)
            SELECT StudentId,Usn,StudentName,CumulativeScore,JoinedAt,QuestionsJson FROM latest WHERE rn=1", r => {
                var questions = JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(r.GetString("QuestionsJson"),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var total = questions.Sum(q => q.Marks);
                return new SessionSubmissionDto("Konnect Quiz", "Konnect Quiz", r.GetInt32("StudentId"), r.GetString("Usn"),
                    r.GetString("StudentName"), "Submitted", r.GetDateTime("JoinedAt"), $"{r.GetInt32("CumulativeScore")}/{total}");
            }, new SqlParameter("@sessionId", sessionId));
        var normalQuizzes = await QueryAsync(@"SELECT StudentId,Usn,StudentName,Score,MaxScore,SubmittedAt
            FROM NormalKonnectQuizSubmissions WHERE SessionId=@sessionId", r => new SessionSubmissionDto(
                "Konnect Quiz","Konnect Quiz",r.GetInt32("StudentId"),r.GetString("Usn"),r.GetString("StudentName"),
                "Submitted",r.GetDateTime("SubmittedAt"),$"{r.GetInt32("Score")}/{r.GetInt32("MaxScore")}"),
            new SqlParameter("@sessionId",sessionId));
        var followUpQuizzes = await QueryAsync(@"SELECT StudentId,Usn,StudentName,Score,MaxScore,SubmittedAt
            FROM FollowUpQuizSubmissions WHERE SessionId=@sessionId", r => new SessionSubmissionDto(
                "Follow-Up Quiz","Follow-Up Quiz",r.GetInt32("StudentId"),r.GetString("Usn"),r.GetString("StudentName"),
                "Submitted",r.GetDateTime("SubmittedAt"),$"{r.GetInt32("Score")}/{r.GetInt32("MaxScore")}"),
            new SqlParameter("@sessionId",sessionId));
        programming.AddRange(descriptive);
        programming.AddRange(quizzes);
        programming.AddRange(normalQuizzes);
        programming.AddRange(followUpQuizzes);
        if (!string.IsNullOrWhiteSpace(batch))
        {
            var allowedStudentIds = (await QueryAsync(@"SELECT bs.StudentId FROM BatchStudents bs
                INNER JOIN Batches b ON b.BatchId=bs.BatchId
                WHERE b.BatchName=@batch OR b.BatchCode=@batch", r => r.GetInt32("StudentId"),
                new SqlParameter("@batch", batch))).ToHashSet();
            programming = programming.Where(x => allowedStudentIds.Contains(x.StudentId)).ToList();
        }
        return programming.OrderByDescending(x => x.SubmittedAt).ToList();
    }

    public async Task<bool> MoveStudentToBatchAsync(int studentId, string batchName)
    {
        var batchIds = await QueryAsync("SELECT TOP 1 BatchId FROM Batches WHERE BatchName=@batch OR BatchCode=@batch",
            r => r.GetInt32("BatchId"), new SqlParameter("@batch", batchName.Trim()));
        if (batchIds.Count == 0) return false;
        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(@"DELETE FROM BatchStudents WHERE StudentId=@studentId;
                INSERT INTO BatchStudents(BatchId,StudentId) VALUES(@batchId,@studentId);", connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@studentId", studentId);
            command.Parameters.AddWithValue("@batchId", batchIds[0]);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private Task<int> EnsureLiveQuizSubmissionTablesAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID('dbo.LiveQuizRuns','U') IS NULL CREATE TABLE dbo.LiveQuizRuns(RunId NVARCHAR(32) PRIMARY KEY,QuizCode NVARCHAR(6) NOT NULL UNIQUE,SessionId NVARCHAR(30) NOT NULL,Batch NVARCHAR(100) NOT NULL,TrainerName NVARCHAR(150) NOT NULL,QuestionsJson NVARCHAR(MAX) NOT NULL,Stage NVARCHAR(20) NOT NULL,Status NVARCHAR(20) NOT NULL,CurrentQuestionIndex INT NOT NULL,SecondsRemaining INT NOT NULL,CreatedAt DATETIME2 NOT NULL,UpdatedAt DATETIME2 NULL);
        IF OBJECT_ID('dbo.LiveQuizParticipants','U') IS NULL CREATE TABLE dbo.LiveQuizParticipants(RunId NVARCHAR(32) NOT NULL,StudentId INT NOT NULL,Usn NVARCHAR(30) NOT NULL,StudentName NVARCHAR(150) NOT NULL,JoinedAt DATETIME2 NOT NULL,CumulativeScore INT NOT NULL,TotalResponseMilliseconds BIGINT NOT NULL DEFAULT 0,CONSTRAINT PK_LiveQuizParticipants PRIMARY KEY(RunId,StudentId));");

    private Task<int> EnsurePerformanceSnapshotTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionPerformanceAnalysis', N'U') IS NULL
        CREATE TABLE dbo.SessionPerformanceAnalysis(
            SessionId NVARCHAR(30) NOT NULL PRIMARY KEY, PerformanceRowsJson NVARCHAR(MAX) NOT NULL,
            AiAnalysisJson NVARCHAR(MAX) NULL, UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());");

    private Task<int> EnsureKonnectQuizModeTablesAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.SessionKonnectQuizSettings',N'U') IS NULL
        CREATE TABLE dbo.SessionKonnectQuizSettings(SessionId NVARCHAR(30) NOT NULL PRIMARY KEY,
            Mode NVARCHAR(10) NOT NULL,UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
        IF OBJECT_ID(N'dbo.NormalKonnectQuizSubmissions',N'U') IS NULL
        CREATE TABLE dbo.NormalKonnectQuizSubmissions(SubmissionId BIGINT IDENTITY(1,1) PRIMARY KEY,
            SessionId NVARCHAR(30) NOT NULL,StudentId INT NOT NULL,Usn NVARCHAR(30) NOT NULL,
            StudentName NVARCHAR(150) NOT NULL,AnswersJson NVARCHAR(MAX) NOT NULL,Score INT NOT NULL,
            MaxScore INT NOT NULL,SubmittedAt DATETIME2 NOT NULL,
            CONSTRAINT UQ_NormalKonnectQuizSubmission UNIQUE(SessionId,StudentId));");

    private Task<int> EnsureFollowUpQuizSubmissionsTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID(N'dbo.FollowUpQuizSubmissions',N'U') IS NULL
        CREATE TABLE dbo.FollowUpQuizSubmissions(SubmissionId BIGINT IDENTITY(1,1) PRIMARY KEY,
            SessionId NVARCHAR(30) NOT NULL,StudentId INT NOT NULL,Usn NVARCHAR(30) NOT NULL,
            StudentName NVARCHAR(150) NOT NULL,AnswersJson NVARCHAR(MAX) NOT NULL,Score INT NOT NULL,
            MaxScore INT NOT NULL,SubmittedAt DATETIME2 NOT NULL,
            CONSTRAINT UQ_FollowUpQuizSubmission UNIQUE(SessionId,StudentId));");

    public async Task<KonnectQuizModeDto> GetKonnectQuizModeAsync(string sessionId)
    {
        await EnsureKonnectQuizModeTablesAsync();
        var rows = await QueryAsync("SELECT SessionId,Mode FROM SessionKonnectQuizSettings WHERE SessionId=@id",
            r => new KonnectQuizModeDto(r.GetString("SessionId"),r.GetString("Mode")),new SqlParameter("@id",sessionId));
        return rows.FirstOrDefault() ?? new KonnectQuizModeDto(sessionId,"Live");
    }

    public async Task SaveKonnectQuizModeAsync(string sessionId, string mode)
    {
        await EnsureKonnectQuizModeTablesAsync();
        await ExecuteNonQueryAsync(@"MERGE SessionKonnectQuizSettings target USING(SELECT @id SessionId) source
            ON target.SessionId=source.SessionId WHEN MATCHED THEN UPDATE SET Mode=@mode,UpdatedAt=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(SessionId,Mode,UpdatedAt) VALUES(@id,@mode,SYSUTCDATETIME());",
            new SqlParameter("@id",sessionId),new SqlParameter("@mode",mode));
    }

    public async Task<NormalKonnectQuizSubmissionDto> SaveNormalKonnectQuizSubmissionAsync(string sessionId, SubmitNormalKonnectQuizDto dto)
    {
        await EnsureKonnectQuizModeTablesAsync();
        var questions = await GetSessionQuizAsync(sessionId,"Konnect Quiz");
        var score = 0;
        var maxScore = questions.Sum(q => q.Marks);
        for (var i=0;i<questions.Count;i++)
        {
            var selected = i < dto.Answers.Count ? dto.Answers[i] : null;
            var correct = questions[i].CorrectOptionIndexes?.FirstOrDefault() ?? questions[i].CorrectOptionIndex;
            if (selected.HasValue && correct.HasValue && selected.Value == correct.Value) score += questions[i].Marks;
        }
        var submittedAt = DateTime.UtcNow;
        await ExecuteNonQueryAsync(@"MERGE NormalKonnectQuizSubmissions target
            USING(SELECT @sessionId SessionId,@studentId StudentId) source
            ON target.SessionId=source.SessionId AND target.StudentId=source.StudentId
            WHEN MATCHED THEN UPDATE SET Usn=@usn,StudentName=@name,AnswersJson=@answers,Score=@score,MaxScore=@max,SubmittedAt=@at
            WHEN NOT MATCHED THEN INSERT(SessionId,StudentId,Usn,StudentName,AnswersJson,Score,MaxScore,SubmittedAt)
            VALUES(@sessionId,@studentId,@usn,@name,@answers,@score,@max,@at);",
            new SqlParameter("@sessionId",sessionId),new SqlParameter("@studentId",dto.StudentId),new SqlParameter("@usn",dto.Usn),
            new SqlParameter("@name",dto.StudentName),new SqlParameter("@answers",JsonSerializer.Serialize(dto.Answers)),
            new SqlParameter("@score",score),new SqlParameter("@max",maxScore),new SqlParameter("@at",submittedAt));
        return (await GetNormalKonnectQuizSubmissionAsync(sessionId,dto.StudentId))!;
    }

    public async Task<NormalKonnectQuizSubmissionDto?> GetNormalKonnectQuizSubmissionAsync(string sessionId, int studentId)
    {
        await EnsureKonnectQuizModeTablesAsync();
        var rows = await QueryAsync(@"SELECT SubmissionId,SessionId,StudentId,Usn,StudentName,Score,MaxScore,SubmittedAt
            FROM NormalKonnectQuizSubmissions WHERE SessionId=@sessionId AND StudentId=@studentId",
            r => new NormalKonnectQuizSubmissionDto(Convert.ToInt64(r["SubmissionId"]),r.GetString("SessionId"),r.GetInt32("StudentId"),
                r.GetString("Usn"),r.GetString("StudentName"),r.GetInt32("Score"),r.GetInt32("MaxScore"),r.GetDateTime("SubmittedAt")),
            new SqlParameter("@sessionId",sessionId),new SqlParameter("@studentId",studentId));
        return rows.FirstOrDefault();
    }

    public async Task<FollowUpQuizSubmissionDto> SaveFollowUpQuizSubmissionAsync(string sessionId, SubmitNormalKonnectQuizDto dto)
    {
        await EnsureFollowUpQuizSubmissionsTableAsync();
        var questions = await GetSessionQuizAsync(sessionId,"Follow-Up Quiz");
        var score = 0;
        var maxScore = questions.Sum(q => q.Marks);
        for (var i=0;i<questions.Count;i++)
        {
            var selected = i < dto.Answers.Count ? dto.Answers[i] : null;
            var correct = questions[i].CorrectOptionIndexes.Count > 0 ? questions[i].CorrectOptionIndexes[0] : questions[i].CorrectOptionIndex;
            if (selected.HasValue && correct.HasValue && selected.Value == correct.Value) score += questions[i].Marks;
        }
        var submittedAt = DateTime.UtcNow;
        await ExecuteNonQueryAsync(@"MERGE FollowUpQuizSubmissions target
            USING(SELECT @sessionId SessionId,@studentId StudentId) source
            ON target.SessionId=source.SessionId AND target.StudentId=source.StudentId
            WHEN MATCHED THEN UPDATE SET Usn=@usn,StudentName=@name,AnswersJson=@answers,Score=@score,MaxScore=@max,SubmittedAt=@at
            WHEN NOT MATCHED THEN INSERT(SessionId,StudentId,Usn,StudentName,AnswersJson,Score,MaxScore,SubmittedAt)
            VALUES(@sessionId,@studentId,@usn,@name,@answers,@score,@max,@at);",
            new SqlParameter("@sessionId",sessionId),new SqlParameter("@studentId",dto.StudentId),new SqlParameter("@usn",dto.Usn),
            new SqlParameter("@name",dto.StudentName),new SqlParameter("@answers",JsonSerializer.Serialize(dto.Answers)),
            new SqlParameter("@score",score),new SqlParameter("@max",maxScore),new SqlParameter("@at",submittedAt));
        return (await GetFollowUpQuizSubmissionAsync(sessionId,dto.StudentId))!;
    }

    public async Task<FollowUpQuizSubmissionDto?> GetFollowUpQuizSubmissionAsync(string sessionId, int studentId)
    {
        await EnsureFollowUpQuizSubmissionsTableAsync();
        var rows = await QueryAsync(@"SELECT SubmissionId,SessionId,StudentId,Usn,StudentName,Score,MaxScore,SubmittedAt
            FROM FollowUpQuizSubmissions WHERE SessionId=@sessionId AND StudentId=@studentId",
            r => new FollowUpQuizSubmissionDto(Convert.ToInt64(r["SubmissionId"]),r.GetString("SessionId"),r.GetInt32("StudentId"),
                r.GetString("Usn"),r.GetString("StudentName"),r.GetInt32("Score"),r.GetInt32("MaxScore"),r.GetDateTime("SubmittedAt")),
            new SqlParameter("@sessionId",sessionId),new SqlParameter("@studentId",studentId));
        return rows.FirstOrDefault();
    }

    public async Task<SessionPerformanceSnapshotDto?> GetSessionPerformanceSnapshotAsync(string sessionId)
    {
        await EnsurePerformanceSnapshotTableAsync();
        var rows = await QueryAsync("SELECT SessionId,PerformanceRowsJson,AiAnalysisJson,UpdatedAt FROM SessionPerformanceAnalysis WHERE SessionId=@id",
            r => new SessionPerformanceSnapshotDto(r.GetString("SessionId"),r.GetString("PerformanceRowsJson"),r.GetNullableString("AiAnalysisJson"),r.GetDateTime("UpdatedAt")),
            new SqlParameter("@id", sessionId));
        return rows.FirstOrDefault();
    }

    public async Task SaveSessionPerformanceSnapshotAsync(string sessionId, SaveSessionPerformanceSnapshotDto dto)
    {
        await EnsurePerformanceSnapshotTableAsync();
        await ExecuteNonQueryAsync(@"MERGE SessionPerformanceAnalysis AS target USING(SELECT @id SessionId) source
            ON target.SessionId=source.SessionId WHEN MATCHED THEN UPDATE SET PerformanceRowsJson=@rows,AiAnalysisJson=@ai,UpdatedAt=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(SessionId,PerformanceRowsJson,AiAnalysisJson,UpdatedAt) VALUES(@id,@rows,@ai,SYSUTCDATETIME());",
            new SqlParameter("@id",sessionId),new SqlParameter("@rows",dto.PerformanceRowsJson),new SqlParameter("@ai",(object?)dto.AiAnalysisJson ?? DBNull.Value));
    }

    public async Task<int> CreateTrainingProgramAsync(SaveTrainingProgramDto dto)
    {
        await EnsureTrainingOwnershipSchemaAsync();
        const string sql = @"
            INSERT INTO TrainingPrograms (TrainingType, Objectives, Outcome, TargetAudience, AcademicYear, Duration, Mode, StartDate, EndDate, Status, CreatedByAccountId, Department)
            VALUES (@type, @obj, @out, @audience, @year, @dur, @mode, @start, @end, @status, @createdBy, @department);
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
            new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status),
            new SqlParameter("@createdBy", (object?)dto.CreatedByAccountId ?? DBNull.Value),
            new SqlParameter("@department", (object?)dto.Department ?? DBNull.Value)));

        if (dto.TrainerIds != null && dto.TrainerIds.Count > 0)
        {
            foreach (var trainerId in dto.TrainerIds)
            {
                await ExecuteNonQueryAsync("INSERT INTO TrainingProgramTrainers (TrainingId, TrainerId) VALUES (@tId, @trId)",
                    new SqlParameter("@tId", id), new SqlParameter("@trId", trainerId));
            }
        }
        if (dto.BatchIds != null)
            foreach (var batchId in dto.BatchIds.Distinct())
                await ExecuteNonQueryAsync(@"INSERT INTO TrainingProgramBatches(TrainingId,BatchId)
                    SELECT @id,@batchId WHERE EXISTS(SELECT 1 FROM Batches WHERE BatchId=@batchId AND Department=@department)",
                    new SqlParameter("@id", id), new SqlParameter("@batchId", batchId), new SqlParameter("@department", dto.Department ?? string.Empty));
        return id;
    }

    public async Task<bool> UpdateTrainingProgramAsync(int id, SaveTrainingProgramDto dto)
    {
        await EnsureTrainingOwnershipSchemaAsync();
        const string sql = @"
            UPDATE TrainingPrograms
            SET TrainingType = @type, Objectives = @obj, Outcome = @out, TargetAudience = @audience,
                AcademicYear = @year, Duration = @dur, Mode = @mode, StartDate = @start, EndDate = @end, Status = @status,
                CreatedByAccountId=COALESCE(CreatedByAccountId,@createdBy), Department=COALESCE(NULLIF(@department,''),Department)
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
            new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status),
            new SqlParameter("@createdBy", (object?)dto.CreatedByAccountId ?? DBNull.Value),
            new SqlParameter("@department", dto.Department ?? string.Empty));

        if (dto.TrainerIds != null)
        {
            await ExecuteNonQueryAsync("DELETE FROM TrainingProgramTrainers WHERE TrainingId = @id", new SqlParameter("@id", id));
            foreach (var trainerId in dto.TrainerIds)
            {
                await ExecuteNonQueryAsync("INSERT INTO TrainingProgramTrainers (TrainingId, TrainerId) VALUES (@tId, @trId)",
                    new SqlParameter("@tId", id), new SqlParameter("@trId", trainerId));
            }
        }
        if (dto.BatchIds != null)
        {
            await ExecuteNonQueryAsync("DELETE FROM TrainingProgramBatches WHERE TrainingId=@id", new SqlParameter("@id", id));
            foreach (var batchId in dto.BatchIds.Distinct())
                await ExecuteNonQueryAsync(@"INSERT INTO TrainingProgramBatches(TrainingId,BatchId)
                    SELECT @id,@batchId WHERE EXISTS(SELECT 1 FROM Batches WHERE BatchId=@batchId AND Department=@department)",
                    new SqlParameter("@id", id), new SqlParameter("@batchId", batchId), new SqlParameter("@department", dto.Department ?? string.Empty));
        }
        return rows > 0;
    }

    public async Task<bool> DeleteTrainingProgramAsync(int id)
    {
        await EnsureTrainingOwnershipSchemaAsync();
        await ExecuteNonQueryAsync("DELETE FROM TrainingProgramBatches WHERE TrainingId = @id", new SqlParameter("@id", id));
        await ExecuteNonQueryAsync("DELETE FROM TrainingProgramTrainers WHERE TrainingId = @id", new SqlParameter("@id", id));
        var rows = await ExecuteNonQueryAsync("DELETE FROM TrainingPrograms WHERE TrainingId = @id", new SqlParameter("@id", id));
        return rows > 0;
    }

    private async Task EnsurePortalSessionColumnsAsync()
    {
        await ExecuteNonQueryAsync(@"
        IF OBJECT_ID('dbo.TrainingSessions', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TrainingSessions (
                SessionId NVARCHAR(30) NOT NULL PRIMARY KEY,
                TrainingId INT NOT NULL,
                SessionName NVARCHAR(200) NOT NULL,
                SessionDate DATE NOT NULL,
                StartTime NVARCHAR(10) NOT NULL,
                EndTime NVARCHAR(10) NOT NULL,
                Venue NVARCHAR(150) NOT NULL,
                Status NVARCHAR(20) NOT NULL,
                BatchesJson NVARCHAR(MAX) NULL,
                AssessmentsJson NVARCHAR(MAX) NULL,
                BatchSchedulesJson NVARCHAR(MAX) NULL,
                BatchAccessControlJson NVARCHAR(MAX) NULL
            );
        END;");
        // SQL Server compiles a batch before executing ALTER TABLE statements.
        // Add legacy-schema columns in a separate batch before any statement references them.
        await ExecuteNonQueryAsync(@"
        IF COL_LENGTH('dbo.TrainingSessions','BatchesJson') IS NULL ALTER TABLE dbo.TrainingSessions ADD BatchesJson NVARCHAR(MAX) NULL;
        IF COL_LENGTH('dbo.TrainingSessions','AssessmentsJson') IS NULL ALTER TABLE dbo.TrainingSessions ADD AssessmentsJson NVARCHAR(MAX) NULL;
        IF COL_LENGTH('dbo.TrainingSessions','BatchSchedulesJson') IS NULL ALTER TABLE dbo.TrainingSessions ADD BatchSchedulesJson NVARCHAR(MAX) NULL;
        IF COL_LENGTH('dbo.TrainingSessions','BatchAccessControlJson') IS NULL ALTER TABLE dbo.TrainingSessions ADD BatchAccessControlJson NVARCHAR(MAX) NULL;");
        await ExecuteNonQueryAsync(@"
        IF NOT EXISTS (SELECT 1 FROM dbo.TrainingSessions)
        BEGIN
            DECLARE @dsaTrainingId INT = ISNULL((SELECT TOP 1 TrainingId FROM dbo.TrainingPrograms WHERE TrainingType LIKE '%Data Structures%'), 1);
            INSERT INTO dbo.TrainingSessions (SessionId, TrainingId, SessionName, SessionDate, StartTime, EndTime, Venue, Status, BatchesJson, AssessmentsJson, BatchSchedulesJson, BatchAccessControlJson)
            VALUES (
                'DSA-S1',
                @dsaTrainingId,
                'Session 1: OOPS Essentials & Overview of Data Structures',
                CAST(GETDATE() AS date),
                '09:00',
                '17:00',
                'Main Lab',
                'Open',
                '[""2026-CSE-A"",""2026-CSE-B"",""2026-ISE-A""]',
                '[""Programming Exercise 1"",""Descriptive Assignment 1"",""Session Quiz 1""]',
                '{}',
                '{}'
            );
        END;");
    }

    public async Task<List<PortalSessionDto>> GetPortalSessionsAsync()
    {
        await EnsurePortalSessionColumnsAsync();
        return await QueryAsync(@"
            SELECT ts.SessionId, ts.SessionName, ISNULL(tp.TrainingType, 'Data Structures and Algorithms') AS TrainingType, ts.Status,
                   ISNULL(ts.BatchesJson,'[]') BatchesJson, ISNULL(ts.AssessmentsJson,'[]') AssessmentsJson,
                   ISNULL(ts.BatchSchedulesJson,'{}') BatchSchedulesJson, ISNULL(ts.BatchAccessControlJson,'{}') BatchAccessControlJson
            FROM dbo.TrainingSessions ts
            LEFT JOIN dbo.TrainingPrograms tp ON tp.TrainingId = ts.TrainingId
            ORDER BY ts.SessionId", reader => new PortalSessionDto(
                reader.GetString("SessionId"), reader.GetString("SessionName"), reader.GetString("TrainingType"), reader.GetString("Status"),
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString("BatchesJson")) ?? [],
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString("AssessmentsJson")) ?? [],
                reader.GetString("BatchSchedulesJson"), reader.GetString("BatchAccessControlJson")));
    }

    public async Task SavePortalSessionAsync(SavePortalSessionDto dto)
    {
        await EnsurePortalSessionColumnsAsync();
        var trainingIdObj = await ExecuteScalarAsync("SELECT TOP 1 TrainingId FROM TrainingPrograms WHERE TrainingType=@program ORDER BY TrainingId",
            new SqlParameter("@program", dto.TrainingProgram));
        int trainingId = (trainingIdObj is null || trainingIdObj is DBNull) ? 1 : Convert.ToInt32(trainingIdObj);
        var status = dto.Status is "Open" or "Closed" ? dto.Status : "Closed";
        await ExecuteNonQueryAsync(@"
            IF EXISTS(SELECT 1 FROM TrainingSessions WHERE SessionId=@id)
                UPDATE TrainingSessions SET TrainingId=@trainingId,SessionName=@name,Status=@status,BatchesJson=@batches,
                    AssessmentsJson=@assessments,BatchSchedulesJson=@schedules,BatchAccessControlJson=@access WHERE SessionId=@id;
            ELSE
                INSERT INTO TrainingSessions(SessionId,TrainingId,SessionName,SessionDate,StartTime,EndTime,Venue,Status,
                    BatchesJson,AssessmentsJson,BatchSchedulesJson,BatchAccessControlJson)
                VALUES(@id,@trainingId,@name,CAST(GETDATE() AS date),'00:00','00:00','',@status,@batches,@assessments,@schedules,@access);",
            new SqlParameter("@id", dto.SessionId), new SqlParameter("@trainingId", trainingId),
            new SqlParameter("@name", dto.SessionName), new SqlParameter("@status", status),
            new SqlParameter("@batches", System.Text.Json.JsonSerializer.Serialize(dto.Batches ?? [])),
            new SqlParameter("@assessments", System.Text.Json.JsonSerializer.Serialize(dto.Assessments ?? [])),
            new SqlParameter("@schedules", dto.BatchSchedulesJson ?? "{}"), new SqlParameter("@access", dto.BatchAccessControlJson ?? "{}"));
    }

    public async Task<bool> DeletePortalSessionAsync(string sessionId)
    {
        await EnsurePortalSessionColumnsAsync();
        return await ExecuteNonQueryAsync("DELETE FROM TrainingSessions WHERE SessionId=@id", new SqlParameter("@id", sessionId)) > 0;
    }

    public async Task<int> CreateStudentAsync(SaveStudentDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var usn = dto.Usn.Trim();
            var effectiveEmail = !string.IsNullOrWhiteSpace(dto.EmailId)
                ? dto.EmailId.Trim().ToLowerInvariant()
                : $"{usn.ToLowerInvariant()}@pending.local";

            const string accountSql = @"
                DECLARE @existingAccountId varchar(30) = (
                    SELECT TOP 1 AccountId FROM Accounts
                    WHERE (LOWER(Email) = LOWER(@email) AND Email <> '')
                       OR (AccountId IN (SELECT AccountId FROM Students WHERE LOWER(USN) = LOWER(@usn)))
                );
                IF @existingAccountId IS NOT NULL
                BEGIN
                    UPDATE Accounts SET Name = @name, DepartmentOrBatch = @department, ContactNo = @contact, Status = @status WHERE AccountId = @existingAccountId;
                    SELECT @existingAccountId;
                END
                ELSE
                BEGIN
                    DECLARE @nextId int = ISNULL((SELECT MAX(TRY_CONVERT(int, SUBSTRING(AccountId, 4, 20))) FROM Accounts WHERE AccountId LIKE 'STD%'), 0) + 1;
                    DECLARE @accountId varchar(30) = 'STD' + RIGHT('000000' + CONVERT(varchar(20), @nextId), 6);
                    INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
                    VALUES (@accountId, 4, @name, @department, @email, @contact, @status, @passwordHash, SYSUTCDATETIME());
                    SELECT @accountId;
                END";

            await using var accountCommand = new SqlCommand(accountSql, connection, transaction);
            accountCommand.Parameters.AddRange(new[] {
                new SqlParameter("@email", effectiveEmail),
                new SqlParameter("@usn", usn),
                new SqlParameter("@name", dto.Name.Trim()),
                new SqlParameter("@department", $"Student - {dto.CurrentSemester} Semester"),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                new SqlParameter("@status", string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status),
                new SqlParameter("@passwordHash", PasswordSecurity.Hash(usn))
            });
            var accountId = Convert.ToString(await accountCommand.ExecuteScalarAsync())!;

            const string studentSql = @"
                IF EXISTS (SELECT 1 FROM Students WHERE LOWER(USN) = LOWER(@usn))
                BEGIN
                    UPDATE Students
                    SET AccountId = @accountId, Name = @name, CurrentSemester = @sem, EmailId = @email, ContactNo = @contact, Status = @status
                    WHERE LOWER(USN) = LOWER(@usn);
                    SELECT StudentId FROM Students WHERE LOWER(USN) = LOWER(@usn);
                END
                ELSE
                BEGIN
                    INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
                    VALUES (@accountId, @usn, @name, @sem, @email, @contact, @status);
                    SELECT SCOPE_IDENTITY();
                END";

            await using var studentCommand = new SqlCommand(studentSql, connection, transaction);
            studentCommand.Parameters.AddRange(new[] {
                new SqlParameter("@accountId", accountId),
                new SqlParameter("@usn", usn),
                new SqlParameter("@name", dto.Name.Trim()),
                new SqlParameter("@sem", dto.CurrentSemester ?? string.Empty),
                new SqlParameter("@email", effectiveEmail),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
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
                   s.StudentId, s.USN, s.CurrentSemester, s.EmailId AS StudentEmail
            FROM Accounts a
            INNER JOIN Roles r ON r.RoleId = a.RoleId
            LEFT JOIN Students s ON s.AccountId = a.AccountId
            WHERE (LOWER(s.USN) = LOWER(@username) OR LOWER(a.AccountId) = LOWER(@username) OR (a.Email <> '' AND LOWER(a.Email) = LOWER(@username)) OR (s.EmailId <> '' AND LOWER(s.EmailId) = LOWER(@username)))
              AND LOWER(r.RoleName) = LOWER(@role) AND a.Status IN ('Active', 'Pending')";
        var rows = await QueryAsync(sql, reader => new {
            AccountId = reader.GetString("AccountId"), Role = reader.GetString("RoleName"), Name = reader.GetString("Name"),
            Email = reader.GetString("Email"), Department = reader.GetString("DepartmentOrBatch"), Hash = reader.GetNullableString("PasswordHash"),
            StudentId = reader["StudentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["StudentId"]),
            Usn = reader.GetNullableString("USN"), Semester = reader.GetNullableString("CurrentSemester"),
            StudentEmail = reader.GetNullableString("StudentEmail")
        }, new SqlParameter("@username", dto.Username.Trim()), new SqlParameter("@role", dto.Role.Trim()));
        var row = rows.FirstOrDefault();
        if (row is null) return null;

        var valid = PasswordSecurity.Verify(dto.Password, row.Hash);
        var isLegacyPlainTextPassword = !valid
            && !string.IsNullOrWhiteSpace(row.Hash)
            && !row.Hash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal)
            && string.Equals(dto.Password, row.Hash, StringComparison.Ordinal);

        if (isLegacyPlainTextPassword)
        {
            await ExecuteNonQueryAsync("UPDATE Accounts SET PasswordHash = @hash, LastLoginAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE AccountId = @id",
                new SqlParameter("@hash", PasswordSecurity.Hash(dto.Password)), new SqlParameter("@id", row.AccountId));
            valid = true;
        }
        else if (valid)
        {
            await ExecuteNonQueryAsync("UPDATE Accounts SET LastLoginAt = SYSUTCDATETIME() WHERE AccountId = @id", new SqlParameter("@id", row.AccountId));
        }

        if (!valid) return null;

        await ExecuteNonQueryAsync(@"
            INSERT INTO LoginHistory(AccountId, LoggedInAt, LoggedOutAt, StayOnPortalMinutes)
            VALUES(@id, SYSUTCDATETIME(), DATEADD(minute, 35, SYSUTCDATETIME()), 35)",
            new SqlParameter("@id", row.AccountId));

        var effectiveEmail = !string.IsNullOrWhiteSpace(row.StudentEmail) && !row.StudentEmail.EndsWith("@pending.local") ? row.StudentEmail

            : (!string.IsNullOrWhiteSpace(row.Email) && !row.Email.EndsWith("@pending.local") ? row.Email : "");

        var isEmailPending = string.IsNullOrWhiteSpace(effectiveEmail) || !effectiveEmail.Contains("@");
        var isPasswordDefault = PasswordSecurity.Verify("student123", row.Hash) || (row.Usn != null && PasswordSecurity.Verify(row.Usn, row.Hash)) || string.IsNullOrWhiteSpace(row.Hash);
        var mustUpdateProfile = row.Role.Equals("Student", StringComparison.OrdinalIgnoreCase) && (isEmailPending || isPasswordDefault);

        return new AuthenticatedUserDto(row.AccountId, row.Role, row.StudentId, row.Name, effectiveEmail, row.Usn, row.Semester, row.Department, mustUpdateProfile);
    }

    private async Task EnsureStudentAccountAsync(string username)
    {
        const string findSql = @"
            SELECT TOP 1 StudentId, USN, Name, CurrentSemester, EmailId, ContactNo, Status
            FROM Students WHERE AccountId IS NULL AND (LOWER(USN) = LOWER(@username) OR LOWER(EmailId) = LOWER(@username))";
        var students = await QueryAsync(findSql, r => new {
            Id = r.GetInt32("StudentId"), Usn = r.GetString("USN"), Name = r.GetString("Name"), Semester = r.GetString("CurrentSemester"),
            Email = r.GetString("EmailId"), Contact = r.GetString("ContactNo"), Status = r.GetString("Status")
        }, new SqlParameter("@username", username.Trim()));
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
            var effectiveEmail = !string.IsNullOrWhiteSpace(student.Email) ? student.Email.ToLowerInvariant() : $"{student.Usn.ToLowerInvariant()}@pending.local";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddRange(new[] {
                new SqlParameter("@studentId", student.Id), new SqlParameter("@name", student.Name),
                new SqlParameter("@department", $"Student - {student.Semester} Semester"), new SqlParameter("@email", effectiveEmail),
                new SqlParameter("@contact", student.Contact), new SqlParameter("@status", student.Status),
                new SqlParameter("@hash", PasswordSecurity.Hash("student123"))
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

    public async Task<bool> CompleteStudentProfileAsync(CompleteStudentProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AccountId) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return false;

        var email = dto.Email.Trim().ToLowerInvariant();
        var hash = PasswordSecurity.Hash(dto.NewPassword.Trim());

        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var duplicate = new SqlCommand(
                "SELECT COUNT(1) FROM Accounts WHERE LOWER(Email)=LOWER(@email) AND AccountId<>@id", connection, (SqlTransaction)transaction);
            duplicate.Parameters.AddWithValue("@email", email);
            duplicate.Parameters.AddWithValue("@id", dto.AccountId);
            if (Convert.ToInt32(await duplicate.ExecuteScalarAsync()) > 0)
            {
                await transaction.RollbackAsync();
                return false;
            }
            await using var account = new SqlCommand(@"UPDATE Accounts SET Email=@email,PasswordHash=@hash,UpdatedAt=SYSUTCDATETIME()
                WHERE AccountId=@id AND RoleId=(SELECT RoleId FROM Roles WHERE RoleName='Student')", connection, (SqlTransaction)transaction);
            account.Parameters.AddWithValue("@email", email);
            account.Parameters.AddWithValue("@hash", hash);
            account.Parameters.AddWithValue("@id", dto.AccountId);
            var accountRows = await account.ExecuteNonQueryAsync();

            await using var student = new SqlCommand("UPDATE Students SET EmailId=@email WHERE AccountId=@id", connection, (SqlTransaction)transaction);
            student.Parameters.AddWithValue("@email", email);
            student.Parameters.AddWithValue("@id", dto.AccountId);
            var studentRows = await student.ExecuteNonQueryAsync();
            if (accountRows != 1 || studentRows != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ResetPasswordByAdminAsync(string accountId, string newPassword) =>
        await ExecuteNonQueryAsync(
            "UPDATE Accounts SET PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME() WHERE AccountId = @id",
            new SqlParameter("@hash", PasswordSecurity.Hash(newPassword)),
            new SqlParameter("@id", accountId)) > 0;

    public async Task<bool> UserExistsByEmailAsync(string email)
    {
        var e = email.Trim().ToLowerInvariant();
        var accountsCount = Convert.ToInt32(await ExecuteScalarAsync(
            "SELECT COUNT(1) FROM Accounts WHERE LOWER(Email) = LOWER(@email)",
            new SqlParameter("@email", e)));
        if (accountsCount > 0) return true;

        var studentsCount = Convert.ToInt32(await ExecuteScalarAsync(
            "SELECT COUNT(1) FROM Students WHERE LOWER(EmailId) = LOWER(@email)",
            new SqlParameter("@email", e)));
        return studentsCount > 0;
    }

    public async Task<bool> ResetPasswordByEmailAsync(string email, string newPassword)
    {
        var e = email.Trim().ToLowerInvariant();
        await EnsureStudentAccountAsync(e);
        var hash = PasswordSecurity.Hash(newPassword);
        var updated = await ExecuteNonQueryAsync(
            "UPDATE Accounts SET PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME() WHERE LOWER(Email) = LOWER(@email)",
            new SqlParameter("@hash", hash), new SqlParameter("@email", e));

        if (updated == 0)
        {
            updated = await ExecuteNonQueryAsync(@"
                UPDATE Accounts SET PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME()
                WHERE AccountId IN (SELECT AccountId FROM Students WHERE LOWER(EmailId) = LOWER(@email))",
                new SqlParameter("@hash", hash), new SqlParameter("@email", e));
        }
        return updated > 0;
    }

    public async Task<string> CreateAccountAsync(CreateAccountDto dto)
    {
        var role = dto.Role.Trim();
        var roleId = role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? 2
            : role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? 3
            : 4;

        var prefix = role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? "HOD"
            : role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? "TRN"
            : "STD";

        var accountId = dto.AccountId;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            var nextId = Convert.ToInt32(await ExecuteScalarAsync(
                $"SELECT ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(AccountId, {prefix.Length + 1}, 20))), 0) + 1 FROM Accounts WHERE AccountId LIKE '{prefix}%'"));
            accountId = $"{prefix}{nextId:D3}";
        }

        string defaultPass;
        if (role.Equals("HOD", StringComparison.OrdinalIgnoreCase))
        {
            defaultPass = string.IsNullOrWhiteSpace(dto.Password) ? "HOD" : dto.Password;
        }
        else if (role.Equals("Trainer", StringComparison.OrdinalIgnoreCase))
        {
            defaultPass = string.IsNullOrWhiteSpace(dto.Password) ? "TRAINER" : dto.Password;
        }
        else if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            defaultPass = string.IsNullOrWhiteSpace(dto.Password) ? (!string.IsNullOrWhiteSpace(dto.Usn) ? dto.Usn.Trim().ToUpperInvariant() : "STUDENT") : dto.Password;
        }
        else
        {
            defaultPass = string.IsNullOrWhiteSpace(dto.Password) ? "Atme@1234" : dto.Password;
        }

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

        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dto.Usn))
        {
            var usn = dto.Usn.Trim().ToUpperInvariant();
            var exists = Convert.ToInt32(await ExecuteScalarAsync(
                "SELECT COUNT(1) FROM Students WHERE LOWER(USN) = LOWER(@usn)",
                new SqlParameter("@usn", usn))) > 0;

            if (!exists)
            {
                const string studentSql = @"
                    INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
                    VALUES (@accountId, @usn, @name, @sem, @email, @contact, 'Active');";
                await ExecuteNonQueryAsync(studentSql,
                    new SqlParameter("@accountId", accountId),
                    new SqlParameter("@usn", usn),
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@sem", dto.CurrentSemester ?? "1st Semester"),
                    new SqlParameter("@email", dto.Email.Trim().ToLowerInvariant()),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty));
            }
            else
            {
                await ExecuteNonQueryAsync(@"
                    UPDATE Students SET AccountId = @accountId, Name = @name, CurrentSemester = @sem, EmailId = @email, ContactNo = @contact
                    WHERE LOWER(USN) = LOWER(@usn)",
                    new SqlParameter("@accountId", accountId),
                    new SqlParameter("@usn", usn),
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@sem", dto.CurrentSemester ?? "1st Semester"),
                    new SqlParameter("@email", dto.Email.Trim().ToLowerInvariant()),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty));
            }
        }

        if (role.Equals("Trainer", StringComparison.OrdinalIgnoreCase))
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var trainerExists = Convert.ToInt32(await ExecuteScalarAsync(
                "SELECT COUNT(1) FROM Trainers WHERE LOWER(EmailId) = LOWER(@email)",
                new SqlParameter("@email", email))) > 0;

            if (!trainerExists)
            {
                const string trainerSql = @"
                    INSERT INTO Trainers (Name, Qualification, Designation, TeachingExperience, IndustryExperience, TotalExperience, EmailId, ContactNo, IsActive)
                    VALUES (@name, @qual, @desig, @teachExp, @indExp, @totExp, @email, @contact, 1);";
                await ExecuteNonQueryAsync(trainerSql,
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@qual", dto.Qualification ?? string.Empty),
                    new SqlParameter("@desig", dto.Designation ?? string.Empty),
                    new SqlParameter("@teachExp", dto.TeachingExperience ?? string.Empty),
                    new SqlParameter("@indExp", dto.IndustryExperience ?? string.Empty),
                    new SqlParameter("@totExp", dto.TotalExperience ?? string.Empty),
                    new SqlParameter("@email", email),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty));
            }
            else
            {
                await ExecuteNonQueryAsync(@"
                    UPDATE Trainers SET Name = @name, Qualification = @qual, Designation = @desig, TeachingExperience = @teachExp, IndustryExperience = @indExp, TotalExperience = @totExp, ContactNo = @contact
                    WHERE LOWER(EmailId) = LOWER(@email)",
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@qual", dto.Qualification ?? string.Empty),
                    new SqlParameter("@desig", dto.Designation ?? string.Empty),
                    new SqlParameter("@teachExp", dto.TeachingExperience ?? string.Empty),
                    new SqlParameter("@indExp", dto.IndustryExperience ?? string.Empty),
                    new SqlParameter("@totExp", dto.TotalExperience ?? string.Empty),
                    new SqlParameter("@email", email),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty));
            }
        }

        return accountId;
    }

    public async Task<bool> UpdateAccountFullAsync(UpdateAccountFullDto dto)
    {
        var role = dto.Role.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();

        var existingAccounts = await QueryAsync("SELECT Email, Name FROM Accounts WHERE LOWER(AccountId) = LOWER(@id)",
            r => new { Email = r.GetNullableString("Email") ?? string.Empty, Name = r.GetNullableString("Name") ?? string.Empty },
            new SqlParameter("@id", dto.AccountId));
        var oldEmail = existingAccounts.FirstOrDefault()?.Email.ToLowerInvariant() ?? string.Empty;
        var oldName = existingAccounts.FirstOrDefault()?.Name ?? string.Empty;

        string sql;
        int updated;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            var hash = PasswordSecurity.Hash(dto.Password);
            sql = @"
                UPDATE Accounts
                SET Name = @name, DepartmentOrBatch = @dept, Email = @email, ContactNo = @contact, Status = @status, PasswordHash = @hash, UpdatedAt = SYSUTCDATETIME()
                WHERE LOWER(AccountId) = LOWER(@accountId) OR (Email <> '' AND LOWER(Email) = LOWER(@email))";
            updated = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@accountId", dto.AccountId),
                new SqlParameter("@name", dto.Name),
                new SqlParameter("@dept", dto.DepartmentOrBatch ?? string.Empty),
                new SqlParameter("@email", email),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                new SqlParameter("@status", dto.Status ?? "Active"),
                new SqlParameter("@hash", hash));
        }
        else
        {
            sql = @"
                UPDATE Accounts
                SET Name = @name, DepartmentOrBatch = @dept, Email = @email, ContactNo = @contact, Status = @status, UpdatedAt = SYSUTCDATETIME()
                WHERE LOWER(AccountId) = LOWER(@accountId) OR (Email <> '' AND LOWER(Email) = LOWER(@email))";
            updated = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@accountId", dto.AccountId),
                new SqlParameter("@name", dto.Name),
                new SqlParameter("@dept", dto.DepartmentOrBatch ?? string.Empty),
                new SqlParameter("@email", email),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                new SqlParameter("@status", dto.Status ?? "Active"));
        }

        if (updated == 0)
        {
            var roleId = role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? 2
                : role.Equals("Trainer", StringComparison.OrdinalIgnoreCase) ? 3
                : 4;
            var defaultPass = !string.IsNullOrWhiteSpace(dto.Password) ? dto.Password : "Atme@1234";
            var hash = PasswordSecurity.Hash(defaultPass);

            await ExecuteNonQueryAsync(@"
                INSERT INTO Accounts (AccountId, RoleId, Name, DepartmentOrBatch, Email, ContactNo, Status, PasswordHash, CreatedAt)
                VALUES (@accountId, @roleId, @name, @dept, @email, @contact, @status, @hash, SYSUTCDATETIME());",
                new SqlParameter("@accountId", dto.AccountId),
                new SqlParameter("@roleId", roleId),
                new SqlParameter("@name", dto.Name),
                new SqlParameter("@dept", dto.DepartmentOrBatch ?? string.Empty),
                new SqlParameter("@email", email),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                new SqlParameter("@status", dto.Status ?? "Active"),
                new SqlParameter("@hash", hash));

            updated = 1;
        }

        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            var studentStatus = dto.Status.Equals("Detained", StringComparison.OrdinalIgnoreCase) ? "Detained"
                : dto.Status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase) ? "Discontinued"
                : "Active";

            var studentRows = await ExecuteNonQueryAsync(@"
                UPDATE Students
                SET Name = @name, CurrentSemester = @sem, EmailId = @email, ContactNo = @contact, Status = @status
                WHERE LOWER(AccountId) = LOWER(@accountId) OR (USN <> '' AND LOWER(USN) = LOWER(@usn))",
                new SqlParameter("@accountId", dto.AccountId),
                new SqlParameter("@usn", dto.Usn ?? string.Empty),
                new SqlParameter("@name", dto.Name),
                new SqlParameter("@sem", dto.CurrentSemester ?? "1st Semester"),
                new SqlParameter("@email", email),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                new SqlParameter("@status", studentStatus));

            if (studentRows == 0 && !string.IsNullOrWhiteSpace(dto.Usn))
            {
                await ExecuteNonQueryAsync(@"
                    INSERT INTO Students (AccountId, USN, Name, CurrentSemester, EmailId, ContactNo, Status)
                    VALUES (@accountId, @usn, @name, @sem, @email, @contact, @status);",
                    new SqlParameter("@accountId", dto.AccountId),
                    new SqlParameter("@usn", dto.Usn.Trim().ToUpperInvariant()),
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@sem", dto.CurrentSemester ?? "1st Semester"),
                    new SqlParameter("@email", email),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty),
                    new SqlParameter("@status", studentStatus));
            }
        }
        else if (role.Equals("Trainer", StringComparison.OrdinalIgnoreCase))
        {
            var trainerRows = await ExecuteNonQueryAsync(@"
                UPDATE Trainers
                SET Name = @name, Qualification = @qual, Designation = @desig, TeachingExperience = @teachExp, IndustryExperience = @indExp, TotalExperience = @totExp, ContactNo = @contact, EmailId = @email
                WHERE (EmailId <> '' AND (LOWER(EmailId) = LOWER(@email) OR LOWER(EmailId) = LOWER(@oldEmail))) OR Name = @name OR Name = @oldName",
                new SqlParameter("@name", dto.Name),
                new SqlParameter("@oldName", oldName),
                new SqlParameter("@qual", dto.Qualification ?? string.Empty),
                new SqlParameter("@desig", dto.Designation ?? string.Empty),
                new SqlParameter("@teachExp", dto.TeachingExperience ?? string.Empty),
                new SqlParameter("@indExp", dto.IndustryExperience ?? string.Empty),
                new SqlParameter("@totExp", dto.TotalExperience ?? string.Empty),
                new SqlParameter("@email", email),
                new SqlParameter("@oldEmail", oldEmail),
                new SqlParameter("@contact", dto.ContactNo ?? string.Empty));

            if (trainerRows == 0)
            {
                await ExecuteNonQueryAsync(@"
                    INSERT INTO Trainers (Name, Qualification, Designation, TeachingExperience, IndustryExperience, TotalExperience, EmailId, ContactNo, IsActive)
                    VALUES (@name, @qual, @desig, @teachExp, @indExp, @totExp, @email, @contact, 1);",
                    new SqlParameter("@name", dto.Name),
                    new SqlParameter("@qual", dto.Qualification ?? string.Empty),
                    new SqlParameter("@desig", dto.Designation ?? string.Empty),
                    new SqlParameter("@teachExp", dto.TeachingExperience ?? string.Empty),
                    new SqlParameter("@indExp", dto.IndustryExperience ?? string.Empty),
                    new SqlParameter("@totExp", dto.TotalExperience ?? string.Empty),
                    new SqlParameter("@email", email),
                    new SqlParameter("@contact", dto.ContactNo ?? string.Empty));
            }
        }

        return true;
    }

    public async Task<bool> DeleteAccountAsync(string accountId)
    {
        var email = await QueryAsync("SELECT Email FROM Accounts WHERE LOWER(AccountId) = LOWER(@id)", r => r.GetNullableString("Email"), new SqlParameter("@id", accountId));
        var userEmail = email.FirstOrDefault() ?? string.Empty;

        await ExecuteNonQueryAsync("DELETE FROM Students WHERE LOWER(AccountId) = LOWER(@id)", new SqlParameter("@id", accountId));
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            await ExecuteNonQueryAsync("DELETE FROM Trainers WHERE LOWER(EmailId) = LOWER(@email)", new SqlParameter("@email", userEmail));
        }

        var rows = await ExecuteNonQueryAsync("DELETE FROM Accounts WHERE LOWER(AccountId) = LOWER(@id)", new SqlParameter("@id", accountId));
        return rows > 0;
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

    private Task<int> EnsureSessionAttendanceTableAsync() => ExecuteNonQueryAsync(@"
        IF OBJECT_ID('dbo.SessionAttendance', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SessionAttendance (
                AttendanceId BIGINT IDENTITY(1,1) PRIMARY KEY,
                SessionId NVARCHAR(50) NOT NULL,
                Batch NVARCHAR(50) NOT NULL,
                StudentId INT NOT NULL,
                Present BIT NOT NULL,
                Absent BIT NOT NULL,
                Remarks NVARCHAR(500) NULL,
                RecordedAt DATETIME2 NOT NULL
            );
        END");

    public async Task<SaveSessionAttendanceDto?> GetSessionAttendanceAsync(string sessionId, string batch)
    {
        await EnsureSessionAttendanceTableAsync();
        var rows = await QueryAsync(@"
            SELECT SessionId, Batch, StudentId, Present, Absent, Remarks, RecordedAt
            FROM SessionAttendance
            WHERE SessionId = @sessionId AND Batch = @batch",
            r => new {
                SessionId = r.GetString("SessionId"),
                Batch = r.GetString("Batch"),
                StudentId = r.GetInt32("StudentId"),
                Present = r.GetBoolean("Present"),
                Absent = r.GetBoolean("Absent"),
                Remarks = r.GetNullableString("Remarks"),
                RecordedAt = r.GetDateTime("RecordedAt")
            },
            new SqlParameter("@sessionId", sessionId),
            new SqlParameter("@batch", batch ?? ""));

        if (rows.Count == 0) return null;

        var records = rows.Select(r => new SessionAttendanceRecordDto(r.StudentId, r.Present, r.Absent, r.Remarks)).ToList();
        var recTime = rows.Max(r => r.RecordedAt).ToString("o");
        return new SaveSessionAttendanceDto(sessionId, batch ?? "", recTime, records);
    }

    public async Task SaveSessionAttendanceAsync(SaveSessionAttendanceDto dto)
    {
        await EnsureSessionAttendanceTableAsync();
        var recAt = DateTime.TryParse(dto.RecordedAt, out var dt) ? dt : DateTime.UtcNow;
        await using var connection = new SqlConnection(_connectionString);
        await OpenConnectionWithRetryAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var delete = new SqlCommand(
                "DELETE FROM SessionAttendance WHERE SessionId=@sessionId AND Batch=@batch", connection, (SqlTransaction)transaction))
            {
                delete.Parameters.AddWithValue("@sessionId", dto.SessionId);
                delete.Parameters.AddWithValue("@batch", dto.Batch);
                await delete.ExecuteNonQueryAsync();
            }
            foreach (var rec in dto.Records)
            {
                await using var insert = new SqlCommand(@"INSERT INTO SessionAttendance
                    (SessionId,Batch,StudentId,Present,Absent,Remarks,RecordedAt)
                    VALUES(@sessionId,@batch,@studentId,@present,@absent,@remarks,@recordedAt)",
                    connection, (SqlTransaction)transaction);
                insert.Parameters.AddWithValue("@sessionId", dto.SessionId);
                insert.Parameters.AddWithValue("@batch", dto.Batch);
                insert.Parameters.AddWithValue("@studentId", rec.StudentId);
                insert.Parameters.AddWithValue("@present", rec.Present);
                insert.Parameters.AddWithValue("@absent", rec.Absent);
                insert.Parameters.AddWithValue("@remarks", (object?)rec.Remarks ?? DBNull.Value);
                insert.Parameters.AddWithValue("@recordedAt", recAt);
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
