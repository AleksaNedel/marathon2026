using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class TryToRemoveGroupParticipant(string persona) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        string? bearerToken = persona switch
        {
            "the guest of the group" => actor.Recall<GroupPreOrderIdentity>("group-guest").IdToken,
            "another customer" => await SignInAsNewCustomerAsync(actor),
            "the waiter assigned to the table" => actor.Recall<GroupPreOrderIdentity>("assigned-waiter").IdToken,
            "an unauthenticated visitor" => null,
            _ => throw new NotSupportedException($"Unknown group participant persona '{persona}'."),
        };

        await new DeleteGroupParticipant(bearerToken: bearerToken, useOwnerToken: false).PerformAsAsync(actor);
    }

    private static async Task<string> SignInAsNewCustomerAsync(Actor actor)
    {
        var authApi = new AuthApi(actor.UsingTheApi());
        var credentials = SampleDataFactory.RegisterRequest() with
        {
            Email = SampleDataFactory.UniqueSampleEmail(),
        };
        var register = await authApi.RegisterAsync(credentials);
        if (!register.Ok)
            throw new InvalidOperationException(
                $"Registering the other customer failed with {register.Status}: {await register.ReadErrorMessageAsync()}");

        var signIn = await authApi.SignInAsync(new SignInRequest(credentials.Email, credentials.Password));
        if (!signIn.Ok)
            throw new InvalidOperationException(
                $"Signing in the other customer failed with {signIn.Status}: {await signIn.ReadErrorMessageAsync()}");

        return (await signIn.ReadAsAsync<SignInResponse>()).IdToken;
    }
}
