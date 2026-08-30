using System.Security.Cryptography;
using System.Text.Json;
using ImmortalLoot.Server.Config;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public interface IServerRandomSource
{
    bool Roll(double probability);
    int Next(int maxExclusive);
}

public sealed class CryptoServerRandomSource : IServerRandomSource
{
    public bool Roll(double probability) => RandomNumberGenerator.GetInt32(1_000_000) < (int)(Math.Clamp(probability, 0, 1) * 1_000_000);
    public int Next(int maxExclusive) => maxExclusive > 0 ? RandomNumberGenerator.GetInt32(maxExclusive) : throw new ArgumentOutOfRangeException(nameof(maxExclusive));
}

public static class RealmBreakthroughStatuses
{
    public const string AdvancedStage = "AdvancedStage";
    public const string Failed = "Failed";
    public const string TribulationRequired = "TribulationRequired";
    public const string TrialAlreadyPending = "TrialAlreadyPending";
    public const string MaximumRealm = "MaximumRealm";
}

// Keep the original five constructor fields stable so existing RewardGrant JSON remains replayable.
public sealed record RealmBreakthroughResult(string RealmId, int RealmStage, bool Succeeded, string SpiritualRootId, bool Replayed)
{
    public string Status { get; init; } = string.Empty;
    public string TargetRealmId { get; init; } = string.Empty;
    public int RequiredLevel { get; init; }
    public long RequiredExperience { get; init; }
    public long RequiredMaterial { get; init; }
    public long MaterialSpent { get; init; }
    public long BreakthroughMaterial { get; init; }
}

public sealed record TribulationSettlement(string RealmId, int RealmStage, string SpiritualRootId);

internal enum PendingTribulationState
{
    Empty,
    Valid,
    Corrupt
}

internal readonly record struct PendingTribulationInspection(
    PendingTribulationState State,
    ServerRealmConfig? Target);

internal static class PendingTribulationInspector
{
    public static PendingTribulationInspection Inspect(Player player, IReadOnlyList<ServerRealmConfig> realms)
    {
        var hasToken = !string.IsNullOrEmpty(player.PendingTribulationToken);
        var hasTarget = !string.IsNullOrEmpty(player.PendingTribulationTargetRealmId);
        var hasReservedMaterial = player.PendingTribulationReservedMaterial != 0;
        var hasRequiredExperience = player.PendingTribulationRequiredExp != 0;
        if (!hasToken && !hasTarget && !hasReservedMaterial && !hasRequiredExperience)
            return new PendingTribulationInspection(PendingTribulationState.Empty, null);
        if (!hasToken || !hasTarget || !hasReservedMaterial || !hasRequiredExperience ||
            string.IsNullOrWhiteSpace(player.PendingTribulationToken) ||
            string.IsNullOrWhiteSpace(player.PendingTribulationTargetRealmId))
            return new PendingTribulationInspection(PendingTribulationState.Corrupt, null);

        var currentIndex = realms.ToList().FindIndex(value => value.Id == player.RealmId);
        if (currentIndex < 0 || currentIndex + 1 >= realms.Count ||
            player.RealmStage != realms[currentIndex].StageCount)
            return new PendingTribulationInspection(PendingTribulationState.Corrupt, null);
        var target = realms[currentIndex + 1];
        return string.Equals(player.PendingTribulationTargetRealmId, target.Id, StringComparison.Ordinal) &&
               player.PendingTribulationReservedMaterial == target.BreakthroughCost &&
               player.PendingTribulationRequiredExp == target.RequiredExp
            ? new PendingTribulationInspection(PendingTribulationState.Valid, target)
            : new PendingTribulationInspection(PendingTribulationState.Corrupt, null);
    }
}

public sealed class RealmAuthorityService(GameDbContext db, TaskService tasks, ServerGameConfigCatalog catalog, IServerRandomSource random)
{
    private IReadOnlyList<ServerRealmConfig> Realms => catalog.Realms;

