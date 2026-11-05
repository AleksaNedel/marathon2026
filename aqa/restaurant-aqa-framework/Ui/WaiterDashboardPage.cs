using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class WaiterDashboardPage(IPage page) : BasePage(page)
{
    public Task NavigateAsync() => NavigateToAsync("/waiter-reservations");

    public async Task OpenPreOrderAsync(string ownerName)
    {
        var reservation = Page.Locator("ul.reservation-list app-card-reservation")
            .Filter(new() { HasText = ownerName });
        await reservation.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await reservation.GetByRole(
            AriaRole.Button,
            new() { Name = "Pre-order", Exact = true }).ClickAsync();
        await Page.Locator(".preorder-list").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }
}
