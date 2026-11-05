using System.Collections.Concurrent;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Dtos.Users;
using Restaurant_Aqa_Framework.Api.Dtos.Waiters;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SetUpGroupPreOrder(string memoryName = "group", bool createSecondDish = false) : ITask
{
    private const int TableAvailabilityAttempts = 12;
    private static readonly ConcurrentDictionary<int, byte> ScheduledDayOffsets = new();

    public async Task PerformAsAsync(Actor actor)
    {
        var settings = Settings.Current;
        if (string.IsNullOrWhiteSpace(settings.TestTableId) ||
            string.IsNullOrWhiteSpace(settings.TestTableLocationId))
        {
            Skip.Test(
                "Group pre-order API scenarios require both TESTTABLE_ID and TESTTABLE_LOCATION_ID. Set both in .env or as environment variables.");
            return;
        }

        string waiterUsername;
        string waiterPassword;
        if (!string.IsNullOrWhiteSpace(settings.TestTableWaiterUsername) &&
            !string.IsNullOrWhiteSpace(settings.TestTableWaiterPassword))
        {
            waiterUsername = settings.TestTableWaiterUsername;
            waiterPassword = settings.TestTableWaiterPassword;
        }
        else if (!string.IsNullOrWhiteSpace(settings.WaiterUsername) &&
                 !string.IsNullOrWhiteSpace(settings.WaiterPassword))
        {
            waiterUsername = settings.WaiterUsername;
            waiterPassword = settings.WaiterPassword;
        }
        else
        {
            Skip.Test(
                "Group pre-order API scenarios require WAITER_USERNAME/WAITER_PASSWORD or TESTTABLE_WAITER_USERNAME/TESTTABLE_WAITER_PASSWORD to identify a waiter-assigned table.");
            return;
        }

        var apiContext = actor.UsingTheApi();
        var authApi = new AuthApi(apiContext);
        var waiterSignIn = await authApi.SignInAsync(new SignInRequest(waiterUsername, waiterPassword));
        if (!waiterSignIn.Ok)
            throw new InvalidOperationException(
                $"Signing in the configured waiter failed with {waiterSignIn.Status}: {await waiterSignIn.ReadErrorMessageAsync()}");

        var waiterToken = (await waiterSignIn.ReadAsAsync<SignInResponse>()).IdToken;
        var waiterUserResponse = await new UsersApi(apiContext).GetCurrentUserAsync(waiterToken);
        if (!waiterUserResponse.Ok)
            throw new InvalidOperationException(
                $"Getting the configured waiter's identity failed with {waiterUserResponse.Status}: {await waiterUserResponse.ReadErrorMessageAsync()}");

        var waiterUser = await waiterUserResponse.ReadAsAsync<UserDto>();
        var waiterLocationResponse = await new WaitersApi(apiContext).GetLocationAsync(waiterUser.Id, waiterToken);
        if (!waiterLocationResponse.Ok)
            throw new InvalidOperationException(
                $"Getting the configured waiter's assigned location failed with {waiterLocationResponse.Status}: {await waiterLocationResponse.ReadErrorMessageAsync()}");

        var waiterLocation = await waiterLocationResponse.ReadAsAsync<WaiterLocationResponse>();
        if (!string.Equals(waiterLocation.LocationId, settings.TestTableLocationId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The configured waiter is not assigned to TESTTABLE_LOCATION_ID.");

        var assignedWaiter = new GroupPreOrderIdentity(
            waiterUser.FirstName,
            waiterToken,
            waiterUsername,
            waiterPassword);
        actor.Remember("assigned-waiter", assignedWaiter);

        var (table, startTime) = await FindTableAsync(
            apiContext,
            settings.TestTableLocationId,
            settings.TestTableId,
            guestCount: 4);

        var ownerCredentials = SampleDataFactory.RegisterRequest() with
        {
            Email = SampleDataFactory.UniqueSampleEmail(),
            FirstName = CreateUniqueOwnerName(),
        };
        var registerResponse = await authApi.RegisterAsync(ownerCredentials);
        if (!registerResponse.Ok)
            throw new InvalidOperationException(
                $"Registering the group reservation owner failed with {registerResponse.Status}: {await registerResponse.ReadErrorMessageAsync()}");

        var signInResponse = await authApi.SignInAsync(new SignInRequest(ownerCredentials.Email, ownerCredentials.Password));
        if (!signInResponse.Ok)
            throw new InvalidOperationException(
                $"Signing in the group reservation owner failed with {signInResponse.Status}: {await signInResponse.ReadErrorMessageAsync()}");

        var ownerToken = (await signInResponse.ReadAsAsync<SignInResponse>()).IdToken;
        var owner = new GroupPreOrderIdentity(
            ownerCredentials.FirstName,
            ownerToken,
            ownerCredentials.Email,
            ownerCredentials.Password);
        actor.Remember(memoryName, owner);

        var dishApi = new DishesApi(apiContext);
        var dishRequest = SampleDataFactory.CreateDishRequest([table.LocationId], DishType.MainCourse) with
        {
            Name = $"{SampleDataFactory.Prefix}Group-{Guid.NewGuid():N}",
        };
        var dishResponse = await dishApi.CreateDishAsync(dishRequest);
        if (!dishResponse.Ok)
            throw new InvalidOperationException(
                $"Creating the group pre-order dish failed with {dishResponse.Status}: {await dishResponse.ReadErrorMessageAsync()}");

        var dish = await dishResponse.ReadAsAsync<DishResponse>();
        actor.Recall<CleanupScope>().TrackDelete(
            $"dish:{dish.Id}",
            () => dishApi.DeleteDishAsync(dish.Id, force: true, bearerToken: waiterToken),
            404);

        DishResponse? secondDish = null;
        if (createSecondDish)
        {
            var secondDishResponse = await dishApi.CreateDishAsync(
                SampleDataFactory.CreateDishRequest([table.LocationId], DishType.MainCourse) with
                {
                    Name = $"{SampleDataFactory.Prefix}Group-{Guid.NewGuid():N}",
                });
            if (!secondDishResponse.Ok)
                throw new InvalidOperationException(
                    $"Creating the second group pre-order dish failed with {secondDishResponse.Status}: {await secondDishResponse.ReadErrorMessageAsync()}");

            secondDish = await secondDishResponse.ReadAsAsync<DishResponse>();
            actor.Recall<CleanupScope>().TrackDelete(
                $"dish:{secondDish.Id}",
                () => dishApi.DeleteDishAsync(secondDish.Id, force: true, bearerToken: waiterToken),
                404);
        }

        var reservationsApi = new ReservationsApi(apiContext);
        var reservationResponse = await reservationsApi.CreateReservationAsync(
            SampleDataFactory.CreateReservationRequest(table.Id, startTime, guestCount: 4),
            ownerToken);
        var reservationError = string.Empty;
        for (var attempt = 1;
             !reservationResponse.Ok && attempt < TableAvailabilityAttempts;
             attempt++)
        {
            reservationError = await reservationResponse.ReadErrorMessageAsync();
            if (!IsAvailabilityConflict(reservationResponse.Status, reservationError))
                break;

            (table, startTime) = await FindTableAsync(
                apiContext,
                settings.TestTableLocationId,
                settings.TestTableId,
                guestCount: 4);
            reservationResponse = await reservationsApi.CreateReservationAsync(
                SampleDataFactory.CreateReservationRequest(table.Id, startTime, guestCount: 4),
                ownerToken);
        }

        if (!reservationResponse.Ok)
        {
            reservationError = await reservationResponse.ReadErrorMessageAsync();
            throw new InvalidOperationException(
                IsAvailabilityConflict(reservationResponse.Status, reservationError)
                    ? $"The configured table is not available for 4 guests on {startTime:yyyy-MM-dd} at {startTime:HH:mm} UTC after {TableAvailabilityAttempts} reservation attempts."
                    : $"Creating the group reservation failed with {reservationResponse.Status}: {reservationError}");
        }

        var reservation = await reservationResponse.ReadAsAsync<ReservationResponse>();
        await actor.TheLastResponse().CaptureAsync(reservationResponse);
        actor.Recall<CleanupScope>().TrackDelete(
            $"reservation:{reservation.Id}",
            () => reservationsApi.CancelReservationAsync(reservation.Id, ownerToken),
            404);

        var groupApi = new GroupOrdersApi(apiContext);
        var tokenResponse = await groupApi.GetInviteTokenAsync(reservation.Id, ownerToken);
        if (!tokenResponse.Ok)
            throw new InvalidOperationException(
                $"Getting the group invitation failed with {tokenResponse.Status}: {await tokenResponse.ReadErrorMessageAsync()}");

        var invitationToken = (await tokenResponse.ReadAsAsync<GroupInviteTokenResponse>()).Token;
        var invitationResponse = await groupApi.GetInvitationAsync(invitationToken);
        if (!invitationResponse.Ok)
            throw new InvalidOperationException(
                $"Opening the group invitation failed with {invitationResponse.Status}: {await invitationResponse.ReadErrorMessageAsync()}");

        var invitation = await invitationResponse.ReadAsAsync<GroupInviteResponse>();
        await actor.TheLastResponse().CaptureAsync(invitationResponse);
        actor.Remember(memoryName, new GroupPreOrderReservation(
            reservation.Id,
            invitation.GroupReservationId,
            table.Id,
            table.LocationId,
            startTime,
            owner,
            assignedWaiter,
            dish,
            invitationToken,
            secondDish));
    }

    private static async Task<(TableResponse Table, DateTime StartTime)> FindTableAsync(
        Microsoft.Playwright.IAPIRequestContext apiContext,
        string locationId,
        string tableId,
        int guestCount)
    {
        var tablesApi = new TablesApi(apiContext);
        var startTime = DateTime.MinValue;
        for (var attempt = 0; attempt < TableAvailabilityAttempts; attempt++)
        {
            startTime = CreateStartTime();
            var availableResponse = await tablesApi.GetAvailableTablesAsync(locationId, guestCount, startTime);
            if (!availableResponse.Ok)
                throw new InvalidOperationException(
                    $"Checking table availability failed with {availableResponse.Status}: {await availableResponse.ReadErrorMessageAsync()}");

            var availableTables = await availableResponse.ReadAsAsync<IReadOnlyList<TableResponse>>();
            var selected = availableTables.FirstOrDefault(candidate => candidate.Id == tableId);
            if (selected is not null)
                return (selected, startTime);
        }

        throw new InvalidOperationException(
            $"The configured table is not available for {guestCount} guests on {startTime:yyyy-MM-dd} at {startTime:HH:mm} UTC (table ID: {tableId}) after {TableAvailabilityAttempts} random date/time attempts.");
    }

    private static DateTime CreateStartTime()
    {
        int dayOffset;
        do
        {
            dayOffset = Random.Shared.Next(30, 366);
        } while (!ScheduledDayOffsets.TryAdd(dayOffset, 0));

        return DateTime.SpecifyKind(
            DateTime.UtcNow.Date.AddDays(dayOffset).AddHours(18).AddMinutes(Random.Shared.Next(60)),
            DateTimeKind.Utc);
    }

    private static bool IsAvailabilityConflict(int status, string message) =>
        status == 400 && message.Contains("overlaps with an existing reservation", StringComparison.OrdinalIgnoreCase);

    private static string CreateUniqueOwnerName()
    {
        var letters = string.Concat(Guid.NewGuid().ToString("N").Select(character =>
            character is >= '0' and <= '9'
                ? (char)('A' + character - '0')
                : (char)('K' + character - 'a')));
        return $"{SampleDataFactory.Prefix}Owner-{letters}";
    }
}
