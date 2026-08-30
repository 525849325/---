using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

        if (db.Database.IsSqlite() &&
            !await HasColumnAsync(db, "Player", "CultivationExperience", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Player\" ADD COLUMN \"CultivationExperience\" INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Player\" SET \"CultivationExperience\" = MAX(\"Exp\", 0);",
                cancellationToken);
        }

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

    private static async Task<bool> HasColumnAsync(
        GameDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            $"SELECT COUNT(*) FROM pragma_table_info('{tableName.Replace("'", "''", StringComparison.Ordinal)}') " +
            "WHERE name = $columnName;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$columnName";
        parameter.Value = columnName;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }
}
