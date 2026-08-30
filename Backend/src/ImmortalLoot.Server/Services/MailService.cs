using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed class MailService(GameDbContext db, RewardService rewards, IServerClock clock)
{
    public Task<List<PlayerMail>> ListAsync(Guid playerId, CancellationToken cancellationToken) => db.PlayerMails.AsNoTracking()
        .Where(value => value.PlayerId == playerId && value.ExpiresAtUtc > clock.UtcNow).OrderByDescending(value => value.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<RewardResult> ClaimAsync(Guid playerId, Guid mailId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var mail = await db.PlayerMails.SingleOrDefaultAsync(value => value.Id == mailId && value.PlayerId == playerId, cancellationToken)
            ?? throw new KeyNotFoundException("Mail was not found.");
        if (mail.ExpiresAtUtc <= clock.UtcNow) throw new InvalidOperationException("Mail has expired.");
        var payload = JsonSerializer.Deserialize<RewardPayload>(mail.AttachmentJson) ?? new RewardPayload();
        var result = await rewards.GrantTrackedAsync(playerId, "mail:" + mail.Id.ToString("N"), "Mail", payload, cancellationToken);
        mail.IsClaimed = true;
        mail.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
