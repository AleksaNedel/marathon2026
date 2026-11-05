using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class WaiterOpensSubmittedGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>();
        var waiter = reservation.AssignedWaiter;
        if (string.IsNullOrWhiteSpace(waiter.Email) || string.IsNullOrWhiteSpace(waiter.Password))
            throw new InvalidOperationException("The assigned waiter credentials were not retained for the UI scenario.");

        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(waiter.Email, waiter.Password));
        var dashboard = new WaiterDashboardPage(actor.LookingAtThePage());
        await dashboard.NavigateAsync();
        await dashboard.OpenPreOrderAsync(reservation.Owner.Name);
    }
}
