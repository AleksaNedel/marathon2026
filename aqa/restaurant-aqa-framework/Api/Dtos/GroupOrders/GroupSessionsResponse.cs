namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

public sealed record GroupSessionsResponse(IReadOnlyList<GroupParticipantResponse> Participants, decimal Total);
