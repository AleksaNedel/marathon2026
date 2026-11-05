using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetInvitationAsync(string token) =>
        GetAsync(Url($"/guest/invites/{Uri.EscapeDataString(token)}"));

    public Task<IAPIResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishToGuestSessionAsync(
        string guestSessionId, AddGuestDishRequest request, string guestToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> SubmitGuestCartAsync(string guestSessionId, string guestToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/submit"), new APIRequestContextOptions
        {
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> GetGuestSessionsAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/sessions"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> DeleteGuestSessionAsync(
        string groupReservationId, string guestSessionId, string? bearerToken = null) =>
        DeleteAsync(Url($"/guest/groups/{groupReservationId}/sessions/{guestSessionId}"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> SubmitGroupPreOrderAsync(
        string groupReservationId, string reservationId, string bearerToken) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(new SubmitGroupPreOrderRequest(reservationId)),
            Headers = Headers(bearerToken),
        });
}
