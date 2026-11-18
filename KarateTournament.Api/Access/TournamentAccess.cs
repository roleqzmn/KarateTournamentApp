using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace KarateTournament.Api.Access;

internal enum TournamentAccessLevel { Judge, Organizer }

internal sealed record TournamentRouteAccess(TournamentAccessLevel Level, bool UsesRecoveryKey = false)
{
    public static TournamentRouteAccess Organizer { get; } = new(TournamentAccessLevel.Organizer);
    public static TournamentRouteAccess Recovery { get; } = new(TournamentAccessLevel.Organizer, UsesRecoveryKey: true);
}

internal sealed class TournamentAccess
{
    private readonly byte[]? _organizerKey;
    private readonly byte[]? _judgeKey;

    public TournamentAccess(IConfiguration configuration)
    {
        var organizer = configuration["Access:OrganizerKey"];
        var judge = configuration["Access:JudgeKey"];
        if (string.IsNullOrEmpty(organizer) && string.IsNullOrEmpty(judge))
            return;
        if (!TryReadKey(organizer, out _organizerKey) || !TryReadKey(judge, out _judgeKey)
            || CryptographicOperations.FixedTimeEquals(_organizerKey, _judgeKey))
            throw new InvalidOperationException("Configure two distinct 32-byte hex keys in Access:OrganizerKey and Access:JudgeKey.");
    }

    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next();
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (_organizerKey is null)
        {
            // Kestrel sets a remote IP for TCP clients. TestServer and local transports may not have one.
            var ip = context.Connection.RemoteIpAddress;
            if (ip is not null && !IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next();
            return;
        }

        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var policy = context.GetEndpoint()?.Metadata.GetMetadata<TournamentRouteAccess>();
        if (policy?.UsesRecoveryKey == true)
        {
            await next();
            return;
        }

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || !TryReadKey(authorization.Parameter, out var provided))
        {
            RejectCredential(context);
            return;
        }

        var organizer = CryptographicOperations.FixedTimeEquals(provided, _organizerKey);
        var judge = CryptographicOperations.FixedTimeEquals(provided, _judgeKey!);
        if (!organizer && !judge)
        {
            RejectCredential(context);
            return;
        }
        if (!organizer && policy?.Level == TournamentAccessLevel.Organizer)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next();
    }

    private static void RejectCredential(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private static bool TryReadKey(string? value, out byte[] key)
    {
        key = [];
        if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit))
            return false;
        key = Convert.FromHexString(value);
        return true;
    }
}
