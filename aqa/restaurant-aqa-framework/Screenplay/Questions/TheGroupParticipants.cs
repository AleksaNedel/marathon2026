using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheGroupParticipants : IQuestion<GroupSessionsResponse>
{
    public Task<GroupSessionsResponse> AnsweredByAsync(Actor actor) =>
        Task.FromResult(actor.TheLastResponse().Json<GroupSessionsResponse>());
}
