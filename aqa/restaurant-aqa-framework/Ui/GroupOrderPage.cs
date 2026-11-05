using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public HeaderComponent Header { get; } = new(page);

    private ILocator ParticipantRows => Page.Locator(".participants > li");
    private ILocator RemoveDialog => Page.GetByRole(AriaRole.Dialog)
        .Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Remove Guest", Exact = true }) });
    private ILocator SubmitDialog => Page.GetByRole(AriaRole.Dialog)
        .Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Submit Pre-Order", Exact = true }) });

    public ILocator Participant(string name) =>
        ParticipantRows.Filter(new() { HasText = name });

    public Task NavigateAsync(string reservationId) =>
        NavigateToAsync($"/reservations/{Uri.EscapeDataString(reservationId)}/group-order");

    public async Task WaitForSubmittedParticipantAsync(string name)
    {
        var pageTitle = Page.GetByRole(
            AriaRole.Heading,
            new() { Name = "Group Pre-Order", Exact = true });
        try
        {
            await pageTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        catch (TimeoutException exception)
        {
            var currentPath = new Uri(Page.Url).AbsolutePath;
            throw new InvalidOperationException(
                $"The group order page did not load; the current route is '{currentPath}'.",
                exception);
        }

        var participant = Participant(name);
        try
        {
            await participant.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        catch (TimeoutException exception)
        {
            var error = Page.Locator(".error-state");
            var pageMessage = await error.CountAsync() > 0
                ? (await error.InnerTextAsync()).Trim()
                : "the participant was not present in the group order list";
            throw new InvalidOperationException(
                $"Participant '{name}' did not appear in the group order page: {pageMessage}.",
                exception);
        }

        await participant.Locator(".chip.submitted").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public async Task RemoveParticipantAsync(string name)
    {
        await Participant(name).GetByRole(
            AriaRole.Button,
            new() { Name = $"Remove {name}", Exact = true }).ClickAsync();
        await RemoveDialog.GetByRole(
            AriaRole.Button,
            new() { Name = "Remove Guest", Exact = true }).ClickAsync();
        await Participant(name).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    public async Task SubmitGroupPreOrderAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true }).ClickAsync();
        await SubmitDialog.GetByRole(
            AriaRole.Button,
            new() { Name = "Submit", Exact = true }).ClickAsync();
        await Page.GetByText("Sent to Restaurant", new() { Exact = true })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }
}
