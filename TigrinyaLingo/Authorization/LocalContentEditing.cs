using System.Net;
using Microsoft.AspNetCore.Authorization;

namespace TigrinyaLingo.Authorization;

// Redigeraren är ett lokalt utvecklingsverktyg. Ingen nätverksadress eller HTTP-header kan slå på den.
public static class LocalContentEditing
{
    public const string Policy = "LocalContentEditing";

    public static void Configure(AuthorizationOptions options, bool isDevelopment)
    {
        options.AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            isDevelopment && context.Resource is HttpContext http
            && http.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address)));
    }
}
