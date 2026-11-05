using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ListGroupParticipants(string reservationMemoryName = "group") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>(reservationMemoryName);
        var response = await new GroupOrdersApi(actor.UsingTheApi()).GetGuestSessionsAsync(
            reservation.GroupReservationId,
            reservation.Owner.IdToken);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}
