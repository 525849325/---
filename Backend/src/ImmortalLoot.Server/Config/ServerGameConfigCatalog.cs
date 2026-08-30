using System.Text.Json;

namespace ImmortalLoot.Server.Config;

public sealed record ServerRealmConfig(string Id, string Name, int Order, int StageCount, int RequiredLevel, long RequiredExp, long BreakthroughCost, double BreakthroughSuccessRate);
public sealed record ServerShopItemConfig(string Id, string ShopId, string ItemId, string Currency, long Price, string LimitType, int LimitCount, string RefreshType, string UnlockCondition);
public sealed record ServerCommercialProductConfig(string Id, string Name, string Type, long AmountMinorUnits, string CurrencyCode, long ImmediatePremium, long DailyPremium, int DurationDays, int AfkCapBonusHours, int QuickAfkBonus, int LifetimeLimit, string UnlockRealmId, string RewardItemId, int RewardItemCount);
public sealed record ServerActivityConfig(string Id, string Name, string Type, DateTime StartTimeUtc, DateTime EndTimeUtc, string Condition, double RewardModifier);
public sealed record ServerEquipmentConfig(string Id, string DisplayName, string Slot, IReadOnlyList<string> AffixPool);
public sealed record ServerAffixConfig(string Id, string DisplayName, double MinValue, double MaxValue, int Weight, string ConflictGroup);
public sealed record ServerQualityRule(string Quality, int MinAffixes, int MaxAffixes);
public sealed record ServerStageConfig(string Id, int Chapter, int StageNumber, long RecommendedPower, long RewardExp, long RewardSoftCurrency, long FirstClearPremiumCurrency, string DropTableId, string FirstClearDropTableId, string UnlockCondition, bool IsBossStage);
public sealed record ServerDropEntryConfig(string ItemId, int Weight, int MinCount, int MaxCount, string MinQuality, string MaxQuality, string Condition);
public sealed record ServerDropTableConfig(string Id, string Name, int RollCount, IReadOnlyList<ServerDropEntryConfig> Entries);
public sealed record ServerSpiritualRootConfig(string Id, string Name, string Element, int MaxLevel);
public sealed record ServerAfkConfig(int MaximumOfflineHours, double ExperiencePerMinute, double SoftCurrencyPerMinute, double MaterialPerMinute, double MinutesPerEquipmentRoll, int QuickAfkHours, int FreeQuickAfkPerDay);
public sealed record ServerInventoryFormula(int BaseGoldPerLevel, double BaseMaterialPerLevel, IReadOnlyList<double> QualityMultipliers, IReadOnlyList<int> EssenceByQuality);
public sealed record ServerTaskConfig(string Id, string EventType, int Target, int ActivityPoints, long SoftCurrency, long PremiumCurrency, IReadOnlyDictionary<string, int> Items);
public sealed record ServerActivityChestConfig(int RequiredPoints, long SoftCurrency, long PremiumCurrency, IReadOnlyDictionary<string, int> Items);

