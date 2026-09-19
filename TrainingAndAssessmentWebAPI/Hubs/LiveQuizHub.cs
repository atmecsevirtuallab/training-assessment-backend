using Microsoft.AspNetCore.SignalR;
using TrainingAndAssessmentWebAPI.Models;
using TrainingAndAssessmentWebAPI.Services;

namespace TrainingAndAssessmentWebAPI.Hubs;

public sealed class LiveQuizHub(LiveQuizCoordinator coordinator) : Hub
{
    public async Task<LiveQuizSnapshot> JoinQuiz(JoinLiveQuizDto dto)
    {
        var snapshot = await coordinator.JoinAsync(dto);
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveQuizCoordinator.Group(dto.Code));
        return snapshot;
    }

    public async Task<LiveQuizSnapshot> JoinHost(string code)
    {
        var snapshot = coordinator.GetSnapshot(code) ?? throw new HubException("Live quiz not found.");
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveQuizCoordinator.Group(code));
        return snapshot;
    }

    public Task StartLobby(string code) => coordinator.StartLobbyAsync(code);
    public Task StartQuestion(string code) => coordinator.StartQuestionAsync(code);
    public Task PauseQuiz(string code) => coordinator.PauseAsync(code);
    public Task NextQuestion(string code) => coordinator.NextQuestionAsync(code);
    public Task SubmitAnswer(SubmitLiveQuizAnswerDto dto) => coordinator.SubmitAnswerAsync(dto);
}
