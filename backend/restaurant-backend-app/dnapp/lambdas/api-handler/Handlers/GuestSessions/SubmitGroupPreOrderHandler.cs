using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class SubmitGroupPreOrderHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Missing groupReservationId in path parameters.", context.Logger);

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        SubmitGroupPreOrderRequest? requestBody;
        try
        {
            requestBody = JsonSerializer.Deserialize<SubmitGroupPreOrderRequest>(
                request.Body ?? string.Empty,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger);
        }

        if (requestBody is null || string.IsNullOrWhiteSpace(requestBody.ReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "ReservationId is required.", context.Logger);

        try
        {
            var submittedDishes = await guestSessionService.SubmitGroupPreOrderAsync(
                userId,
                groupReservationId,
                requestBody.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, submittedDishes);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (ValidationException exception)
        {
            return ResponseBuilder.CreateErrorResponse(400, exception.Message, context.Logger);
        }
    }
}