public sealed class ServerGameConfigCatalog
{
    public IReadOnlyList<ServerRealmConfig> Realms { get; }
    public IReadOnlyList<ServerShopItemConfig> ShopItems { get; }
    public IReadOnlyList<ServerCommercialProductConfig> CommercialProducts { get; }
    public IReadOnlyList<ServerActivityConfig> Activities { get; }
    public IReadOnlyList<ServerEquipmentConfig> Equipment { get; }
    public IReadOnlyDictionary<string, ServerAffixConfig> Affixes { get; }
    public IReadOnlyDictionary<string, ServerQualityRule> QualityRules { get; }
    public IReadOnlyList<ServerStageConfig> Stages { get; }
    public IReadOnlyDictionary<string, ServerDropTableConfig> DropTables { get; }
    public IReadOnlyList<ServerSpiritualRootConfig> SpiritualRoots { get; }
    public ServerAfkConfig Afk { get; }
    public ServerInventoryFormula InventoryFormula { get; }
    public IReadOnlyList<ServerTaskConfig> Tasks { get; }
    public IReadOnlyList<ServerActivityChestConfig> ActivityChests { get; }
    public string SourceDirectory { get; }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ServerGameConfigCatalog(string directory)
    {
        SourceDirectory = directory;
        Realms = Load<RealmFile>(directory, "realms.json").Realms;
        ShopItems = Load<ShopFile>(directory, "shop.json").Items;
        CommercialProducts = Load<CommercialFile>(directory, "commercial_products.json").Products;
        Activities = Load<ActivityFile>(directory, "activities.json").Activities;
        Equipment = Load<EquipmentFile>(directory, "equipment.json").Equipment;
        Affixes = Unique(Load<AffixFile>(directory, "affixes.json").Affixes, value => value.Id, "affix");
        QualityRules = Unique(Load<QualityFile>(directory, "quality_rules.json").Rules, value => value.Quality, "quality rule");
        Stages = Load<StageFile>(directory, "stages.json").Stages;
        DropTables = Unique(Load<DropTableFile>(directory, "drop_tables.json").DropTables, value => value.Id, "drop table");
        SpiritualRoots = Load<RootFile>(directory, "spiritual_roots.json").SpiritualRoots;
        var afk = Load<AfkFile>(directory, "afk.json");
        Afk = new ServerAfkConfig(afk.MaximumOfflineHours, afk.ExperiencePerMinute, afk.SoftCurrencyPerMinute, afk.MaterialPerMinute, afk.MinutesPerEquipmentRoll, afk.QuickAfkHours, afk.FreeQuickAfkPerDay);
        var inventory = Load<InventoryFormulaFile>(directory, "inventory_formula.json");
        InventoryFormula = new ServerInventoryFormula(inventory.BaseGoldPerLevel, inventory.BaseMaterialPerLevel, inventory.QualityMultipliers, inventory.EssenceByQuality);
        var tasks = Load<TaskFile>(directory, "tasks.json");
        Tasks = tasks.Tasks;
        ActivityChests = tasks.ActivityChests;
        Validate();
    }

