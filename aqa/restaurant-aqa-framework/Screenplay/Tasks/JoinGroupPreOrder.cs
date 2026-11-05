using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class JoinGroupPreOrder(
    int count = 1,
    string memoryName = "group",
    string guestMemoryName = "group-guest") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var reservation = actor.Recall<GroupPreOrderReservation>(memoryName);
        var apiContext = actor.UsingTheApi();
        var authApi = new AuthApi(apiContext);
        var guestCredentials = SampleDataFactory.RegisterRequest() with
        {
            Email = SampleDataFactory.UniqueSampleEmail(),
        };
        var registerResponse = await authApi.RegisterAsync(guestCredentials);
        if (!registerResponse.Ok)
            throw new InvalidOperationException(
                $"Registering the guest failed with {registerResponse.Status}: {await registerResponse.ReadErrorMessageAsync()}");

        var signInResponse = await authApi.SignInAsync(new SignInRequest(guestCredentials.Email, guestCredentials.Password));
        if (!signInResponse.Ok)
            throw new InvalidOperationException(
                $"Signing in the guest failed with {signInResponse.Status}: {await signInResponse.ReadErrorMessageAsync()}");

        var guestIdentity = new GroupPreOrderIdentity(
            guestCredentials.FirstName,
            (await signInResponse.ReadAsAsync<SignInResponse>()).IdToken);
        actor.Remember(guestMemoryName, guestIdentity);

        var groupApi = new GroupOrdersApi(apiContext);
        var tokenResponse = await groupApi.GetInviteTokenAsync(reservation.ReservationId, reservation.Owner.IdToken);
        if (!tokenResponse.Ok)
            throw new InvalidOperationException(
                $"Getting the group invitation failed with {tokenResponse.Status}: {await tokenResponse.ReadErrorMessageAsync()}");

        var inviteToken = (await tokenResponse.ReadAsAsync<GroupInviteTokenResponse>()).Token;
        var inviteResponse = await groupApi.GetInvitationAsync(inviteToken);
        if (!inviteResponse.Ok)
            throw new InvalidOperationException(
                $"Opening the group invitation failed with {inviteResponse.Status}: {await inviteResponse.ReadErrorMessageAsync()}");

        var invite = await inviteResponse.ReadAsAsync<GroupInviteResponse>();
        if (invite.GroupReservationId != reservation.GroupReservationId)
            throw new InvalidOperationException("The invitation did not resolve to the created group reservation.");

        var guests = new List<GroupPreOrderGuest>();
        for (var index = 0; index < count; index++)
        {
            var guestName = $"{SampleDataFactory.Prefix}Guest-{Guid.NewGuid():N}"[..26];
            var createResponse = await groupApi.CreateGuestSessionAsync(
                new CreateGuestSessionRequest(invite.GroupReservationId, guestName));
            if (!createResponse.Ok)
                throw new InvalidOperationException(
                    $"Joining the group reservation failed with {createResponse.Status}: {await createResponse.ReadErrorMessageAsync()}");

            var created = await createResponse.ReadAsAsync<CreateGuestSessionResponse>();
            var guest = new GroupPreOrderGuest(guestName, created.SessionId, created.SessionToken);
            guests.Add(guest);
            actor.Remember(guestMemoryName, guest);
            actor.Recall<CleanupScope>().TrackDelete(
                $"guest-session:{created.SessionId}",
                () => groupApi.DeleteGuestSessionAsync(
                    reservation.GroupReservationId,
                    created.SessionId,
                    reservation.Owner.IdToken),
                404);
            await actor.TheLastResponse().CaptureAsync(createResponse);
        }

        actor.Remember(guestMemoryName, (IReadOnlyList<GroupPreOrderGuest>)guests);
    }
}
