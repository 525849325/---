using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public sealed record EquipResult(string InstanceId, string Slot, bool Replaced);
public sealed record DecomposeResult(string InstanceId, long SoftCurrency, int Essence, bool Replayed);
public sealed record EnhanceResult(string InstanceId, int Level, long SoftCurrencyCost, bool Replayed);

public sealed class EquipmentAuthorityService(GameDbContext db, CurrencyService currencies, TaskService tasks, ServerGameConfigCatalog catalog)
{
    public async Task<EquipResult> EquipAsync(Guid playerId, string instanceId, CancellationToken cancellationToken)
    {
        var equipment = await db.PlayerEquipment.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.InstanceId == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        var equipped = await db.PlayerEquipment.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IsEquipped && value.EquippedSlot == equipment.Slot, cancellationToken);
        var replaced = equipped is not null && equipped.Id != equipment.Id;
        if (replaced) { equipped!.IsEquipped = false; equipped.EquippedSlot = string.Empty; }
        equipment.IsEquipped = true;
        equipment.EquippedSlot = equipment.Slot;
        var player = await db.Players.SingleAsync(value => value.Id == playerId, cancellationToken);
        var qualityPower = equipment.Quality switch { "Fine" => 20, "Rare" => 40, "Epic" => 80, "Legendary" => 160, "Mythic" => 320, _ => 10 };
        player.Power = player.Level * 100L + await db.PlayerEquipment.Where(value => value.PlayerId == playerId && value.IsEquipped && value.Id != equipment.Id).SumAsync(value => (long)value.Level * 10, cancellationToken) + equipment.Level * 10L + qualityPower;
        db.EquipmentLogs.Add(new EquipmentLog { PlayerId = playerId, InstanceId = instanceId, Action = "Equip", ReferenceId = equipment.Slot });
        await db.SaveChangesAsync(cancellationToken);
        return new(instanceId, equipment.Slot, replaced);
    }

    public async Task<DecomposeResult> DecomposeAsync(Guid playerId, string instanceId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var key = "decompose:" + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<DecomposeResult>(prior.PayloadJson)!;
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }
        var equipment = await db.PlayerEquipment.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.InstanceId == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        if (equipment.IsLocked || equipment.IsEquipped) throw new InvalidOperationException("Locked or equipped items cannot be decomposed.");
        var multiplier = equipment.Quality switch { "Fine" => 2, "Rare" => 4, "Epic" => 8, "Legendary" => 16, "Mythic" => 32, _ => 1 };
        var soft = checked((long)Math.Max(1, equipment.Level) * 10L * multiplier);
        var essence = Math.Max(1, multiplier / 2);
        await currencies.ChangeAsync(playerId, GameCurrency.SoftCurrency, soft, "EquipmentDecompose", instanceId, cancellationToken);
        var result = new DecomposeResult(instanceId, soft, essence, false);
        var json = JsonSerializer.Serialize(result);
        db.PlayerEquipment.Remove(equipment);
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = key, RewardType = "EquipmentDecompose", PayloadJson = json });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = key, RewardType = "EquipmentDecompose", PayloadJson = json });
        db.EquipmentLogs.Add(new EquipmentLog { PlayerId = playerId, InstanceId = instanceId, Action = "Decompose", ReferenceId = key });
        db.ItemLogs.Add(new ItemLog { PlayerId = playerId, ItemId = "equipment_essence", Delta = essence, Reason = "EquipmentDecompose", ReferenceId = instanceId });
        await tasks.RecordAsync(playerId, "EquipmentDecompose", 1, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<EnhanceResult> EnhanceAsync(Guid playerId, string instanceId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var key = "enhance:" + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<EnhanceResult>(prior.PayloadJson)!;
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }
        var equipment = await db.PlayerEquipment.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.InstanceId == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        var cost = checked((long)catalog.InventoryFormula.BaseGoldPerLevel * Math.Max(1, equipment.Level));
        await currencies.ChangeAsync(playerId, GameCurrency.SoftCurrency, -cost, "EquipmentEnhance", instanceId, cancellationToken);
        equipment.Level = checked(equipment.Level + 1);
        var result = new EnhanceResult(instanceId, equipment.Level, cost, false);
        var json = JsonSerializer.Serialize(result);
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = key, RewardType = "EquipmentEnhance", PayloadJson = json });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = key, RewardType = "EquipmentEnhance", PayloadJson = json });
        db.EquipmentLogs.Add(new EquipmentLog { PlayerId = playerId, InstanceId = instanceId, Action = "Enhance", ReferenceId = key });
        await tasks.RecordAsync(playerId, "EquipmentEnhance", 1, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
