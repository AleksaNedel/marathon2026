namespace APIHandler.Models.GuestSessions;

public record SubmitGroupPreOrderRequest(string ReservationId);

public record SubmittedGroupPreOrderItem(string DishId, int Quantity);
