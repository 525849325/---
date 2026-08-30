using System.Collections.Concurrent;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public enum RankingType { Power, Realm, Stage }
public sealed record RankingEntry(int Rank, Guid PlayerId, string Nickname, long Score);
public sealed record RankingPage(RankingType Type, string PeriodKey, int Page, int PageSize, int Total, IReadOnlyList<RankingEntry> Entries, RankingEntry? Self);

public interface IRankingCache
{
    bool TryGet(RankingType type, string periodKey, out IReadOnlyList<RankingEntry> entries);
    void Set(RankingType type, string periodKey, IReadOnlyList<RankingEntry> entries);
    void Remove(RankingType type, string periodKey);
}

public sealed class MemoryRankingCache : IRankingCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<RankingEntry>> _entries = new();
    public bool TryGet(RankingType type, string periodKey, out IReadOnlyList<RankingEntry> entries) => _entries.TryGetValue(Key(type, periodKey), out entries!);
    public void Set(RankingType type, string periodKey, IReadOnlyList<RankingEntry> entries) => _entries[Key(type, periodKey)] = entries;
    public void Remove(RankingType type, string periodKey) => _entries.TryRemove(Key(type, periodKey), out _);
    private static string Key(RankingType type, string periodKey) => type + ":" + periodKey;
}

public sealed class RankingService(GameDbContext db, IRankingCache cache, IServerClock clock, ServerGameConfigCatalog catalog)
{

    public string CurrentPeriodKey => "permanent";
    public string CurrentWeeklyPeriodKey
    {
        get
        {
            var day = (7 + (int)clock.UtcNow.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return "week:" + clock.UtcNow.Date.AddDays(-day).ToString("yyyy-MM-dd");
        }
    }

    public async Task RefreshAsync(RankingType type, string? periodKey, CancellationToken cancellationToken)
    {
        periodKey = NormalizePeriod(periodKey);
        var players = await db.Players.AsNoTracking().Select(value => new { value.Id, value.Power, value.RealmId, value.RealmStage }).ToListAsync(cancellationToken);
        Dictionary<Guid, long>? stageScores = null;
        if (type == RankingType.Stage)
        {
            var cleared = await db.PlayerStages.AsNoTracking().Where(value => value.Cleared).Select(value => new { value.PlayerId, value.StageId }).ToListAsync(cancellationToken);
            stageScores = cleared.GroupBy(value => value.PlayerId).ToDictionary(group => group.Key, group => group.Max(value => StageScore(value.StageId)));
        }
        var ranked = players.Select(player => new { player.Id, Score = Score(type, player.Power, player.RealmId, player.RealmStage, stageScores, player.Id) })
            .OrderByDescending(value => value.Score).ThenBy(value => value.Id).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previous = await db.RankingSnapshots.Where(value => value.RankingType == type.ToString() && value.PeriodKey == periodKey).ToListAsync(cancellationToken);
        db.RankingSnapshots.RemoveRange(previous);
        for (var index = 0; index < ranked.Count; index++)
            db.RankingSnapshots.Add(new RankingSnapshot { PlayerId = ranked[index].Id, RankingType = type.ToString(), PeriodKey = periodKey, Score = ranked[index].Score, Rank = index + 1 });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cache.Remove(type, periodKey);
    }

    public async Task<RankingPage> GetPageAsync(RankingType type, string? periodKey, int page, int pageSize, Guid? selfPlayerId, CancellationToken cancellationToken)
    {
        periodKey = NormalizePeriod(periodKey);
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (!await db.RankingSnapshots.AnyAsync(value => value.RankingType == type.ToString() && value.PeriodKey == periodKey, cancellationToken))
            await RefreshAsync(type, periodKey, cancellationToken);
        if (!cache.TryGet(type, periodKey, out var entries))
        {
            entries = await (from snapshot in db.RankingSnapshots.AsNoTracking()
                             join player in db.Players.AsNoTracking() on snapshot.PlayerId equals player.Id
                             where snapshot.RankingType == type.ToString() && snapshot.PeriodKey == periodKey
                             orderby snapshot.Rank
                             select new RankingEntry(snapshot.Rank, snapshot.PlayerId, player.Nickname, snapshot.Score)).ToListAsync(cancellationToken);
            cache.Set(type, periodKey, entries);
        }
        var slice = entries.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var self = selfPlayerId.HasValue ? entries.SingleOrDefault(value => value.PlayerId == selfPlayerId.Value) : null;
        return new RankingPage(type, periodKey, page, pageSize, entries.Count, slice, self);
    }

    private string NormalizePeriod(string? periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey) || periodKey.Equals("permanent", StringComparison.OrdinalIgnoreCase)) return CurrentPeriodKey;
        if (periodKey.Equals("weekly", StringComparison.OrdinalIgnoreCase) || periodKey.Equals("week", StringComparison.OrdinalIgnoreCase)) return CurrentWeeklyPeriodKey;
        if (periodKey.StartsWith("week:", StringComparison.Ordinal)) return periodKey;
        throw new ArgumentException("Ranking period must be permanent or weekly.");
    }
    private long Score(RankingType type, long power, string realmId, int realmStage, Dictionary<Guid, long>? stageScores, Guid playerId = default) => type switch
    {
        RankingType.Power => power,
        RankingType.Realm => (catalog.Realms.SingleOrDefault(value => value.Id == realmId)?.Order ?? 0L) * 100L + Math.Clamp(realmStage, 1, 10),
        RankingType.Stage => stageScores is not null && stageScores.TryGetValue(playerId, out var value) ? value : 0,
        _ => 0
    };

    private static long StageScore(string stageId)
    {
        var parts = stageId.Split('_');
        return parts.Length >= 3 && int.TryParse(parts[^2], out var chapter) && int.TryParse(parts[^1], out var stage) ? chapter * 10000L + stage : 0;
    }
}
