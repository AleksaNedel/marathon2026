using Restaurant_Aqa_Framework.Api.Dtos.Dishes;

namespace Restaurant_Aqa_Framework.Screenplay;

public sealed record GroupPreOrderIdentity(
    string Name,
    string IdToken,
    string? Email = null,
    string? Password = null);

public sealed record GroupPreOrderGuest(string Name, string SessionId, string SessionToken);

public sealed record GroupPreOrderReservation(
    string ReservationId,
    string GroupReservationId,
    string TableId,
    string LocationId,
    DateTime StartTime,
    GroupPreOrderIdentity Owner,
    GroupPreOrderIdentity AssignedWaiter,
    DishResponse Dish,
    string InvitationToken,
    DishResponse? SecondDish = null);

public sealed record GroupPreOrderUiGuests(
    string RemovedGuestName,
    string RemainingGuestName,
    string RemovedDishName,
    string RemainingDishName);
