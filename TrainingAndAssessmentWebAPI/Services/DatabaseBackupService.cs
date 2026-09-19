using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TrainingAndAssessmentDataAccessLayer;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class DatabaseBackupService(TrainingAssessmentDbContext db, IWebHostEnvironment environment)
{
    private string BackupDirectory => Path.Combine(environment.ContentRootPath, "App_Data", "Backups");
    private string SqlBakPath => Path.Combine(BackupDirectory, "TrainingAssessmentPortalDB.bak");
    private string JsonSnapshotPath => Path.Combine(BackupDirectory, "database_snapshot.json");

    public async Task<object> GetStatusAsync()
    {
        Directory.CreateDirectory(BackupDirectory);
        var hasBak = File.Exists(SqlBakPath);
        var hasJson = File.Exists(JsonSnapshotPath);

        FileInfo? fileInfo = hasBak ? new FileInfo(SqlBakPath) : (hasJson ? new FileInfo(JsonSnapshotPath) : null);

        var connectionString = db.Database.GetConnectionString() ?? string.Empty;
        var builder = new SqlConnectionStringBuilder(connectionString);

        int totalRecords = 0;
        try
        {
            totalRecords = await db.Accounts.CountAsync()
                + await db.Students.CountAsync()
                + await db.Trainers.CountAsync()
                + await db.TrainingPrograms.CountAsync()
                + await db.Batches.CountAsync()
                + await db.Announcements.CountAsync();
        }
        catch { }

        return new
        {
            backupExists = hasBak || hasJson,
            backupFileName = fileInfo?.Name ?? "None",
            backupFilePath = fileInfo?.FullName ?? string.Empty,
            backupFileSize = fileInfo?.Length ?? 0,
            backupFileSizeBytes = fileInfo?.Length ?? 0,
            lastBackupTime = fileInfo?.LastWriteTime.ToString("dd MMM yyyy, hh:mm tt") ?? "No backup created yet",
            databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "TrainingAssessmentPortalDB" : builder.InitialCatalog,
            serverName = string.IsNullOrWhiteSpace(builder.DataSource) ? "(localdb)\\MSSQLLocalDB" : builder.DataSource,
            totalRecords
        };
    }

    public async Task<object> BackupAsync()
    {
        Directory.CreateDirectory(BackupDirectory);
        var dbName = db.Database.GetDbConnection().Database;
        if (string.IsNullOrWhiteSpace(dbName)) dbName = "TrainingAssessmentPortalDB";

        bool sqlBakSuccess = false;
        string sqlBakMessage = string.Empty;

        try
        {
            // Attempt native SQL BACKUP DATABASE query first
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{SqlBakPath}' WITH FORMAT, INIT, NAME = N'{dbName} Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";
            await db.Database.ExecuteSqlRawAsync(sql);
            sqlBakSuccess = true;
            sqlBakMessage = "Native SQL .bak database backup created successfully.";
        }
        catch (Exception ex)
        {
            sqlBakMessage = $"Native SQL .bak failed ({ex.Message}), falling back to JSON snapshot.";
        }

        // Always generate JSON data snapshot as reliable fallback
        var snapshot = new DatabaseSnapshot
        {
            ExportedAt = DateTime.Now.ToString("o"),
            DatabaseName = dbName,
            Accounts = await db.Accounts.AsNoTracking().ToListAsync(),
            Students = await db.Students.AsNoTracking().ToListAsync(),
            Trainers = await db.Trainers.AsNoTracking().ToListAsync(),
            TrainingPrograms = await db.TrainingPrograms.AsNoTracking().ToListAsync(),
            TrainingProgramTrainers = await db.TrainingProgramTrainers.AsNoTracking().ToListAsync(),
            Batches = await db.Batches.AsNoTracking().ToListAsync(),
            BatchStudents = await db.BatchStudents.AsNoTracking().ToListAsync(),
            Announcements = await db.Announcements.AsNoTracking().ToListAsync(),
            FeedbackForms = await db.FeedbackForms.AsNoTracking().ToListAsync(),
            FeedbackQuestions = await db.FeedbackQuestions.AsNoTracking().ToListAsync(),
            FeedbackResponses = await db.FeedbackResponses.AsNoTracking().ToListAsync(),
            TrainingSessions = await db.TrainingSessions.AsNoTracking().ToListAsync(),
            Assessments = await db.Assessments.AsNoTracking().ToListAsync(),
            Attendance = await db.Attendance.AsNoTracking().ToListAsync(),
            AssessmentSubmissions = await db.AssessmentSubmissions.AsNoTracking().ToListAsync(),
            LoginHistory = await db.LoginHistory.AsNoTracking().ToListAsync()
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(JsonSnapshotPath, json);

        var status = await GetStatusAsync();

        return new
        {
            message = "Database backup created successfully inside project folder!",
            details = sqlBakMessage,
            status
        };
    }

    public async Task<object> RestoreAsync()
    {
        Directory.CreateDirectory(BackupDirectory);
        var dbName = db.Database.GetDbConnection().Database;
        if (string.IsNullOrWhiteSpace(dbName)) dbName = "TrainingAssessmentPortalDB";

        var hasBak = File.Exists(SqlBakPath);
        var hasJson = File.Exists(JsonSnapshotPath);

        if (!hasBak && !hasJson)
        {
            throw new InvalidOperationException("No database backup file was found in the project folder. Create a backup first.");
        }

        bool sqlRestoreSuccess = false;
        string restoreMessage = string.Empty;

        if (hasBak)
        {
            try
            {
                // Attempt SQL RESTORE query
                var sql = $"USE [master]; ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{dbName}] FROM DISK = N'{SqlBakPath}' WITH REPLACE; ALTER DATABASE [{dbName}] SET MULTI_USER;";
                await db.Database.ExecuteSqlRawAsync(sql);
                sqlRestoreSuccess = true;
                restoreMessage = "Database updated and restored successfully from native SQL .bak file!";
            }
            catch (Exception ex)
            {
                restoreMessage = $"Native SQL restore note: {ex.Message}";
            }
        }

        if (!sqlRestoreSuccess && hasJson)
        {
            // Restore from JSON snapshot
            var json = await File.ReadAllTextAsync(JsonSnapshotPath);
            var snapshot = JsonSerializer.Deserialize<DatabaseSnapshot>(json);

            if (snapshot is not null)
            {
                // Clean existing entities in correct dependency order
                db.AssessmentSubmissions.RemoveRange(db.AssessmentSubmissions);
                db.Attendance.RemoveRange(db.Attendance);
                db.Assessments.RemoveRange(db.Assessments);
                db.TrainingSessions.RemoveRange(db.TrainingSessions);
                db.FeedbackResponses.RemoveRange(db.FeedbackResponses);
                db.FeedbackQuestions.RemoveRange(db.FeedbackQuestions);
                db.FeedbackForms.RemoveRange(db.FeedbackForms);
                db.Announcements.RemoveRange(db.Announcements);
                db.BatchStudents.RemoveRange(db.BatchStudents);
                db.Batches.RemoveRange(db.Batches);
                db.TrainingProgramTrainers.RemoveRange(db.TrainingProgramTrainers);
                db.TrainingPrograms.RemoveRange(db.TrainingPrograms);
                db.Trainers.RemoveRange(db.Trainers);
                db.Students.RemoveRange(db.Students);
                db.Accounts.RemoveRange(db.Accounts);
                db.LoginHistory.RemoveRange(db.LoginHistory);
                await db.SaveChangesAsync();

                // Re-add snapshot items
                if (snapshot.Accounts?.Count > 0) db.Accounts.AddRange(snapshot.Accounts);
                if (snapshot.Students?.Count > 0) db.Students.AddRange(snapshot.Students);
                if (snapshot.Trainers?.Count > 0) db.Trainers.AddRange(snapshot.Trainers);
                if (snapshot.TrainingPrograms?.Count > 0) db.TrainingPrograms.AddRange(snapshot.TrainingPrograms);
                await db.SaveChangesAsync();

                if (snapshot.TrainingProgramTrainers?.Count > 0) db.TrainingProgramTrainers.AddRange(snapshot.TrainingProgramTrainers);
                if (snapshot.Batches?.Count > 0) db.Batches.AddRange(snapshot.Batches);
                await db.SaveChangesAsync();

                if (snapshot.BatchStudents?.Count > 0) db.BatchStudents.AddRange(snapshot.BatchStudents);
                if (snapshot.Announcements?.Count > 0) db.Announcements.AddRange(snapshot.Announcements);
                if (snapshot.FeedbackForms?.Count > 0) db.FeedbackForms.AddRange(snapshot.FeedbackForms);
                await db.SaveChangesAsync();

                if (snapshot.FeedbackQuestions?.Count > 0) db.FeedbackQuestions.AddRange(snapshot.FeedbackQuestions);
                if (snapshot.FeedbackResponses?.Count > 0) db.FeedbackResponses.AddRange(snapshot.FeedbackResponses);
                if (snapshot.TrainingSessions?.Count > 0) db.TrainingSessions.AddRange(snapshot.TrainingSessions);
                if (snapshot.Assessments?.Count > 0) db.Assessments.AddRange(snapshot.Assessments);
                if (snapshot.Attendance?.Count > 0) db.Attendance.AddRange(snapshot.Attendance);
                if (snapshot.AssessmentSubmissions?.Count > 0) db.AssessmentSubmissions.AddRange(snapshot.AssessmentSubmissions);
                if (snapshot.LoginHistory?.Count > 0) db.LoginHistory.AddRange(snapshot.LoginHistory);
                await db.SaveChangesAsync();

                restoreMessage = "Database updated and synchronized successfully from project backup file!";
            }
        }

        var status = await GetStatusAsync();

        return new
        {
            message = restoreMessage,
            status
        };
    }
}

public sealed class DatabaseSnapshot
{
    public string ExportedAt { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public List<Account>? Accounts { get; set; }
    public List<Student>? Students { get; set; }
    public List<Trainer>? Trainers { get; set; }
    public List<TrainingProgram>? TrainingPrograms { get; set; }
    public List<TrainingProgramTrainer>? TrainingProgramTrainers { get; set; }
    public List<Batch>? Batches { get; set; }
    public List<BatchStudent>? BatchStudents { get; set; }
    public List<Announcement>? Announcements { get; set; }
    public List<FeedbackForm>? FeedbackForms { get; set; }
    public List<FeedbackQuestion>? FeedbackQuestions { get; set; }
    public List<FeedbackResponse>? FeedbackResponses { get; set; }
    public List<TrainingSession>? TrainingSessions { get; set; }
    public List<Assessment>? Assessments { get; set; }
    public List<Attendance>? Attendance { get; set; }
    public List<AssessmentSubmission>? AssessmentSubmissions { get; set; }
    public List<LoginHistory>? LoginHistory { get; set; }
}
