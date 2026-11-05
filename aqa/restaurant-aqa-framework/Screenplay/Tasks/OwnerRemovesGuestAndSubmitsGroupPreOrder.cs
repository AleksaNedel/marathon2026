using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OwnerRemovesGuestAndSubmitsGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>();
        var guests = actor.Recall<GroupPreOrderUiGuests>();
        var owner = reservation.Owner;
        var ownerLogin = new SignInThroughTheLoginForm(owner.Email!, owner.Password!);
        await actor.AttemptsToAsync(ownerLogin);
        var page = actor.LookingAtThePage();
        if (!await page.EvaluateAsync<bool>("() => Boolean(localStorage.getItem('auth'))"))
            throw new InvalidOperationException("The owner sign-in form completed without persisting an authenticated session.");

        var groupOrderPage = new GroupOrderPage(page);
        await groupOrderPage.NavigateAsync(reservation.ReservationId);
        await groupOrderPage.WaitForSubmittedParticipantAsync(guests.RemovedGuestName);
        await groupOrderPage.WaitForSubmittedParticipantAsync(guests.RemainingGuestName);
        await groupOrderPage.RemoveParticipantAsync(guests.RemovedGuestName);
        await groupOrderPage.SubmitGroupPreOrderAsync();
        await groupOrderPage.Header.SignOutAsync();
    }
}
