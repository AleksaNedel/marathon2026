namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

public sealed record GroupInviteResponse(
    string GroupReservationId,
    string LocationId,
    string RestaurantName,
    string LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int MaxGuestCount,
    int TableNumber,
    string InviterName);
