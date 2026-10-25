using System.Security.Claims;
using FellowshipSim.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace FellowshipSim.Service.Services;

/// <summary>
/// Each browser's <see cref="Visitor"/>: a random ID in an HttpOnly cookie, issued on the first request and shared by
/// every tab of that browser. A circuit reads it from its user (<see cref="AuthenticationStateProvider"/>), since no
/// component reads the HTTP request.
/// </summary>
public static class VisitorIdentity
{
    public const string ClaimType = "simfell:visitor";
    public const string CookieName = "simfell_visitor";

    /// <summary>The visitor cookie, kept for 400 days after the last visit.</summary>
    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(400);
        options.SlidingExpiration = true;
    }

    /// <summary>
    /// Signs a request without a visitor cookie in as a new visitor, in a cookie that outlives the browser. The request's
    /// user is the new visitor too, so the page rendered for this same request has it. A visitor whose cookie ends with
    /// the browser (issued before) gets it again, persistent.
    /// </summary>
    public static async Task IssueAsync(HttpContext context, RequestDelegate next)
    {
        if (IdOf(context.User) != null)
        {
            // A cookie issued before it was persistent ends with the browser: it is issued again for the same visitor.
            var current = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (current is { Succeeded: true, Properties.IsPersistent: false })
            {
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, current.Principal!,
                    new AuthenticationProperties { IsPersistent = true });
            }
        }
        else
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimType, Guid.NewGuid().ToString("N"))], CookieAuthenticationDefaults.AuthenticationScheme));
            // Persistent: without it the cookie lasts until the browser closes, and the next visit is a new visitor who
            // sees none of their saved builds, lists, sims or reviews.
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, new AuthenticationProperties { IsPersistent = true });
            context.User = user;
        }
        await next(context);
    }

    /// <summary>The visitor ID of <paramref name="user"/>, or null when it has none.</summary>
    public static string? IdOf(ClaimsPrincipal user) => user.FindFirst(ClaimType)?.Value;

    /// <summary>
    /// The visitor of a circuit or a rendered request. Blazor sets the authentication state before it creates any
    /// component, so the state is complete by the time a component's services resolve.
    /// </summary>
    public static Visitor Of(AuthenticationStateProvider authentication)
    {
        var state = authentication.GetAuthenticationStateAsync();
        if (!state.IsCompletedSuccessfully) throw new InvalidOperationException("The visitor is not known yet.");
        string id = IdOf(state.Result.User) ?? throw new InvalidOperationException("The request has no visitor cookie.");
        return new Visitor(id, OwnsDataFolder: false);
    }
}
