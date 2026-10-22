namespace TrainingAndAssessmentWebAPI.Models;

public record EventLoginRequest(string Username, string Password);
public record EventLoginResponse(string Token, string Role, string DisplayName, bool MustChangePassword, DateTime ExpiresAt);
public record ExternalEventRequest(string EventName, string HostingInstitution, string Venue, string EventPortal, string Category, string EventType, string RegistrationMode, int MinParticipants, int MaxParticipants, string StudentMessage, DateTime? MessageSharedAt, DateTime? EventFrom, DateTime? EventTo, DateTime? RegistrationDeadline);
public record EventMemberRequest(string Name, string Usn, string Semester, string Branch, string Mobile, string Email, bool IsLeader);
public record EventRegistrationRequest(int EventId, string RegistrationMode, string Category, string Mentor, List<EventMemberRequest> Members, bool DeclarationAccepted);
public record TeamParticipationRequest(string PostEventStatus, DateTime? FromDate, DateTime? ToDate, string PrizeDetails, string NonParticipationReason, string ExperienceFeedback, bool DeclarationAccepted, List<EventMemberRequest> Members);
public record TeamDetailsUpdateRequest(List<EventMemberRequest> Members);
public record PasswordChangeRequest(string CurrentPassword, string NewPassword);
public record TeamReviewRequest(string Status, string Remarks);
public record CoordinatorTeamUpdateRequest(int EventId, string Category, string Mentor, string ApprovalStatus, string Status, string Remarks, List<EventMemberRequest> Members);
public record CoordinatorPostEventUpdateRequest(string PostEventStatus, DateTime? FromDate, DateTime? ToDate, string PrizeDetails, string NonParticipationReason);
public record EventFreezeRequest(bool IsFrozen);
public record WinningTeamRequest(string SocialMediaUrl);
