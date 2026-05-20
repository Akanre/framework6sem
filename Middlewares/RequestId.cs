using Microsoft.AspNetCore.Http;

namespace Pr3.ConfigAndSecurity.Middlewares;

public static class RequestId
{
    private const string RequestIdKey = "RequestId";

    public static string GetOrCreate(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestIdKey, out var value) && value is string requestId)
        {
            return requestId;
        }

        var newRequestId = Guid.NewGuid().ToString();
        context.Items[RequestIdKey] = newRequestId;
        return newRequestId;
    }
}