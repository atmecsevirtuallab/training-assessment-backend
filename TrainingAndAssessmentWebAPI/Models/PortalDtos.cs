namespace TrainingAndAssessmentWebAPI.Models;

public sealed record DashboardSummaryDto(
    int TotalTrainings,
    int ActiveTrainings,
    int ClosedTrainings,
    int TotalStudents,
    int ActiveStudents,
    int TotalTrainers,
    int ActiveTrainers,
    int TotalBatches,
    int ActiveBatches,
    int TotalReports,
    int TotalAnnouncements,
    int TotalFeedbacks);

public sealed record AccountDto(
    string AccountId,
    string Role,
    string Name,
    string DepartmentOrBatch,
    string Email,
    string ContactNo,
    string Status,
    DateTime? LastLoginAt);

public sealed record CreateAccountDto(
    string? AccountId,
    string Role,
    string Name,
    string DepartmentOrBatch,
    string Email,
    string ContactNo,
    string? Password = null,
    string? Usn = null,
    string? CurrentSemester = null,
    string? Qualification = null,
    string? Designation = null,
    string? TeachingExperience = null,
    string? IndustryExperience = null,
    string? TotalExperience = null);

public sealed record StudentDto(
    int StudentId,
    string Usn,
    string Name,
    string CurrentSemester,
    string EmailId,
    string ContactNo,
    string Status);

public sealed record TrainerDto(
    int TrainerId,
    string Name,
    string Qualification,
    string Designation,
    string TeachingExperience,
    string IndustryExperience,
    string TotalExperience,
    string EmailId,
    string ContactNo,
    bool IsActive);

public sealed record TrainingProgramDto(
    int TrainingId,
    string TrainingType,
    string Objectives,
    string Outcome,
    string TargetAudience,
    string AcademicYear,
    string Duration,
    string Mode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string Trainers,
    string Batches);

public sealed record BatchDto(
    int BatchId,
    string BatchCode,
    string BatchName,
    string TrainingProgram,
    int Strength,
    bool IsActive,
    string Students);

public sealed record AnnouncementDto(
    string AnnouncementId,
    string Subject,
    string Message,
    string PublishedBy,
    string PublishedTo,
    DateTime PublishedAt);

public sealed record SaveAnnouncementDto(
    string TrainingProgram,
    List<string> Batches,
    string Subject,
    string Message,
    string PublishedBy);

public sealed record FeedbackFormDto(
    string FeedbackId,
    string Program,
    string Batch,
    DateTime PublishedAt,
    DateTime DeadlineAt,
    int Assigned,
    int Submitted,
    int NotSubmitted,
    string Rating,
    string? Suggestions);

public sealed record SaveFeedbackQuestionDto(int QuestionNo, string QuestionText, List<string> Options);
public sealed record SaveFeedbackFormDto(
    string Program,
    string Batch,
    string SessionId,
    DateTime DeadlineAt,
    string? Suggestions,
    List<SaveFeedbackQuestionDto> Questions);
public sealed record AvailableFeedbackDto(
    string FeedbackId,
    string Program,
    string Batch,
    string SessionId,
    string SessionName,
    DateTime PublishedAt,
    DateTime DeadlineAt,
    bool Submitted,
    List<SaveFeedbackQuestionDto> Questions);
public sealed record SubmitFeedbackResponseDto(int QuestionNo, string SelectedOption);
public sealed record SubmitFeedbackDto(int StudentId, string? Suggestions, List<SubmitFeedbackResponseDto> Responses);

public sealed record LoginHistoryDto(
    long LoginHistoryId,
    string AccountType,
    string Name,
    DateTime LastLoggedIn,
    DateTime? LastLoggedOut,
    int StayOnPortalMinutes,
    int TotalStayOnPortalMinutes);

public sealed record ReportDto(
    int ReportId,
    string ReportTitle,
    string Description,
    DateTime UpdatedAt);

public sealed record SaveTrainingProgramDto(
    string TrainingType,
    string Objectives,
    string Outcome,
    string TargetAudience,
    string AcademicYear,
    string Duration,
    string Mode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    List<int>? TrainerIds = null);

public sealed record SaveStudentDto(
    string Usn,
    string Name,
    string CurrentSemester,
    string EmailId,
    string ContactNo,
    string Status);

public sealed record LoginRequestDto(string Role, string Username, string Password);
public sealed record ChangePasswordRequestDto(string AccountId, string CurrentPassword, string NewPassword);
public sealed record AdminResetPasswordRequestDto(string NewPassword);
public sealed record SendOtpRequestDto(string Email);
public sealed record VerifyOtpRequestDto(string Email, string Otp);
public sealed record ResetPasswordOtpRequestDto(string Email, string ResetToken, string NewPassword);
public sealed record CompleteStudentProfileDto(string AccountId, string Email, string NewPassword);
public sealed record AuthenticatedUserDto(
    string AccountId,
    string Role,
    int? StudentId,
    string Name,
    string Email,
    string? Usn,
    string? CurrentSemester,
    string DepartmentOrBatch,
    bool MustUpdateProfile = false);

public sealed record SaveBatchDto(
    string BatchCode,
    string BatchName,
    int TrainingId,
    bool IsActive = true);

public sealed record SessionNotesDto(string SessionId, string ContentHtml, DateTime? UpdatedAt);

public sealed record SaveSessionNotesDto(string ContentHtml);

public sealed record SessionQuizQuestionDto(
    string QuestionLabel,
    string QuestionText,
    List<string> Options,
    int AnswerTimeSeconds,
    int Marks,
    int? CorrectOptionIndex,
    string QuestionType,
    List<int> CorrectOptionIndexes);

public sealed record ProgrammingTestCaseDto(string TestCaseId, string Input, string Output);
public sealed record ProgrammingExerciseDto(string ItemLabel, string Question, List<ProgrammingTestCaseDto> TestCases);
public sealed record ExecuteProgramDto(string Language, string Code, List<ProgrammingTestCaseDto> TestCases);
public sealed record SubmitProgramDto(
    string SessionId,
    string ItemLabel,
    string Language,
    string Code,
    List<ProgrammingTestCaseDto> TestCases,
    int? StudentId = null,
    string? Usn = null,
    string? StudentName = null);
public sealed record StartInteractiveProgramDto(string Language, string Code);
public sealed record InteractiveProgramInputDto(string Input);
public sealed record InteractiveProgramResponseDto(string? SessionId, string Output, string Error, bool IsRunning);
public sealed record ExecutionResultDto(string TestCaseId, string Input, string ExpectedOutput, string ActualOutput, string Status);
public sealed record ProgramExecutionResponseDto(string? CompileError, List<ExecutionResultDto> Results);
