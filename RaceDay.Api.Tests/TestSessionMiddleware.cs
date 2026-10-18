
using Microsoft.AspNetCore.Http;

namespace RaceDay.Api.Tests;

public class TestSessionMiddleware
{
    private readonly RequestDelegate _next;

    public TestSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var testUserId = context.Request.Headers["X-Test-UserId"].FirstOrDefault();
        var testRole = context.Request.Headers["X-Test-Role"].FirstOrDefault();

        if (int.TryParse(testUserId, out var userId) &&
            (testRole == "Participant" || testRole == "Organiser"))
        {
            context.Session.SetInt32("UserId", userId);
            context.Session.SetString("Role", testRole);
        }

        await _next(context);
    }
}
