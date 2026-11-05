using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SubmitGroupGuestCarts(
    int count,
    string reservationMemoryName = "group",
    string guestMemoryName = "group-guest") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>(reservationMemoryName);
        var guests = actor.Recall<IReadOnlyList<GroupPreOrderGuest>>(guestMemoryName);
        var api = new GroupOrdersApi(actor.UsingTheApi());

        if (count > guests.Count)
            throw new InvalidOperationException($"Requested {count} guest carts, but only {guests.Count} guests joined.");

        for (var index = 0; index < count; index++)
        {
            var guest = guests[index];
            var addResponse = await api.AddDishToGuestSessionAsync(
                guest.SessionId,
                new AddGuestDishRequest(reservation.Dish.Id, index + 1),
                guest.SessionToken);
            if (!addResponse.Ok)
                throw new InvalidOperationException(
                    $"Adding a dish to the guest cart failed with {addResponse.Status}: {await addResponse.ReadErrorMessageAsync()}");

            var submitResponse = await api.SubmitGuestCartAsync(guest.SessionId, guest.SessionToken);
            if (!submitResponse.Ok)
                throw new InvalidOperationException(
                    $"Submitting the guest cart failed with {submitResponse.Status}: {await submitResponse.ReadErrorMessageAsync()}");

            await actor.TheLastResponse().CaptureAsync(submitResponse);
        }
    }
}
