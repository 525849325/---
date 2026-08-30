using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmortalLoot.Server.Persistence;

public abstract class EntityBase
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("Account")]
public sealed class Account : EntityBase
{
    [MaxLength(128)] public string ExternalAccountId { get; set; } = string.Empty;
    [MaxLength(32)] public string Provider { get; set; } = "guest";
    public DateTime LastLoginTimeUtc { get; set; }
}

[Table("AuthSession")]
public sealed class AuthSession : EntityBase
{
    public Guid AccountId { get; set; }
    public Guid PlayerId { get; set; }
    [MaxLength(64)] public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}

[Table("Player")]
public sealed class Player : EntityBase
{
    public Guid AccountId { get; set; }
    [MaxLength(32)] public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public long Exp { get; set; }
    [MaxLength(64)] public string RealmId { get; set; } = "realm_body_tempering";
    public int RealmStage { get; set; } = 1;
    public long Power { get; set; }
    public DateTime LastLoginTimeUtc { get; set; }
    public DateTime LastOfflineTimeUtc { get; set; }
}

[Table("PlayerStats")]
public sealed class PlayerStats : EntityBase
{
    public Guid PlayerId { get; set; }
    public string StatsJson { get; set; } = "{}";
}

[Table("PlayerInventory")]
public sealed class PlayerInventory : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string ItemId { get; set; } = string.Empty;
    public int Count { get; set; }
    [MaxLength(32)] public string Category { get; set; } = string.Empty;
}

[Table("PlayerEquipment")]
public sealed class PlayerEquipment : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(64)] public string InstanceId { get; set; } = string.Empty;
    [MaxLength(128)] public string BaseId { get; set; } = string.Empty;
    [MaxLength(32)] public string Slot { get; set; } = string.Empty;
    public int Level { get; set; }
    [MaxLength(32)] public string Quality { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool IsEquipped { get; set; }
    [MaxLength(32)] public string EquippedSlot { get; set; } = string.Empty;
    public string InstanceJson { get; set; } = "{}";
}

[Table("PlayerSkill")]
public sealed class PlayerSkill : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string SkillId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public bool Equipped { get; set; }
}

[Table("PlayerCultivation")]
public sealed class PlayerCultivation : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string MethodId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    [MaxLength(32)] public string EquippedSlot { get; set; } = string.Empty;
}

[Table("PlayerSpiritualRoot")]
public sealed class PlayerSpiritualRoot : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string RootId { get; set; } = string.Empty;
    public int Level { get; set; }
}

[Table("PlayerStage")]
public sealed class PlayerStage : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string StageId { get; set; } = string.Empty;
    public bool Cleared { get; set; }
    public DateTime? FirstClearTimeUtc { get; set; }
}

[Table("PlayerCurrency")]
public sealed class PlayerCurrency : EntityBase
{
    public Guid PlayerId { get; set; }
    public long SoftCurrency { get; set; }
    public long PremiumCurrency { get; set; }
    public long Version { get; set; }
}

[Table("PlayerMail")]
public sealed class PlayerMail : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string AttachmentJson { get; set; } = "[]";
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRead { get; set; }
    public bool IsClaimed { get; set; }
}

[Table("PlayerTask")]
public sealed class PlayerTask : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string TaskId { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool IsClaimed { get; set; }
    [MaxLength(16)] public string UtcDate { get; set; } = string.Empty;
}

[Table("PlayerPurchase")]
public sealed class PlayerPurchase : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string ProductId { get; set; } = string.Empty;
    [MaxLength(32)] public string PeriodKey { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public DateTime LastPurchaseTimeUtc { get; set; }
}

[Table("ShopPurchase")]
public sealed class ShopPurchase : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string ProductId { get; set; } = string.Empty;
    [MaxLength(160)] public string IdempotencyKey { get; set; } = string.Empty;
    public int Quantity { get; set; }
    [MaxLength(32)] public string Currency { get; set; } = string.Empty;
    public long TotalPrice { get; set; }
    public long BalanceAfter { get; set; }
}

[Table("PaymentOrder")]
public sealed class PaymentOrder : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string OrderNo { get; set; } = string.Empty;
    [MaxLength(128)] public string ProductId { get; set; } = string.Empty;
    [MaxLength(32)] public string Status { get; set; } = "Created";
    [MaxLength(128)] public string ProviderTransactionId { get; set; } = string.Empty;
    [MaxLength(32)] public string Provider { get; set; } = string.Empty;
    public long AmountMinorUnits { get; set; }
    [MaxLength(8)] public string CurrencyCode { get; set; } = "CNY";
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? GrantedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
}

[Table("RankingSnapshot")]
public sealed class RankingSnapshot : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(32)] public string RankingType { get; set; } = string.Empty;
    [MaxLength(32)] public string PeriodKey { get; set; } = string.Empty;
    public long Score { get; set; }
    public int Rank { get; set; }
}

[Table("BattleSession")]
public sealed class BattleSession : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string StageId { get; set; } = string.Empty;
    [MaxLength(128)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(32)] public string Status { get; set; } = "Started";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public long RewardSoftCurrency { get; set; }
    public long RewardExp { get; set; }
    [MaxLength(64)] public string RewardEquipmentInstanceId { get; set; } = string.Empty;
}

[Table("RewardGrant")]
public sealed class RewardGrant : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(160)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(64)] public string RewardType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

[Table("CurrencyLog")]
public sealed class CurrencyLog : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(32)] public string Currency { get; set; } = string.Empty;
    public long Delta { get; set; }
    public long BalanceAfter { get; set; }
    [MaxLength(64)] public string Reason { get; set; } = string.Empty;
    [MaxLength(160)] public string ReferenceId { get; set; } = string.Empty;
}

[Table("ItemLog")]
public sealed class ItemLog : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string ItemId { get; set; } = string.Empty;
    public int Delta { get; set; }
    [MaxLength(64)] public string Reason { get; set; } = string.Empty;
    [MaxLength(160)] public string ReferenceId { get; set; } = string.Empty;
}

[Table("EquipmentLog")]
public sealed class EquipmentLog : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(64)] public string InstanceId { get; set; } = string.Empty;
    [MaxLength(64)] public string Action { get; set; } = string.Empty;
    [MaxLength(160)] public string ReferenceId { get; set; } = string.Empty;
}

[Table("PaymentLog")]
public sealed class PaymentLog : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(128)] public string OrderNo { get; set; } = string.Empty;
    [MaxLength(64)] public string Action { get; set; } = string.Empty;
    public string DetailJson { get; set; } = "{}";
}

[Table("RewardLog")]
public sealed class RewardLog : EntityBase
{
    public Guid PlayerId { get; set; }
    [MaxLength(160)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(64)] public string RewardType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

[Table("BattleLog")]
public sealed class BattleLog : EntityBase
{
    public Guid PlayerId { get; set; }
    public Guid BattleSessionId { get; set; }
    [MaxLength(128)] public string StageId { get; set; } = string.Empty;
    [MaxLength(32)] public string Result { get; set; } = string.Empty;
    public string DetailJson { get; set; } = "{}";
}