    public async Task<RealmBreakthroughResult> BreakthroughAsync(Guid playerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var key = "realm:" + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<RealmBreakthroughResult>(prior.PayloadJson)
                         ?? throw new InvalidOperationException("Stored realm result is invalid.");
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }

        var player = await db.Players.SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken)
                     ?? throw new KeyNotFoundException("Player was not found.");
        var index = Realms.ToList().FindIndex(value => value.Id == player.RealmId);
        if (index < 0) throw new InvalidOperationException("Player realm is invalid.");
        var current = Realms[index];
        if (player.RealmStage < 1 || player.RealmStage > current.StageCount)
            throw new InvalidOperationException("Player realm stage is invalid.");

        var pending = PendingTribulationInspector.Inspect(player, Realms);
        if (pending.State == PendingTribulationState.Corrupt)
            throw new InvalidOperationException("Pending tribulation state requires recovery before another breakthrough.");

        RealmBreakthroughResult result;
        if (pending.State == PendingTribulationState.Valid)
        {
            result = Result(player, RealmBreakthroughStatuses.TrialAlreadyPending,
                targetRealmId: player.PendingTribulationTargetRealmId,
                requiredExperience: player.PendingTribulationRequiredExp,
                requiredMaterial: player.PendingTribulationReservedMaterial);
        }
        else if (player.RealmStage < current.StageCount)
        {
            result = ResolveMinorBreakthrough(player, current, key);
        }
        else if (index + 1 >= Realms.Count)
        {
            result = Result(player, RealmBreakthroughStatuses.MaximumRealm);
        }
        else
        {
            result = BeginMajorBreakthrough(player, Realms[index + 1], key);
        }

        var json = JsonSerializer.Serialize(result);
        db.RewardGrants.Add(new RewardGrant
        {
            PlayerId = playerId, IdempotencyKey = key, RewardType = "RealmBreakthrough", PayloadJson = json
        });
        db.RewardLogs.Add(new RewardLog
        {
            PlayerId = playerId, IdempotencyKey = key, RewardType = "RealmBreakthrough", PayloadJson = json
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TribulationSettlement?> ResolvePendingAfterBossVictoryAsync(
        Player player,
        CancellationToken cancellationToken)
    {
        var pending = PendingTribulationInspector.Inspect(player, Realms);
        if (pending.State != PendingTribulationState.Valid ||
            player.CultivationExperience < player.PendingTribulationRequiredExp)
            return null;
        var target = pending.Target!;

        player.CultivationExperience -= player.PendingTribulationRequiredExp;
        player.RealmId = target.Id;
        player.RealmStage = 1;
        player.PendingTribulationToken = string.Empty;
        player.PendingTribulationTargetRealmId = string.Empty;
        player.PendingTribulationReservedMaterial = 0;
        player.PendingTribulationRequiredExp = 0;
        var spiritualRootId = await GrantSpiritualRootAsync(player.Id, cancellationToken);
        await tasks.RecordAsync(player.Id, "Tribulation", 1, cancellationToken);
        return new TribulationSettlement(player.RealmId, player.RealmStage, spiritualRootId);
    }

    private RealmBreakthroughResult ResolveMinorBreakthrough(Player player, ServerRealmConfig current, string key)
    {
        var requiredExperience = ScaledRequirement(
            current.RequiredExp, catalog.RealmFormula.MinorExpScale, player.RealmStage, current.StageCount);
        var requiredMaterial = ScaledRequirement(
            current.BreakthroughCost, catalog.RealmFormula.MinorCostScale, player.RealmStage, current.StageCount);
        EnsureRequirements(player, current.RequiredLevel, requiredExperience, requiredMaterial);

        var succeeded = random.Roll(current.BreakthroughSuccessRate);
        var materialSpent = succeeded
            ? requiredMaterial
            : Math.Max(0L, (long)Math.Ceiling(requiredMaterial * catalog.RealmFormula.MinorFailureLossRatio));
        player.BreakthroughMaterial -= materialSpent;
        if (materialSpent > 0) AddMaterialLog(player.Id, -materialSpent, succeeded ? "RealmBreakthrough" : "RealmBreakthroughFailure", key);
        if (succeeded)
        {
            player.CultivationExperience -= requiredExperience;
            player.RealmStage++;
        }
        return Result(player, succeeded ? RealmBreakthroughStatuses.AdvancedStage : RealmBreakthroughStatuses.Failed,
            succeeded, current.RequiredLevel, requiredExperience, requiredMaterial, materialSpent);
    }

    private RealmBreakthroughResult BeginMajorBreakthrough(Player player, ServerRealmConfig target, string key)
    {
        EnsureRequirements(player, target.RequiredLevel, target.RequiredExp, target.BreakthroughCost);
        player.BreakthroughMaterial -= target.BreakthroughCost;
        player.PendingTribulationToken = Guid.NewGuid().ToString("N");
        player.PendingTribulationTargetRealmId = target.Id;
        player.PendingTribulationReservedMaterial = target.BreakthroughCost;
        player.PendingTribulationRequiredExp = target.RequiredExp;
        AddMaterialLog(player.Id, -target.BreakthroughCost, "RealmTribulationReserve", key);
        return Result(player, RealmBreakthroughStatuses.TribulationRequired,
            targetRealmId: target.Id, requiredLevel: target.RequiredLevel,
            requiredExperience: target.RequiredExp, requiredMaterial: target.BreakthroughCost,
            materialSpent: target.BreakthroughCost);
    }

    private static void EnsureRequirements(Player player, int requiredLevel, long requiredExperience, long requiredMaterial)
    {
        if (player.Level < requiredLevel)
            throw new InvalidOperationException($"Player level {player.Level} is below realm requirement {requiredLevel}.");
        if (player.CultivationExperience < requiredExperience)
            throw new InvalidOperationException("Insufficient cultivation experience.");
        if (player.BreakthroughMaterial < requiredMaterial)
            throw new InvalidOperationException("Insufficient breakthrough material.");
    }

    private async Task<string> GrantSpiritualRootAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var levels = await db.PlayerSpiritualRoots.Where(value => value.PlayerId == playerId)
            .ToDictionaryAsync(value => value.RootId, cancellationToken);
        var candidates = catalog.SpiritualRoots
            .Where(value => !levels.TryGetValue(value.Id, out var progress) || progress.Level < value.MaxLevel)
            .ToArray();
        if (candidates.Length == 0) return string.Empty;
        var spiritualRootId = candidates[random.Next(candidates.Length)].Id;
        var root = await db.PlayerSpiritualRoots.SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.RootId == spiritualRootId, cancellationToken);
        if (root is null)
        {
            root = new PlayerSpiritualRoot { PlayerId = playerId, RootId = spiritualRootId };
            db.PlayerSpiritualRoots.Add(root);
        }
        root.Level++;
        return spiritualRootId;
    }

    private void AddMaterialLog(Guid playerId, long delta, string reason, string referenceId)
    {
        db.ItemLogs.Add(new ItemLog
        {
            PlayerId = playerId,
            ItemId = "breakthrough_material",
            Delta = checked((int)delta),
            Reason = reason,
            ReferenceId = referenceId
        });
    }

    private static long ScaledRequirement(long configured, double scale, int realmStage, int stageCount) =>
        Math.Max(1L, (long)Math.Ceiling(configured * scale * realmStage / stageCount));

    private static RealmBreakthroughResult Result(
        Player player,
        string status,
        bool succeeded = false,
        int requiredLevel = 0,
        long requiredExperience = 0,
        long requiredMaterial = 0,
        long materialSpent = 0,
        string targetRealmId = "") =>
        new(player.RealmId, player.RealmStage, succeeded, string.Empty, false)
        {
            Status = status,
            TargetRealmId = targetRealmId,
            RequiredLevel = requiredLevel,
            RequiredExperience = requiredExperience,
            RequiredMaterial = requiredMaterial,
            MaterialSpent = materialSpent,
            BreakthroughMaterial = player.BreakthroughMaterial
        };
}
