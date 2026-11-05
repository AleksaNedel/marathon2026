using Restaurant_Aqa_Framework.Ui;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Signs in through the storefront's login form and waits until it has moved on from the login page.</summary>
public sealed class SignInThroughTheLoginForm(string email, string password, bool openTheLoginPage = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var loginPage = new LoginPage(page);

        try
        {
            if (openTheLoginPage)
                await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            await loginPage.EmailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await loginPage.SignInAsync(email, password);
            await page.WaitForURLAsync(
                url => !url.Contains("/login", StringComparison.Ordinal),
                new PageWaitForURLOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException exception)
        {
            var errorText = await loginPage.VisibleErrorTextAsync();
            throw new InvalidOperationException(
                $"Signing in through the login form timed out at '{page.Url}'. Visible error: {errorText}",
                exception);
        }
    }
}
