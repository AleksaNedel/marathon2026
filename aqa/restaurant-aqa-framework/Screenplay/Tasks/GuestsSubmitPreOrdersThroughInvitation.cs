using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Hooks;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class GuestsSubmitPreOrdersThroughInvitation : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>();
        var secondDish = reservation.SecondDish
            ?? throw new InvalidOperationException("The UI fixture did not create its second dish.");
        var api = new GroupOrdersApi(actor.UsingTheApi());
        var uiGuests = new GroupPreOrderUiGuests(
            CreateGuestName(),
            CreateGuestName(),
            reservation.Dish.Name,
            secondDish.Name);

        await SubmitGuestAsync(reservation, api, uiGuests.RemovedGuestName, uiGuests.RemovedDishName);
        await TrackGuestSessionAsync(actor, reservation, api, uiGuests.RemovedGuestName);
        await SubmitGuestAsync(reservation, api, uiGuests.RemainingGuestName, uiGuests.RemainingDishName);
        await TrackGuestSessionAsync(actor, reservation, api, uiGuests.RemainingGuestName);

        actor.Remember(uiGuests);
    }

    private static async Task SubmitGuestAsync(
        GroupPreOrderReservation reservation,
        GroupOrdersApi api,
        string guestName,
        string dishName)
    {
        var settings = Settings.Current;
        var context = await PlaywrightRunHooks.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = settings.UiBaseUrl,
        });
        context.SetDefaultTimeout(settings.DefaultTimeoutMs);
        context.SetDefaultNavigationTimeout(settings.NavigationTimeoutMs);

        try
        {
            var guestPage = new GuestPreOrderPage(await context.NewPageAsync());
            await guestPage.NavigateAsync(reservation.InvitationToken);
            await guestPage.SubmitDishAsync(guestName, dishName);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private static async Task TrackGuestSessionAsync(
        Actor actor,
        GroupPreOrderReservation reservation,
        GroupOrdersApi api,
        string guestName)
    {
        var response = await api.GetGuestSessionsAsync(
            reservation.GroupReservationId,
            reservation.Owner.IdToken);
        if (!response.Ok)
            throw new InvalidOperationException(
                $"Listing group participants after guest submission failed with {response.Status}: {await response.ReadErrorMessageAsync()}");

        var sessions = await response.ReadAsAsync<GroupSessionsResponse>();
        var participant = sessions.Participants.SingleOrDefault(candidate => candidate.Name == guestName);
        var sessionId = participant?.Id;
        if (participant is null || string.IsNullOrWhiteSpace(sessionId) || !participant.IsSubmitted)
            throw new InvalidOperationException($"Submitted guest '{guestName}' was not returned as a submitted participant.");

        actor.Recall<CleanupScope>().TrackDelete(
            $"guest-session:{sessionId}",
            () => api.DeleteGuestSessionAsync(
                reservation.GroupReservationId,
                sessionId,
                reservation.Owner.IdToken),
            404);
        await actor.TheLastResponse().CaptureAsync(response);
    }

    private static string CreateGuestName() =>
        $"{SampleDataFactory.Prefix}Guest-{Guid.NewGuid():N}"[..26];
}