    public static ServerGameConfigCatalog LoadDefault()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Config"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Game", "Resources", "Config")
        };
        foreach (var candidate in candidates) if (File.Exists(Path.Combine(candidate, "realms.json"))) return new ServerGameConfigCatalog(Path.GetFullPath(candidate));
        throw new DirectoryNotFoundException("Shared game config directory was not found beside the server or in the repository.");
    }

    private void Validate()
    {
        if (Realms.Count != 10 || Realms.Select(value => value.Order).Distinct().Count() != Realms.Count) throw new InvalidDataException("Realm config must contain ten unique orders.");
        if (Stages.Count != 10 || Stages.Select(value => value.Id).Distinct().Count() != Stages.Count) throw new InvalidDataException("Stage config must contain chapter 1-1 through 1-10.");
        if (Stages.Any(value => value.RecommendedPower <= 0 || value.RewardExp <= 0 || value.RewardSoftCurrency <= 0 || value.FirstClearPremiumCurrency < 0)) throw new InvalidDataException("Stage rewards must be configured with positive base values.");
        foreach (var stage in Stages) if (!DropTables.ContainsKey(stage.DropTableId)) throw new InvalidDataException($"Stage '{stage.Id}' references missing drop table.");
        foreach (var table in DropTables.Values)
        {
            if (table.RollCount <= 0 || table.Entries.Count == 0 || table.Entries.Any(value => value.Weight <= 0 || value.MinCount <= 0 || value.MaxCount < value.MinCount)) throw new InvalidDataException($"Drop table '{table.Id}' is invalid.");
            foreach (var entry in table.Entries.Where(value => Equipment.Any(item => item.Id == value.ItemId)))
                if (!QualityRules.ContainsKey(entry.MinQuality) || !QualityRules.ContainsKey(entry.MaxQuality)) throw new InvalidDataException($"Drop table '{table.Id}' has an invalid equipment quality.");
        }
        if (SpiritualRoots.Count != 9) throw new InvalidDataException("Spiritual root config must contain nine roots.");
        if (Afk.MaximumOfflineHours <= 0 || Afk.ExperiencePerMinute < 0 || Afk.SoftCurrencyPerMinute < 0 || Afk.MaterialPerMinute < 0 || Afk.MinutesPerEquipmentRoll <= 0) throw new InvalidDataException("AFK config is invalid.");
        if (InventoryFormula.BaseGoldPerLevel <= 0 || InventoryFormula.QualityMultipliers.Count != 6 || InventoryFormula.EssenceByQuality.Count != 6) throw new InvalidDataException("Inventory formula config is invalid.");
        if (Tasks.Count == 0 || Tasks.Any(value => value.Target <= 0 || value.ActivityPoints <= 0) || Tasks.Select(value => value.Id).Distinct().Count() != Tasks.Count) throw new InvalidDataException("Task config is invalid.");
        if (ActivityChests.Count == 0 || ActivityChests.Any(value => value.RequiredPoints <= 0) || ActivityChests.Select(value => value.RequiredPoints).Distinct().Count() != ActivityChests.Count) throw new InvalidDataException("Activity chest config is invalid.");
        foreach (var item in Equipment)
            foreach (var affixId in item.AffixPool)
                if (!Affixes.ContainsKey(affixId)) throw new InvalidDataException($"Equipment '{item.Id}' references missing affix '{affixId}'.");
        foreach (var shop in ShopItems)
            if (shop.UnlockCondition.Length > 0 && Realms.All(value => value.Id != shop.UnlockCondition)) throw new InvalidDataException($"Shop item '{shop.Id}' references missing realm.");
        foreach (var activity in Activities)
            if (activity.EndTimeUtc <= activity.StartTimeUtc || activity.RewardModifier <= 0) throw new InvalidDataException($"Activity '{activity.Id}' has an invalid window or modifier.");
    }

    private static T Load<T>(string directory, string file) where T : VersionedFile
    {
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, file)), JsonOptions) ?? throw new InvalidDataException(file + " is empty.");
        if (value.SchemaVersion != 1) throw new InvalidDataException(file + " schemaVersion is unsupported.");
        return value;
    }

    private static IReadOnlyDictionary<string, T> Unique<T>(IReadOnlyList<T> values, Func<T, string> key, string label)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in values) if (!result.TryAdd(key(value), value)) throw new InvalidDataException($"Duplicate {label} '{key(value)}'.");
        return result;
    }

    private abstract record VersionedFile(int SchemaVersion);
    private sealed record RealmFile(int SchemaVersion, List<ServerRealmConfig> Realms) : VersionedFile(SchemaVersion);
    private sealed record ShopFile(int SchemaVersion, List<ServerShopItemConfig> Items) : VersionedFile(SchemaVersion);
    private sealed record CommercialFile(int SchemaVersion, List<ServerCommercialProductConfig> Products) : VersionedFile(SchemaVersion);
    private sealed record ActivityFile(int SchemaVersion, List<ServerActivityConfig> Activities) : VersionedFile(SchemaVersion);
    private sealed record EquipmentFile(int SchemaVersion, List<ServerEquipmentConfig> Equipment) : VersionedFile(SchemaVersion);
    private sealed record AffixFile(int SchemaVersion, List<ServerAffixConfig> Affixes) : VersionedFile(SchemaVersion);
    private sealed record QualityFile(int SchemaVersion, List<ServerQualityRule> Rules) : VersionedFile(SchemaVersion);
    private sealed record StageFile(int SchemaVersion, List<ServerStageConfig> Stages) : VersionedFile(SchemaVersion);
    private sealed record DropTableFile(int SchemaVersion, List<ServerDropTableConfig> DropTables) : VersionedFile(SchemaVersion);
    private sealed record RootFile(int SchemaVersion, List<ServerSpiritualRootConfig> SpiritualRoots) : VersionedFile(SchemaVersion);
    private sealed record AfkFile(int SchemaVersion, int MaximumOfflineHours, double ExperiencePerMinute, double SoftCurrencyPerMinute, double MaterialPerMinute, double MinutesPerEquipmentRoll, int QuickAfkHours, int FreeQuickAfkPerDay) : VersionedFile(SchemaVersion);
    private sealed record InventoryFormulaFile(int SchemaVersion, int BaseGoldPerLevel, double BaseMaterialPerLevel, List<double> QualityMultipliers, List<int> EssenceByQuality) : VersionedFile(SchemaVersion);
    private sealed record TaskFile(int SchemaVersion, List<ServerTaskConfig> Tasks, List<ServerActivityChestConfig> ActivityChests) : VersionedFile(SchemaVersion);
}
