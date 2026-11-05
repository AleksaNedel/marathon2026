using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.Waiters;

namespace Restaurant_Aqa_Framework.Api;

public sealed class WaitersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetLocationAsync(string waiterId, string bearerToken) =>
        GetAsync(Url($"/waiters/{waiterId}/location"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });
}
