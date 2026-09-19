using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingAndAssessmentDataAccessLayer
{
    public class Role
    {
        [Key]
        public byte RoleId { get; set; }
        [Required, MaxLength(30)]
        public string RoleName { get; set; } = string.Empty;
    }

    public class Account
    {
        [Key, MaxLength(30)]
        public string AccountId { get; set; } = string.Empty;
        public byte RoleId { get; set; }
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string DepartmentOrBatch { get; set; } = string.Empty;
        [Required, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string ContactNo { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";
        [MaxLength(500)]
        public string? PasswordHash { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Role? Role { get; set; }
    }

    public class Student
    {
        [Key]
        public int StudentId { get; set; }
        [MaxLength(30)]
        public string? AccountId { get; set; }
        [Required, MaxLength(30)]
        public string USN { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string CurrentSemester { get; set; } = string.Empty;
        [Required, MaxLength(256)]
        public string EmailId { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string ContactNo { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";

        public Account? Account { get; set; }
    }

    public class Trainer
    {
        [Key]
        public int TrainerId { get; set; }
        [MaxLength(30)]
        public string? AccountId { get; set; }
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string Qualification { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string Designation { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string TeachingExperience { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string IndustryExperience { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string TotalExperience { get; set; } = string.Empty;
        [Required, MaxLength(256)]
        public string EmailId { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string ContactNo { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public Account? Account { get; set; }
    }

    public class TrainingProgram
    {
        [Key]
        public int TrainingId { get; set; }
        [Required, MaxLength(150)]
        public string TrainingType { get; set; } = string.Empty;
        [Required, MaxLength(1000)]
        public string Objectives { get; set; } = string.Empty;
        [Required, MaxLength(1000)]
        public string Outcome { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string TargetAudience { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string AcademicYear { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string Duration { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Mode { get; set; } = "Offline";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";
    }

    public class TrainingProgramTrainer
    {
        public int TrainingId { get; set; }
        public int TrainerId { get; set; }

        public TrainingProgram? TrainingProgram { get; set; }
        public Trainer? Trainer { get; set; }
    }

    public class Batch
    {
        [Key]
        public int BatchId { get; set; }
        [MaxLength(30)]
        public string? SessionId { get; set; }
        [Required, MaxLength(30)]
        public string BatchCode { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string BatchName { get; set; } = string.Empty;
        public int TrainingId { get; set; }
        public bool IsActive { get; set; } = true;

        public TrainingProgram? TrainingProgram { get; set; }
    }

    public class BatchStudent
    {
        public int BatchId { get; set; }
        public int StudentId { get; set; }

        public Batch? Batch { get; set; }
        public Student? Student { get; set; }
    }

    public class Announcement
    {
        [Key, MaxLength(50)]
        public string AnnouncementId { get; set; } = string.Empty;
        public int? TrainingId { get; set; }
        [Required, MaxLength(300)]
        public string Subject { get; set; } = string.Empty;
        [Required, MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string PublishedBy { get; set; } = string.Empty;
        [Required, MaxLength(300)]
        public string PublishedTo { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        public TrainingProgram? TrainingProgram { get; set; }
    }

    public class FeedbackForm
    {
        [Key, MaxLength(50)]
        public string FeedbackId { get; set; } = string.Empty;
        public int TrainingId { get; set; }
        public int BatchId { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime DeadlineAt { get; set; }
        [MaxLength(1000)]
        public string? Suggestions { get; set; }

        public TrainingProgram? TrainingProgram { get; set; }
        public Batch? Batch { get; set; }
    }

    public class FeedbackQuestion
    {
        [Key]
        public int QuestionId { get; set; }
        [Required, MaxLength(50)]
        public string FeedbackId { get; set; } = string.Empty;
        public int QuestionNo { get; set; }
        [Required, MaxLength(1000)]
        public string QuestionText { get; set; } = string.Empty;
        public string OptionsJson { get; set; } = "[\"Strongly Agree\",\"Agree\",\"Disagree\",\"Strongly Disagree\"]";

        public FeedbackForm? FeedbackForm { get; set; }
    }

    public class FeedbackResponse
    {
        [Key]
        public long ResponseId { get; set; }
        [Required, MaxLength(50)]
        public string FeedbackId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int QuestionNo { get; set; }
        [Required, MaxLength(50)]
        public string SelectedOption { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        [Required, MaxLength(20)]
        public string Attendance { get; set; } = "Present";
        public bool IsConsidered { get; set; } = true;
        [MaxLength(1000)]
        public string? StudentSuggestions { get; set; }

        public FeedbackForm? FeedbackForm { get; set; }
        public Student? Student { get; set; }
    }

    public class TrainingSession
    {
        [Key, MaxLength(30)]
        public string SessionId { get; set; } = string.Empty;
        public int TrainingId { get; set; }
        [Required, MaxLength(200)]
        public string SessionName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        [Required, MaxLength(150)]
        public string Venue { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Open";

        public TrainingProgram? TrainingProgram { get; set; }
    }

    public class Assessment
    {
        [Key, MaxLength(30)]
        public string AssessmentId { get; set; } = string.Empty;
        public int TrainingId { get; set; }
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string AssessmentType { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Open";
        [MaxLength(30)]
        public string? MaxScore { get; set; }
        public DateTime? PublishedAt { get; set; }

        public TrainingProgram? TrainingProgram { get; set; }
    }

    public class Attendance
    {
        [Key]
        public long AttendanceId { get; set; }
        [Required, MaxLength(30)]
        public string SessionId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public bool IsPresent { get; set; }
        [MaxLength(500)]
        public string? Remarks { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public TrainingSession? TrainingSession { get; set; }
        public Student? Student { get; set; }
    }

    public class AssessmentSubmission
    {
        [Key]
        public long SubmissionId { get; set; }
        [Required, MaxLength(30)]
        public string AssessmentId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        [Required, MaxLength(30)]
        public string SubmissionStatus { get; set; } = "Pending";
        public DateTime? SubmittedAt { get; set; }
        [MaxLength(30)]
        public string? Score { get; set; }

        public Assessment? Assessment { get; set; }
        public Student? Student { get; set; }
    }

    public class LoginHistory
    {
        [Key]
        public long LoginHistoryId { get; set; }
        [Required, MaxLength(30)]
        public string AccountId { get; set; } = string.Empty;
        public DateTime LoggedInAt { get; set; } = DateTime.UtcNow;
        public DateTime? LoggedOutAt { get; set; }
        public int StayOnPortalMinutes { get; set; }

        public Account? Account { get; set; }
    }

    public class Report
    {
        [Key]
        public int ReportId { get; set; }
        [Required, MaxLength(200)]
        public string ReportTitle { get; set; } = string.Empty;
        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class VwLoginHistoryWithTotals
    {
        public long LoginHistoryId { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime LastLoggedIn { get; set; }
        public DateTime? LastLoggedOut { get; set; }
        public int StayOnPortalMinutes { get; set; }
        public int TotalStayOnPortalMinutes { get; set; }
    }

    public class VwDashboardSummary
    {
        public int TotalTrainings { get; set; }
        public int ActiveTrainings { get; set; }
        public int ClosedTrainings { get; set; }
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int TotalTrainers { get; set; }
        public int ActiveTrainers { get; set; }
        public int TotalBatches { get; set; }
        public int ActiveBatches { get; set; }
        public int TotalReports { get; set; }
        public int TotalAnnouncements { get; set; }
        public int TotalFeedbacks { get; set; }
    }
}
