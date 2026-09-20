using Microsoft.AspNetCore.Mvc;
using TrainingAndAssessmentWebAPI.Models;
using TrainingAndAssessmentWebAPI.Services;

namespace TrainingAndAssessmentWebAPI.Controllers;

[ApiController]
[Route("api/portal")]
public sealed class PortalDataController : ControllerBase
{
    private readonly PortalRepository _repository;
    private readonly OllamaAnalysisService _ollama;
    private readonly EmailOtpService _emailOtp;

    public PortalDataController(PortalRepository repository, OllamaAnalysisService ollama, EmailOtpService emailOtp)
    {
        _repository = repository;
        _ollama = ollama;
        _emailOtp = emailOtp;
    }

    [HttpPost("ai/feedback-analysis")]
    public async Task<IActionResult> AnalyzeFeedback([FromBody] FeedbackAnalysisRequest dto)
    {
        try
        {
            var analysis = await _ollama.AnalyzeFeedbackAsync(dto.SessionName, dto.BatchStrength, dto.Submitted, dto.AverageRating);
            return Ok(new { analysis });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = $"Ollama analysis failed: {ex.Message}" });
        }
    }

    public sealed record FeedbackAnalysisRequest(string SessionName, int BatchStrength, int Submitted, double AverageRating);

    [HttpPost("live-quizzes")]
    public async Task<IActionResult> CreateLiveQuiz([FromBody] CreateLiveQuizDto dto, [FromServices] LiveQuizCoordinator coordinator) =>
        Ok(await coordinator.CreateAsync(dto));

    [HttpGet("live-quizzes/{code}")]
    public IActionResult GetLiveQuiz(string code, [FromServices] LiveQuizCoordinator coordinator)
    {
        var snapshot = coordinator.GetSnapshot(code);
        return snapshot is null ? NotFound(new { message = "Live quiz not found." }) : Ok(snapshot);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await _repository.GetSummaryAsync());

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var user = await _repository.AuthenticateAsync(dto);
        return user is null ? Unauthorized(new { message = "Invalid username, password, or role." }) : Ok(user);
    }

    [HttpPost("auth/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return BadRequest(new { message = "The new password must contain at least 8 characters." });
        var changed = await _repository.ChangePasswordAsync(dto);
        return changed ? Ok(new { message = "Password changed successfully." }) : BadRequest(new { message = "Current password is incorrect." });
    }

    [HttpPost("accounts/{accountId}/reset-password")]
    public async Task<IActionResult> ResetPassword(string accountId, [FromBody] AdminResetPasswordRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 3)
            return BadRequest(new { message = "The temporary password must contain at least 3 characters." });
        var reset = await _repository.ResetPasswordByAdminAsync(accountId, dto.NewPassword);
        return reset
            ? Ok(new { message = "Password reset successfully." })
            : NotFound(new { message = "Account not found." });
    }

    [HttpPost("auth/send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email address is required." });

        var exists = await _repository.UserExistsByEmailAsync(dto.Email);
        if (!exists)
            return NotFound(new { message = "No registered user account found with this email address." });

        var result = await _emailOtp.SendOtpAsync(dto.Email);
        return Ok(new { message = result.Message, demoOtp = result.DemoOtp });
    }

    [HttpPost("auth/verify-otp")]
    public IActionResult VerifyOtp([FromBody] VerifyOtpRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp))
            return BadRequest(new { message = "Email address and OTP code are required." });

        var result = _emailOtp.VerifyOtp(dto.Email, dto.Otp);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message, resetToken = result.ResetToken });
    }

    [HttpPost("auth/reset-password-otp")]
    public async Task<IActionResult> ResetPasswordOtp([FromBody] ResetPasswordOtpRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.ResetToken) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new { message = "Email, reset token, and new password are required." });

        if (dto.NewPassword.Length < 6)
            return BadRequest(new { message = "The new password must be at least 6 characters long." });

        var isValidToken = _emailOtp.ValidateResetToken(dto.Email, dto.ResetToken);
        if (!isValidToken)
            return BadRequest(new { message = "Invalid or expired reset token. Please restart password reset." });

        var reset = await _repository.ResetPasswordByEmailAsync(dto.Email, dto.NewPassword);
        if (!reset)
            return BadRequest(new { message = "Failed to reset password. User account not found." });

        _emailOtp.InvalidateOtp(dto.Email);
        return Ok(new { message = "Password reset successfully. You can now sign in with your new password." });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts() => Ok(await _repository.GetAccountsAsync());

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Role) || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Role, Name, and Email are required." });
        var accountId = await _repository.CreateAccountAsync(dto);
        return Ok(new { accountId, message = $"Account created successfully for {dto.Name} ({accountId}). Default password: {dto.Password ?? "Atme@1234"}" });
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents() => Ok(await _repository.GetStudentsAsync());

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent([FromBody] SaveStudentDto dto)
    {
        var id = await _repository.CreateStudentAsync(dto);
        return Ok(new { studentId = id, message = "Student created successfully. Username: Email ID; initial password: USN." });
    }

    [HttpPost("students/bulk")]
    public async Task<IActionResult> BulkCreateStudents([FromBody] List<SaveStudentDto> dtos)
    {
        var count = await _repository.BulkCreateStudentsAsync(dtos);
        return Ok(new { count, message = $"{count} student(s) imported successfully." });
    }

    [HttpPut("students/{id:int}")]
    public async Task<IActionResult> UpdateStudent(int id, [FromBody] SaveStudentDto dto)
    {
        var updated = await _repository.UpdateStudentAsync(id, dto);
        if (!updated) return NotFound(new { message = "Student not found." });
        return Ok(new { message = "Student updated successfully." });
    }

    [HttpDelete("students/{id:int}")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        var deleted = await _repository.DeleteStudentAsync(id);
        if (!deleted) return NotFound(new { message = "Student not found." });
        return Ok(new { message = "Student deleted successfully." });
    }

    [HttpGet("trainers")]
    public async Task<IActionResult> GetTrainers() => Ok(await _repository.GetTrainersAsync());

    [HttpGet("trainings")]
    public async Task<IActionResult> GetTrainings() => Ok(await _repository.GetTrainingsAsync());

    [HttpPost("trainings")]
    public async Task<IActionResult> CreateTraining([FromBody] SaveTrainingProgramDto dto)
    {
        var id = await _repository.CreateTrainingProgramAsync(dto);
        return Ok(new { trainingId = id, message = "Training created successfully." });
    }

    [HttpPut("trainings/{id:int}")]
    public async Task<IActionResult> UpdateTraining(int id, [FromBody] SaveTrainingProgramDto dto)
    {
        var updated = await _repository.UpdateTrainingProgramAsync(id, dto);
        if (!updated) return NotFound(new { message = "Training not found." });
        return Ok(new { message = "Training updated successfully." });
    }

    [HttpDelete("trainings/{id:int}")]
    public async Task<IActionResult> DeleteTraining(int id)
    {
        var deleted = await _repository.DeleteTrainingProgramAsync(id);
        if (!deleted) return NotFound(new { message = "Training not found." });
        return Ok(new { message = "Training deleted successfully." });
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches() => Ok(await _repository.GetBatchesAsync());

    [HttpPost("batches")]
    public async Task<IActionResult> CreateBatch([FromBody] SaveBatchDto dto)
    {
        var id = await _repository.CreateBatchAsync(dto);
        return Ok(new { batchId = id, message = "Batch created successfully." });
    }

    [HttpPut("batches/{id:int}")]
    public async Task<IActionResult> UpdateBatch(int id, [FromBody] SaveBatchDto dto)
    {
        var updated = await _repository.UpdateBatchAsync(id, dto);
        if (!updated) return NotFound(new { message = "Batch not found." });
        return Ok(new { message = "Batch updated successfully." });
    }

    [HttpDelete("batches/{id:int}")]
    public async Task<IActionResult> DeleteBatch(int id)
    {
        var deleted = await _repository.DeleteBatchAsync(id);
        if (!deleted) return NotFound(new { message = "Batch not found." });
        return Ok(new { message = "Batch deleted successfully." });
    }

    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements() => Ok(await _repository.GetAnnouncementsAsync());

    [HttpPost("announcements")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] SaveAnnouncementDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TrainingProgram) || dto.Batches.Count == 0 || string.IsNullOrWhiteSpace(dto.Subject) || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Training program, at least one batch, subject, and message are required." });
        var id = await _repository.CreateAnnouncementAsync(dto);
        return Ok(new { announcementId = id, message = "Announcement published successfully." });
    }

    [HttpGet("feedbacks")]
    public async Task<IActionResult> GetFeedbacks() => Ok(await _repository.GetFeedbacksAsync());

    [HttpPost("feedbacks")]
    public async Task<IActionResult> CreateFeedback([FromBody] SaveFeedbackFormDto dto)
    {
        var feedbackId = await _repository.CreateFeedbackAsync(dto);
        return Ok(new { feedbackId, message = "Feedback form published successfully." });
    }

    [HttpGet("feedbacks/available")]
    public async Task<IActionResult> GetAvailableFeedbacks([FromQuery] string batch, [FromQuery] int studentId) =>
        Ok(await _repository.GetAvailableFeedbacksAsync(batch, studentId));

    [HttpPost("feedbacks/{feedbackId}/submit")]
    public async Task<IActionResult> SubmitFeedback(string feedbackId, [FromBody] SubmitFeedbackDto dto)
    {
        await _repository.SubmitFeedbackAsync(feedbackId, dto);
        return Ok(new { message = "Feedback submitted successfully." });
    }

    [HttpGet("login-history")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] string? role = null, [FromQuery] string? accountId = null) =>
        Ok(await _repository.GetLoginHistoryAsync(role, accountId));

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports() => Ok(await _repository.GetReportsAsync());

    [HttpGet("sessions/{sessionId}/notes")]
    public async Task<IActionResult> GetSessionNotes(string sessionId) =>
        Ok(await _repository.GetSessionNotesAsync(sessionId));

    [HttpPut("sessions/{sessionId}/notes")]
    public async Task<IActionResult> SaveSessionNotes(string sessionId, [FromBody] SaveSessionNotesDto dto)
    {
        await _repository.SaveSessionNotesAsync(sessionId, dto.ContentHtml ?? string.Empty);
        return Ok(new { message = "Successfully Saved" });
    }

    [HttpGet("sessions/{sessionId}/quizzes/{quizType}")]
    public async Task<IActionResult> GetSessionQuiz(string sessionId, string quizType) =>
        Ok(await _repository.GetSessionQuizAsync(sessionId, quizType));

    [HttpPut("sessions/{sessionId}/quizzes/{quizType}")]
    public async Task<IActionResult> SaveSessionQuiz(
        string sessionId,
        string quizType,
        [FromBody] List<SessionQuizQuestionDto> questions)
    {
        await _repository.SaveSessionQuizAsync(sessionId, quizType, questions);
        return Ok(new { message = "Successfully Saved" });
    }

    [HttpGet("sessions/{sessionId}/programming-exercises/{itemLabel}")]
    public async Task<IActionResult> GetProgrammingExercise(string sessionId, string itemLabel) =>
        Ok(await _repository.GetProgrammingExerciseAsync(sessionId, itemLabel));

    [HttpPut("sessions/{sessionId}/programming-exercises/{itemLabel}")]
    public async Task<IActionResult> SaveProgrammingExercise(string sessionId, string itemLabel, [FromBody] ProgrammingExerciseDto dto)
    {
        await _repository.SaveProgrammingExerciseAsync(sessionId, dto with { ItemLabel = itemLabel });
        return Ok(new { message = "Successfully Saved" });
    }

    [HttpPost("programming/execute")]
    public async Task<IActionResult> ExecuteProgram([FromBody] ExecuteProgramDto dto, [FromServices] ProgramExecutionService executor) =>
        Ok(await executor.ExecuteAsync(dto));

    [HttpPost("programming/interactive/start")]
    public async Task<IActionResult> StartInteractiveProgram(
        [FromBody] StartInteractiveProgramDto dto,
        [FromServices] InteractiveProgramService executor) => Ok(await executor.StartAsync(dto));

    [HttpPost("programming/interactive/{sessionId}/input")]
    public async Task<IActionResult> SendInteractiveInput(
        string sessionId,
        [FromBody] InteractiveProgramInputDto dto,
        [FromServices] InteractiveProgramService executor) => Ok(await executor.SendInputAsync(sessionId, dto.Input));

    [HttpDelete("programming/interactive/{sessionId}")]
    public IActionResult StopInteractiveProgram(string sessionId, [FromServices] InteractiveProgramService executor)
    {
        executor.Stop(sessionId);
        return NoContent();
    }

    [HttpPost("programming/submit")]
    public async Task<IActionResult> SubmitProgram([FromBody] SubmitProgramDto dto, [FromServices] ProgramExecutionService executor)
    {
        var result = await executor.ExecuteAsync(new ExecuteProgramDto(dto.Language, dto.Code, dto.TestCases));
        var submissionId = await _repository.SaveProgrammingSubmissionAsync(dto, result);
        return Ok(new { result.CompileError, result.Results, submissionId });
    }
}
