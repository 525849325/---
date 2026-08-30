using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<PlayerInventory> PlayerInventories => Set<PlayerInventory>();
    public DbSet<PlayerEquipment> PlayerEquipment => Set<PlayerEquipment>();
    public DbSet<PlayerSkill> PlayerSkills => Set<PlayerSkill>();
    public DbSet<PlayerCultivation> PlayerCultivations => Set<PlayerCultivation>();
    public DbSet<PlayerSpiritualRoot> PlayerSpiritualRoots => Set<PlayerSpiritualRoot>();
    public DbSet<PlayerStage> PlayerStages => Set<PlayerStage>();
    public DbSet<PlayerCurrency> PlayerCurrencies => Set<PlayerCurrency>();
    public DbSet<PlayerMail> PlayerMails => Set<PlayerMail>();
    public DbSet<PlayerTask> PlayerTasks => Set<PlayerTask>();
    public DbSet<PlayerPurchase> PlayerPurchases => Set<PlayerPurchase>();
    public DbSet<ShopPurchase> ShopPurchases => Set<ShopPurchase>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<RankingSnapshot> RankingSnapshots => Set<RankingSnapshot>();
    public DbSet<BattleSession> BattleSessions => Set<BattleSession>();
    public DbSet<RewardGrant> RewardGrants => Set<RewardGrant>();
    public DbSet<CurrencyLog> CurrencyLogs => Set<CurrencyLog>();
    public DbSet<ItemLog> ItemLogs => Set<ItemLog>();
    public DbSet<EquipmentLog> EquipmentLogs => Set<EquipmentLog>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<RewardLog> RewardLogs => Set<RewardLog>();
    public DbSet<BattleLog> BattleLogs => Set<BattleLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().HasIndex(value => new { value.Provider, value.ExternalAccountId }).IsUnique();
        modelBuilder.Entity<AuthSession>().HasIndex(value => value.TokenHash).IsUnique();
        modelBuilder.Entity<Player>().HasIndex(value => value.AccountId).IsUnique();
        modelBuilder.Entity<PlayerCurrency>().HasIndex(value => value.PlayerId).IsUnique();
        modelBuilder.Entity<PlayerEquipment>().HasIndex(value => value.InstanceId).IsUnique();
        modelBuilder.Entity<PlayerInventory>().HasIndex(value => new { value.PlayerId, value.ItemId, value.Category }).IsUnique();
        modelBuilder.Entity<PlayerStage>().HasIndex(value => new { value.PlayerId, value.StageId }).IsUnique();
        modelBuilder.Entity<PlayerSkill>().HasIndex(value => new { value.PlayerId, value.SkillId }).IsUnique();
        modelBuilder.Entity<PlayerCultivation>().HasIndex(value => new { value.PlayerId, value.MethodId }).IsUnique();
        modelBuilder.Entity<PlayerSpiritualRoot>().HasIndex(value => new { value.PlayerId, value.RootId }).IsUnique();
        modelBuilder.Entity<PlayerTask>().HasIndex(value => new { value.PlayerId, value.TaskId, value.UtcDate }).IsUnique();
        modelBuilder.Entity<PlayerPurchase>().HasIndex(value => new { value.PlayerId, value.ProductId, value.PeriodKey }).IsUnique();
        modelBuilder.Entity<ShopPurchase>().HasIndex(value => new { value.PlayerId, value.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<PaymentOrder>().HasIndex(value => value.OrderNo).IsUnique();
        modelBuilder.Entity<PaymentOrder>().HasIndex(value => new { value.Provider, value.ProviderTransactionId }).IsUnique().HasFilter("\"ProviderTransactionId\" <> ''");
        modelBuilder.Entity<BattleSession>().HasIndex(value => new { value.PlayerId, value.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<RewardGrant>().HasIndex(value => new { value.PlayerId, value.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<RankingSnapshot>().HasIndex(value => new { value.PlayerId, value.RankingType, value.PeriodKey }).IsUnique();
        modelBuilder.Entity<RankingSnapshot>().HasIndex(value => new { value.RankingType, value.PeriodKey, value.Rank });
        modelBuilder.Entity<RewardLog>().HasIndex(value => new { value.PlayerId, value.IdempotencyKey }).IsUnique();
    }
}
