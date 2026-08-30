using System.Security.Cryptography;
using System.Text;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record LoginResult(Guid PlayerId, string AccessToken, DateTime ExpiresAtUtc, bool IsNewPlayer);

public sealed class AuthService(GameDbContext db, IServerClock clock, TaskService tasks)
{
    public async Task<LoginResult> LoginAsync(string provider, string externalAccountId, string nickname, CancellationToken cancellationToken)
    {
        provider = Normalize(provider, 32, "provider");
        externalAccountId = Normalize(externalAccountId, 128, "external account id");
        nickname = Normalize(nickname, 32, "nickname");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await db.Accounts.SingleOrDefaultAsync(
            value => value.Provider == provider && value.ExternalAccountId == externalAccountId, cancellationToken);
        var isNew = account is null;
        if (account is null)
        {
            account = new Account { Provider = provider, ExternalAccountId = externalAccountId };
            db.Accounts.Add(account);
        }
        account.LastLoginTimeUtc = clock.UtcNow;
        var player = await db.Players.SingleOrDefaultAsync(value => value.AccountId == account.Id, cancellationToken);
        if (player is null)
        {
            player = new Player
            {
                AccountId = account.Id, Nickname = nickname, LastLoginTimeUtc = clock.UtcNow,
                LastOfflineTimeUtc = clock.UtcNow
            };
            db.Players.Add(player);
            db.PlayerStats.Add(new PlayerStats { PlayerId = player.Id });
            db.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = player.Id });
        }
        else player.LastLoginTimeUtc = clock.UtcNow;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = clock.UtcNow.AddDays(30);
        db.AuthSessions.Add(new AuthSession
        {
            AccountId = account.Id, PlayerId = player.Id, TokenHash = Hash(rawToken),
            ExpiresAtUtc = expires, LastSeenAtUtc = clock.UtcNow
        });
        await tasks.RecordAsync(player.Id, "Login", 1, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginResult(player.Id, rawToken, expires, isNew);
    }

    public async Task<Guid?> ResolvePlayerAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorizationHeader[7..].Trim();
        if (token.Length != 64) return null;
        var hash = Hash(token);
        var session = await db.AuthSessions.SingleOrDefaultAsync(value => value.TokenHash == hash, cancellationToken);
        if (session is null || session.ExpiresAtUtc <= clock.UtcNow) return null;
        session.LastSeenAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return session.PlayerId;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Normalize(string value, int maxLength, string label)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > maxLength) throw new ArgumentException(label + " is required and cannot exceed " + maxLength + " characters.");
        return value;
    }
}
