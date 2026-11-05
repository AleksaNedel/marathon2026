using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class DeleteGroupParticipant(
    string participant = "first",
    string reservationMemoryName = "group",
    string guestMemoryName = "group-guest",
    string? bearerToken = null,
    bool useOwnerToken = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>(reservationMemoryName);
        var target = participant switch
        {
            "first" => actor.Recall<IReadOnlyList<GroupPreOrderGuest>>(guestMemoryName)[0],
            "only" => actor.Recall<IReadOnlyList<GroupPreOrderGuest>>(guestMemoryName).Single(),
            "different group" => actor.Recall<GroupPreOrderGuest>("other-group-guest"),
            "missing" => new GroupPreOrderGuest("missing", Guid.NewGuid().ToString(), ""),
            _ => throw new NotSupportedException($"Unknown participant selection '{participant}'."),
        };
        var response = await new GroupOrdersApi(actor.UsingTheApi()).DeleteGuestSessionAsync(
            reservation.GroupReservationId,
            target.SessionId,
            useOwnerToken ? reservation.Owner.IdToken : bearerToken);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}
