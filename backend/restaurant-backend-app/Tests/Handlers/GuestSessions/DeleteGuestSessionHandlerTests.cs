using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.GuestSessions;

public class DeleteGuestSessionHandlerTests
{
    [Fact]
    public async Task HandleRequest_ReturnsUpdatedSnapshotWithCamelCaseProperties()
    {
        var snapshot = new GetAllSessionsResponse(
            [new Participant("session2", ParticipantType.Guest, "Olena", [], 220, false)],
            220);
        var guestSessionService = new Mock<IGuestSessionService>();
        guestSessionService
            .Setup(service => service.DeleteGuestSessionAsync("owner-id", "group-id", "session1"))
            .ReturnsAsync(snapshot);

        var request = JsonSerializer.Deserialize<APIGatewayProxyRequest>(
            """{"pathParameters":{"groupReservationId":"group-id","guestSessionId":"session1"},"requestContext":{"authorizer":{"claims":{"sub":"owner-id"}}}}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var handler = new DeleteGuestSessionHandler(guestSessionService.Object);

        var response = await handler.HandleRequest(request, null!);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        var root = body.RootElement;
        Assert.Equal(220, root.GetProperty("total").GetDecimal());

        var participant = root.GetProperty("participants")[0];
        Assert.Equal("session2", participant.GetProperty("id").GetString());
        Assert.Equal("Guest", participant.GetProperty("type").GetString());
        Assert.Equal("Olena", participant.GetProperty("name").GetString());
        Assert.Equal(220, participant.GetProperty("total").GetDecimal());
        Assert.False(participant.GetProperty("isSubmitted").GetBoolean());
        Assert.True(participant.GetProperty("items").GetArrayLength() == 0);
    }

    [Fact]
    public async Task HandleRequest_WhenGroupReservationNotFound_Returns404()
    {
        var service = new Mock<IGuestSessionService>();
        service
            .Setup(s => s.DeleteGuestSessionAsync("owner-id", "group-id", "session-id"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await new DeleteGuestSessionHandler(service.Object)
            .HandleRequest(CreateRequest(), CreateLambdaContext());

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenGuestSessionNotFound_Returns404()
    {
        var service = new Mock<IGuestSessionService>();
        service
            .Setup(s => s.DeleteGuestSessionAsync("owner-id", "group-id", "missing"))
            .ThrowsAsync(new GuestSessionNotFoundException("missing"));

        var request = JsonSerializer.Deserialize<APIGatewayProxyRequest>(
            """{"pathParameters":{"groupReservationId":"group-id","guestSessionId":"missing"},"requestContext":{"authorizer":{"claims":{"sub":"owner-id"}}}}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var response = await new DeleteGuestSessionHandler(service.Object)
            .HandleRequest(request, CreateLambdaContext());

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenGuestSessionBelongsToAnotherGroup_Returns400()
    {
        var service = new Mock<IGuestSessionService>();
        service
            .Setup(s => s.DeleteGuestSessionAsync("owner-id", "group-id", "session-id"))
            .ThrowsAsync(new ValidationException("Guest session does not belong to this group reservation."));

        var response = await new DeleteGuestSessionHandler(service.Object)
            .HandleRequest(CreateRequest(), CreateLambdaContext());

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenUserIsNotOwner_Returns403()
    {
        var service = new Mock<IGuestSessionService>();
        service
            .Setup(s => s.DeleteGuestSessionAsync("owner-id", "group-id", "session-id"))
            .ThrowsAsync(new ActionForbiddenException("User is not the owner."));

        var response = await new DeleteGuestSessionHandler(service.Object)
            .HandleRequest(CreateRequest(), CreateLambdaContext());

        Assert.Equal(403, response.StatusCode);
    }

    private static APIGatewayProxyRequest CreateRequest() =>
        JsonSerializer.Deserialize<APIGatewayProxyRequest>(
            """{"pathParameters":{"groupReservationId":"group-id","guestSessionId":"session-id"},"requestContext":{"authorizer":{"claims":{"sub":"owner-id"}}}}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static ILambdaContext CreateLambdaContext()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());
        return context.Object;
    }
}