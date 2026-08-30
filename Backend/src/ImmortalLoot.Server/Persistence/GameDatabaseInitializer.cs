using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Persistence;

public static class GameDatabaseInitializer
{
    public const string ActiveBattleIndexName = "IX_BattleSession_PlayerId_Started";

    public static async Task InitializeAsync(
        GameDbContext db,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var activeSessions = await db.BattleSessions
            .Where(value => value.Status == "Started")
            .OrderBy(value => value.PlayerId)
            .ThenByDescending(value => value.StartedAtUtc)
            .ThenByDescending(value => value.Id)
            .ToListAsync(cancellationToken);
        foreach (var duplicate in activeSessions
                     .GroupBy(value => value.PlayerId)
                     .SelectMany(group => group.Skip(1)))
        {
            duplicate.Status = "Invalidated";
            duplicate.FinishedAtUtc ??= utcNow;
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            $"CREATE UNIQUE INDEX IF NOT EXISTS \"{ActiveBattleIndexName}\" " +
            "ON \"BattleSession\" (\"PlayerId\") WHERE \"Status\" = 'Started';",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
