
using Microsoft.AspNetCore.Http;

namespace RaceDay.Api.Security;

public static class SessionAuthorization
{
    public static bool IsAuthenticated(HttpContext context)
    {
        return context.Session.GetInt32("UserId").HasValue;
    }

    public static bool HasRole(HttpContext context, string requiredRole)
    {
        var userId = context.Session.GetInt32("UserId");
        var role = context.Session.GetString("Role");

        return userId.HasValue &&
               string.Equals(
                   role,
                   requiredRole,
                   StringComparison.OrdinalIgnoreCase);
    }
}
