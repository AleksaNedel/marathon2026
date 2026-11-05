using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GuestPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator GuestNameInput => Page.GetByLabel("Enter Your Name", new() { Exact = true });
    public ILocator BrowseMenuButton => Page.GetByRole(AriaRole.Button, new() { Name = "Browse Menu" });
    public ILocator CartButton => Page.GetByRole(AriaRole.Button, new() { Name = "Open cart", Exact = true });
    public ILocator CartDialog => Page.GetByRole(AriaRole.Dialog);
    public ILocator SubmitCartButton => CartDialog.GetByRole(
        AriaRole.Button,
        new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator SubmittedHeading => Page.GetByRole(
        AriaRole.Heading,
        new() { Name = "Pre-Order Submitted!", Exact = true });

    public Task NavigateAsync(string invitationToken) =>
        NavigateToAsync($"/join/{Uri.EscapeDataString(invitationToken)}");

    public async Task SubmitDishAsync(string guestName, string dishName)
    {
        await GuestNameInput.FillAsync(guestName);
        await BrowseMenuButton.ClickAsync();

        var dish = Page.Locator(".dish-cards app-dish-card").Filter(new() { HasText = dishName });
        await dish.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await new DishCardComponent(dish).PreOrderAsync();

        await CartButton.ClickAsync();
        await SubmitCartButton.ClickAsync();
        await SubmittedHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }
}
