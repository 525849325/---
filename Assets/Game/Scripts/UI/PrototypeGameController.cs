using System;
using ImmortalLoot.Battle;
using ImmortalLoot.AFK;
using ImmortalLoot.Analytics;
using ImmortalLoot.Character;
using ImmortalLoot.Core;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using ImmortalLoot.Drop;
using ImmortalLoot.Inventory;
using ImmortalLoot.Network;
using ImmortalLoot.Cultivation;
using ImmortalLoot.Realm;
using ImmortalLoot.Debugging;
using ImmortalLoot.Player;
using ImmortalLoot.Payment;
using ImmortalLoot.Stage;
using ImmortalLoot.Settings;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeGameController : MonoBehaviour
    {
        [SerializeField] private Text statusText;
        [SerializeField] private Text lootText;
        [SerializeField] private Slider enemyHealth;
        [SerializeField] private Text profileText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text guideText;
        [SerializeField] private Button equipLatestButton;
        private AutoBattleEngine _battle;
        private EquipmentGenerator _generator;
        private DropTableService _drops;
        private GameConfigCatalog _catalog;
        private int _kills;
        private int _stageNumber = 1;
        private int _level = 1;
        private long _exp;
        private long _softCurrency;
        private long _premiumCurrency = 60;
        private long _power;
        private EquipmentInstance _latestLoot;
        private int _realmStage = 1;
        private int _spiritualRootPoints;
        private int _buildIndex;
        private bool _mailClaimed;
        private bool _taskClaimed;
        private InventoryService _inventory;
        private EquipmentDecompositionService _decomposition;
        private EquipmentLoadoutService _loadout;
        private CharacterStatService _stats;
        private PowerCalculator _powerCalculator;
        private PrototypeLoginController _login;
        private string _serverLatestInstanceId = string.Empty;
        private bool _settlingServerBattle;
        private CultivationMethodService _cultivation;
        private GameDebugService _debugService;
        private int _debugStep;
        private DemoPacingSession _pacing;
        private DemoPacingConfig _pacingConfig;
        private float _pacingSpeed = 1f;
        private bool _playtestQuitRequested;
        private IPlayerSaveRepository _saveRepository;
        private string _saveLoadWarning;
        private AfkState _afkState;
        private string _offlineRewardSummary;
        private GameSettingsService _settings;
        private IReadOnlyList<CommercialProductConfig> _commercialProducts;
        private ValidationFunnelTracker _validationTelemetry;
        private PrototypeCombatFeedback _feedback;
        private readonly CharacterStats _baseStats = new CharacterStats
        {
            HP = 180f, Attack = 12f, Defense = 3f, CritRate = 0.1f, CritDamage = 1.5f, AttackSpeed = 1f, FireDamage = 0.1f
        };
        public EquipmentInstance LatestLoot => _latestLoot;
        public long Power => _power;
        public int StageNumber => _stageNumber;
        public bool CommercialUnlocked => _latestLoot != null || !string.IsNullOrEmpty(_serverLatestInstanceId) || (_inventory?.State.Equipment.Count ?? 0) > 0;

        private void Start()
        {
            PrototypeVisualTheme.Apply(FindAnyObjectByType<Canvas>());
            _settings = new GameSettingsService(new PlayerPrefsSettingsStore());
            _settings.ApplySound();
            _catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            _commercialProducts = CommercialEntitlementService.LoadProducts(new ResourcesConfigSource());
            _validationTelemetry = new ValidationFunnelTracker(new JsonlValidationEventSink(Path.Combine(Application.persistentDataPath, "validation-funnel.jsonl")));
            _feedback = gameObject.GetComponent<PrototypeCombatFeedback>() ?? gameObject.AddComponent<PrototypeCombatFeedback>();
            _feedback.Initialize();
            _saveRepository = JsonPlayerSaveRepository.CreateDefault();
            var saved = LoadSnapshotSafely();
            _generator = new EquipmentGenerator(new SystemRandomSource(), _catalog);
            _drops = new DropTableService(_catalog, _generator, new SystemRandomSource());
            _inventory = new InventoryService(RestoreInventory(saved), _catalog);
            _decomposition = new EquipmentDecompositionService(_inventory, DecompositionFormulaLoader.Load(new ResourcesConfigSource()));
            _loadout = new EquipmentLoadoutService(_catalog);
            _stats = new CharacterStatService();
            _stats.AddProvider(new EquipmentStatProvider(_catalog, _loadout));
            var methodState = new CultivationMethodState();
            _cultivation = new CultivationMethodService(_catalog, new RealmProgressState { RealmId = "realm_spirit_foundation" }, methodState);
            foreach (var methodId in _catalog.CultivationMethods.Keys) _cultivation.Learn(methodId);
            EquipBuild(0);
            _stats.AddProvider(new CultivationMethodStatProvider(_cultivation));
            _powerCalculator = PowerCalculator.Load(new ResourcesConfigSource());
            _pacingConfig = DemoPacingLoader.Load(new ResourcesConfigSource());
            _pacing = new DemoPacingSession(_pacingConfig);
            RestoreProgress(saved);
            ClaimOfflineProgress(saved);
            _pacingSpeed = DevelopmentPlaytestOptions.Speed;
            _debugService = new GameDebugService(new DebugGameState(), _catalog, new SystemRandomSource());
            _login = FindAnyObjectByType<PrototypeLoginController>();
            if (equipLatestButton != null) equipLatestButton.onClick.AddListener(EquipLatest);
            RefreshProgressDisplay();
            _validationTelemetry.TrackOnce("session_started", _pacing.ElapsedSeconds, _stageNumber, _power);
            _validationTelemetry.TrackOnce("battle_visible", _pacing.ElapsedSeconds, _stageNumber, _power);
            if (guideText != null)
            {
                if (!string.IsNullOrEmpty(_saveLoadWarning)) guideText.text = _saveLoadWarning;
                else if (!string.IsNullOrEmpty(_offlineRewardSummary)) guideText.text = _offlineRewardSummary;
            }
            SpawnEnemy();
        }

        private void Update()
        {
            _pacing.Advance(Time.unscaledDeltaTime * _pacingSpeed);
            _stageNumber = _pacing.CurrentStageNumber;
            _battle.Tick(Time.deltaTime * _pacingSpeed);
            enemyHealth.value = _battle.Enemy.Hp / _battle.Enemy.MaxHp;
            var stage = _catalog.Stages[$"stage_1_{_stageNumber}"];
            var bossLabel = stage.IsBossStage ? "BOSS · " : string.Empty;
            statusText.text = $"{bossLabel}{stage.Name}  1-{_stageNumber}\n{_battle.Enemy.Id}  {_battle.Enemy.Hp:0}/{_battle.Enemy.MaxHp:0}\n已击败：{_kills}";
            statusText.color = stage.IsBossStage ? PrototypeVisualTheme.Gold : PrototypeVisualTheme.TextPrimary;
            if (!_playtestQuitRequested && _pacing.IsComplete && _pacing.PendingRewards == 0 && !_settlingServerBattle && DevelopmentPlaytestOptions.AutoQuit)
            {
                _playtestQuitRequested = true;
                Debug.Log($"PLAYTEST_COMPLETE elapsed={_pacing.ElapsedSeconds:0} kills={_kills} rewardWindows={_pacing.GeneratedRewardWindows} consumed={_pacing.ConsumedRewardWindows} pending={_pacing.PendingRewards} inventory={_inventory.State.Equipment.Count} power={_power}");
                Application.Quit(0);
            }
        }

        private void SpawnEnemy()
        {
            var monsterId = _stageNumber == 10 ? "monster_stone_nightmare" : "monster_wasteland_beast";
            var monster = _catalog.Monsters[monsterId];
            var hp = monster.MaxHp + _kills * 6f;
            var playerStats = _stats.Calculate(_baseStats);
            playerStats.HP = Mathf.Max(playerStats.HP, 9999f);
            var player = new BattleActor("player", playerStats, 0.7f, new[] { _catalog.Skills["skill_ember_brand"] });
            var enemy = new BattleActor(monster.Id, new CharacterStats
            {
                HP = hp, Attack = monster.Attack, Defense = monster.Defense, CritDamage = 1.5f
            }, monster.AttackInterval, rank: monster.Rank, enrageSeconds: monster.EnrageSeconds);
            _battle = new AutoBattleEngine(player, enemy,
                new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new SystemRandomSource()));
            _battle.Finished += HandleBattleFinished;
            _battle.EventRaised += value =>
            {
                if (value.Type == BattleEventType.BasicAttack || value.Type == BattleEventType.SkillCast || value.Type == BattleEventType.DamageOverTime)
                    _feedback.PlayHit(value.IsCritical);
            };
            if (monster.Rank == MonsterRank.Boss) _feedback.PlayBossAppearance();
        }

        private async void HandleBattleFinished(BattleState state)
        {
            if (state != BattleState.Victory) return;
            if (!_pacing.TryConsumeBattleReward())
            {
                _kills++;
                if (guideText != null) guideText.text = $"修炼积累中 · {_pacing.ElapsedMinutes}/{_pacingConfig.durationMinutes} 分钟\n下一次装备结算按 {_pacingConfig.equipmentDropSeconds} 秒节奏触发";
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
                return;
            }
            if (_login != null && _login.IsServerAuthenticated)
            {
                if (_settlingServerBattle) return;
                _settlingServerBattle = true;
                try
                {
                    var stageId = $"stage_1_{_stageNumber}";
                    var nonce = Guid.NewGuid().ToString("N");
                    var started = ImmortalLootApiClient.Parse<BattleStartDto>(await _login.ApiClient.StartBattleAsync(stageId, "ui-start-" + nonce));
                    var finished = ImmortalLootApiClient.Parse<BattleFinishDto>(await _login.ApiClient.FinishBattleAsync(Guid.Parse(started.sessionId), "ui-finish-" + nonce));
                    _serverLatestInstanceId = finished.equipmentInstanceId;
                    var inventory = ImmortalLootApiClient.Parse<InventoryDto>(await _login.ApiClient.GetInventoryAsync());
                    EquipmentItemDto row = null;
                    if (inventory.equipment != null) foreach (var candidate in inventory.equipment) if (candidate.instanceId == _serverLatestInstanceId) { row = candidate; break; }
                    var item = row == null || string.IsNullOrEmpty(row.instanceJson) ? null : JsonUtility.FromJson<ServerEquipmentDto>(row.instanceJson);
                    lootText.text = FormatServerLoot(item, row) + $"\n\n服务器奖励：经验 +{finished.rewardExp:N0} · 灵砂 +{finished.rewardSoftCurrency:N0}";
                    _kills++;
                    _validationTelemetry.TrackOnce("first_equipment_drop", _pacing.ElapsedSeconds, _stageNumber, _power, row?.quality ?? string.Empty);
                    if (Enum.TryParse(row?.quality, true, out EquipmentQuality serverQuality)) _feedback.PlayLoot(serverQuality);
                    if (_stageNumber == 10)
                    {
                        _validationTelemetry.TrackOnce("first_boss_defeated", _pacing.ElapsedSeconds, _stageNumber, _power, row?.quality ?? string.Empty);
                        if (guideText != null) guideText.text = "服务器 Boss 已击败：可领取挂机收益并准备境界突破";
                    }
                    await RefreshServerProfile();
                }
                catch (Exception exception) { if (guideText != null) guideText.text = "服务器战斗结算失败：" + exception.Message; }
                finally { _settlingServerBattle = false; Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed); }
                return;
            }
            _kills++;
            GrantExperience(25);
            _softCurrency += _stageNumber == 10 ? 50 : 10;
            var isBoss = _stageNumber == 10;
            var dropTableId = isBoss ? _catalog.Stages["stage_1_10"].DropTableId : "drop_prototype_equipment";
            var source = isBoss ? DropSourceType.Boss : DropSourceType.Monster;
            var sourceId = isBoss ? "monster_stone_nightmare" : "monster_wasteland_beast";
            var drops = _drops.Roll(dropTableId, new DropContext(source, 1 + _kills / 3, sourceId));
            _latestLoot = drops[0].Equipment;
            if (_inventory.State.Equipment.Count >= _inventory.State.EquipmentCapacity)
                _inventory.RemoveEquipment(_inventory.State.Equipment[0].InstanceId, out _);
            _inventory.AddEquipment(_latestLoot);
            _validationTelemetry.TrackOnce("first_equipment_drop", _pacing.ElapsedSeconds, _stageNumber, _power, _latestLoot.Quality.ToString());
            _feedback.PlayLoot(_latestLoot.Quality);
            lootText.text = FormatLoot(_latestLoot) + "\n\n点击“穿戴最新装备”提升战力";
            lootText.color = PrototypeVisualTheme.QualityColor(_latestLoot.Quality);
            if (_stageNumber == 10)
            {
                _validationTelemetry.TrackOnce("first_boss_defeated", _pacing.ElapsedSeconds, _stageNumber, _power, _latestLoot.Quality.ToString());
                if (guideText != null) guideText.text = "Boss 已击败：可领取挂机收益并准备境界突破";
            }
            RefreshProgressDisplay();
            SaveProgress();
            Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
        }

        public async void EquipLatest()
        {
            if (_login != null && _login.IsServerAuthenticated)
            {
                if (string.IsNullOrEmpty(_serverLatestInstanceId)) { if (guideText != null) guideText.text = "尚无服务器装备，先完成一场在线战斗。"; return; }
                try
                {
                    var serverPowerBefore = _power;
                    var result = ImmortalLootApiClient.Parse<EquipResultDto>(await _login.ApiClient.EquipAsync(_serverLatestInstanceId));
                    await RefreshServerProfile();
                    _validationTelemetry.TrackOnce("first_equipment_equipped", _pacing.ElapsedSeconds, _stageNumber, _power, value: Math.Max(0, _power - serverPowerBefore));
                    _feedback.PlayEquip();
                    if (guideText != null) guideText.text = $"服务器装备成功：{result.slot}{(result.replaced ? "，已替换旧装备" : string.Empty)}";
                }
                catch (Exception exception) { if (guideText != null) guideText.text = "服务器穿戴失败：" + exception.Message; }
                return;
            }
            if (_latestLoot == null) return;
            var before = _power;
            _loadout.Equip(_latestLoot);
            RefreshProgressDisplay();
            _validationTelemetry.TrackOnce("first_equipment_equipped", _pacing.ElapsedSeconds, _stageNumber, _power, _latestLoot.Quality.ToString(), Math.Max(0, _power - before));
            _feedback.PlayEquip();
            StartCoroutine(FlashPowerGain());
            SaveProgress();
            if (guideText != null) guideText.text = $"装备成功，战力 {before} → {_power}。继续推图挑战 1-10 Boss";
        }

        private void RefreshProgressDisplay()
        {
            var calculated = _stats.Calculate(_baseStats);
            _power = _powerCalculator.Calculate(calculated);
            if (profileText != null) profileText.text = $"云游剑客  Lv.{_level}\n战力 {_power}";
            if (currencyText != null) currencyText.text = $"灵砂 {_softCurrency:N0}    仙晶 {_premiumCurrency:N0}";
        }

        private IEnumerator FlashPowerGain()
        {
            if (profileText == null) yield break;
            var originalScale = profileText.rectTransform.localScale;
            profileText.color = PrototypeVisualTheme.Gold;
            profileText.rectTransform.localScale = originalScale * 1.12f;
            yield return new WaitForSecondsRealtime(0.18f);
            profileText.rectTransform.localScale = originalScale;
            profileText.color = PrototypeVisualTheme.TextPrimary;
        }

        private PlayerSaveSnapshot LoadSnapshotSafely()
        {
            if (!_saveRepository.Exists) return null;
            try { return _saveRepository.Load(); }
            catch (Exception exception)
            {
                _saveLoadWarning = "存档校验失败，已安全新开；损坏文件已保留。";
                Debug.LogError("SAVE_RECOVERY: " + exception);
                var source = JsonPlayerSaveRepository.DefaultPath;
                if (File.Exists(source)) File.Move(source, source + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                return null;
            }
        }

        private static InventoryState RestoreInventory(PlayerSaveSnapshot saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.InventoryJson)) return new InventoryState { EquipmentCapacity = 120 };
            var state = JsonUtility.FromJson<InventoryState>(saved.InventoryJson) ?? new InventoryState();
            state.EquipmentCapacity = Math.Max(120, state.EquipmentCapacity);
            state.Equipment ??= new List<EquipmentInstance>();
            state.Materials ??= new List<ItemStack>();
            state.Consumables ??= new List<ItemStack>();
            return state;
        }

        private void RestoreProgress(PlayerSaveSnapshot saved)
        {
            if (saved == null) return;
            _level = Math.Max(1, saved.Level);
            _exp = Math.Max(0, saved.Exp);
            _kills = Math.Max(0, saved.Kills);
            _softCurrency = Math.Max(0, saved.SoftCurrency);
            _premiumCurrency = Math.Max(0, saved.PremiumCurrency);
            _realmStage = Math.Max(1, saved.RealmStage);
            _baseStats.Attack += (_level - 1) * 2f;
            _baseStats.HP += (_level - 1) * 15f;
            _pacing.Restore(saved.StageElapsedSeconds);
            var equipped = JsonUtility.FromJson<EquippedIds>(saved.EquippedInstanceIdsJson);
            if (equipped?.ids == null) return;
            foreach (var id in equipped.ids)
            {
                var item = _inventory.State.Equipment.Find(value => value.InstanceId == id);
                if (item != null) _loadout.Equip(item);
            }
        }

        private void ClaimOfflineProgress(PlayerSaveSnapshot saved)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lastActive = saved?.LastActiveUnixSeconds ?? now;
            if (lastActive <= 0 || lastActive > now) lastActive = now;
            _afkState = new AfkState { LastOfflineUnixSeconds = lastActive };
            if (saved == null) return;
            var service = new AfkRewardService(AfkConfigLoader.Load(new ResourcesConfigSource()), _afkState, new UtcClock());
            var stageRate = _catalog.Stages[$"stage_1_{_pacing.CurrentStageNumber}"].AfkRewardRate;
            var reward = service.Claim(stageRate, _cultivation.GetAfkMultiplier());
            if (reward.EffectiveSeconds <= 0) return;
            GrantExperience(reward.Experience);
            _softCurrency += reward.SoftCurrency;
            if (reward.MaterialCount > 0) _inventory.AddStack("item_enhancement_stone", reward.MaterialCount, ItemCategory.Material);
            var equipmentCount = Math.Min(reward.EquipmentRolls, _inventory.State.EquipmentCapacity - _inventory.State.Equipment.Count);
            for (var i = 0; i < equipmentCount; i++)
            {
                var item = _drops.Roll("drop_prototype_equipment", new DropContext(DropSourceType.Afk, _level, "offline"))[0].Equipment;
                if (item != null) _inventory.AddEquipment(item);
            }
            _offlineRewardSummary = $"离线修炼 {TimeSpan.FromSeconds(reward.EffectiveSeconds).TotalHours:0.#} 小时\n经验 +{reward.Experience:N0} · 灵砂 +{reward.SoftCurrency:N0} · 装备 {equipmentCount} 件";
            SaveProgress();
        }

        private void GrantExperience(long amount)
        {
            _exp += Math.Max(0, amount);
            while (_exp >= _level * 50L)
            {
                _exp -= _level * 50L;
                _level++;
                _baseStats.Attack += 2f;
                _baseStats.HP += 15f;
            }
        }

        private void SaveProgress()
        {
            if (_saveRepository == null || _inventory == null || _pacing == null) return;
            var equipped = new EquippedIds();
            foreach (var item in _loadout.Equipped.Values) equipped.ids.Add(item.InstanceId);
            _saveRepository.Save(new PlayerSaveSnapshot
            {
                PlayerId = "local-player", Nickname = "云游剑客", Level = _level, Exp = _exp,
                RealmStage = _realmStage, Kills = _kills, SoftCurrency = _softCurrency,
                PremiumCurrency = _premiumCurrency, StageElapsedSeconds = _pacing.ElapsedSeconds,
                LastActiveUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(_inventory.State),
                EquippedInstanceIdsJson = JsonUtility.ToJson(equipped)
            });
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveProgress(); }
        private void OnApplicationQuit() => SaveProgress();

        [Serializable]
        private sealed class EquippedIds { public List<string> ids = new List<string>(); }

        private sealed class UtcClock : IServerClock { public DateTime UtcNow => DateTime.UtcNow; }

        public string ExecutePageAction(string pageName)
        {
            switch (pageName)
            {
                case "CharacterPage":
                    RefreshProgressDisplay();
                    return $"等级 {_level} · 战力 {_power}\n经验 {_exp}/{_level * 50L}\n当前境界 {_realmStage} 阶";
                case "EquipmentPage":
                    if (_latestLoot == null) return "尚无装备，先完成一场战斗。";
                    var comparison = new EquipmentComparisonService(_catalog).Compare(_baseStats, _loadout.Equipped, _latestLoot);
                    var comparisonText = $"穿戴比较：攻击 {comparison.AttackDelta:+0.##;-0.##;0} · 生命 {comparison.HpDelta:+0.##;-0.##;0} · 防御 {comparison.DefenseDelta:+0.##;-0.##;0}";
                    EquipLatest();
                    return $"{comparisonText}\n已穿戴 {_latestLoot.DisplayName}\n统一战力更新为 {_power}";
                case "InventoryPage":
                    var sorted = _inventory.QueryEquipment(EquipmentSortMode.QualityDescending);
                    var targets = new System.Collections.Generic.List<string>();
                    foreach (var item in sorted)
                    {
                        if (!IsEquipped(item) && !item.IsLocked && item.Quality <= EquipmentQuality.Epic) targets.Add(item.InstanceId);
                    }
                    var reward = new DecompositionReward();
                    foreach (var id in targets) reward += _decomposition.Decompose(id);
                    if (reward.EnhancementMaterial > 0) _inventory.AddStack("item_enhancement_stone", reward.EnhancementMaterial, ItemCategory.Material);
                    if (reward.EquipmentEssence > 0) _inventory.AddStack("item_equipment_essence", reward.EquipmentEssence, ItemCategory.Material);
                    _softCurrency += reward.SoftCurrency;
                    RefreshProgressDisplay();
                    return $"背包已按品质降序筛选 · 批量分解 Epic 及以下 {targets.Count} 件\n灵砂 +{reward.SoftCurrency:N0} · 强化石 +{reward.EnhancementMaterial} · 装备精华 +{reward.EquipmentEssence}\n装备 {_inventory.State.Equipment.Count}/{_inventory.State.EquipmentCapacity} · 材料 {_inventory.State.Materials.Count} 类 · 消耗品 {_inventory.State.Consumables.Count} 类\nLegendary/Mythic、锁定及已穿戴装备受保护";
                case "CultivationPage":
                    _realmStage = Math.Min(10, _realmStage + 1);
                    _buildIndex = (_buildIndex + 1) % 3;
                    EquipBuild(_buildIndex);
                    RefreshProgressDisplay();
                    _validationTelemetry.TrackOnce("first_realm_breakthrough", _pacing.ElapsedSeconds, _stageNumber, _power, value: _realmStage);
                    return $"境界突破至 {_realmStage} 阶\n已学习并装备 {BuildName()}\n主修：{PrimaryMethodName()} · 辅修：{AuxiliaryMethodName()}\n统一属性服务重算战力 {_power}";
                case "SpiritualRootPage":
                    _spiritualRootPoints++;
                    _baseStats.FireDamage += 0.01f;
                    RefreshProgressDisplay();
                    return $"渡劫灵根成长：火灵根 +1\n累计 {_spiritualRootPoints} 点";
                case "StagePage": return $"当前推进 1-{_stageNumber}\n1-10 为石魇 Boss，战斗会自动推进。";
                case "ShopPage":
                    if (!CommercialUnlocked) return "完成首件装备并理解战力成长后解锁商店。";
                    var offerText = "商业化验证商品（离线预览，不执行支付）\n";
                    foreach (var product in _commercialProducts)
                        offerText += $"{product.name} · {product.amountMinorUnits / 100m:0.##} {product.currencyCode}\n";
                    return offerText + "真实购买必须由服务器建单、平台回执并由服务器验证发放。";
                case "RankingPage": return $"本地预览：战力榜 · {_power} 分\n正式榜单由服务器计算永久榜/周榜。";
                case "MailPage":
                    if (_mailClaimed) return "补偿飞简附件已领取，重复点击不会再发放。";
                    _mailClaimed = true; _premiumCurrency += 10; RefreshProgressDisplay();
                    return "邮件附件领取成功：仙晶 +10";
                case "TaskPage":
                    if (_taskClaimed) return "今日 20 活跃宝箱已领取。";
                    _taskClaimed = true; _softCurrency += 100; RefreshProgressDisplay();
                    return "完成登录/推图任务：活跃度 20\n宝箱灵砂 +100";
                case "ActivityPage": return "灵潮涌动生效中\n服务器挂机收益 ×2";
                case "DebugPage":
                    return SettingsSummary();
                default: return "功能已就绪。";
            }
        }

        public void RecordShopExposure()
        {
            if (CommercialUnlocked) _validationTelemetry?.TrackOnce("shop_exposed", _pacing?.ElapsedSeconds ?? 0f, _stageNumber, _power);
        }

        public string SettingsSummary() =>
            $"设置\n\n声音：{(_settings.SoundEnabled ? "开启" : "关闭")}\n震动：{(_settings.VibrationEnabled ? "开启" : "关闭")}\n进度会在暂停、退出和关键成长节点自动保存。";

        public string ToggleSoundSetting()
        {
            _settings.ToggleSound();
            _settings.ApplySound();
            return SettingsSummary();
        }

        public string ToggleVibrationSetting()
        {
            _settings.ToggleVibration();
            _settings.TryVibrate();
            return SettingsSummary();
        }

        public string SaveNowFromSettings()
        {
            SaveProgress();
            return SettingsSummary() + "\n\n进度已安全保存。";
        }

        public string LegalNotice() =>
            "隐私政策与用户协议\n\n本候选版仅保存本地游戏进度与不含个人身份的验证事件。\n不读取通讯录、定位、相册或广告标识。\n正式外测前须由发行主体补充主体名称、联系邮箱和最终法律文本。";

        private string BuildName() => _buildIndex == 0 ? "火修燃烧" : _buildIndex == 1 ? "雷修暴击" : "血修吸血";

        private void EquipBuild(int index)
        {
            var primary = index == 0 ? "method_cinder_scripture" : index == 1 ? "method_thunder_pulse" : "method_crimson_well";
            var auxiliary = index == 0 ? "method_ember_breath" : index == 1 ? "method_quick_spark" : "method_blood_return";
            _cultivation.EquipPrimary(primary);
            _cultivation.EquipAuxiliary(0, auxiliary);
        }

        private string PrimaryMethodName()
        {
            foreach (var method in _cultivation.GetActiveMethods()) if (method.IsPrimary) return method.Name;
            return "未装备";
        }

        private string AuxiliaryMethodName()
        {
            foreach (var method in _cultivation.GetActiveMethods()) if (!method.IsPrimary) return method.Name;
            return "未装备";
        }

        private bool IsEquipped(EquipmentInstance item)
        {
            foreach (var equipped in _loadout.Equipped.Values) if (ReferenceEquals(equipped, item)) return true;
            return false;
        }

        private static string FormatLoot(EquipmentInstance item)
        {
            var text = $"最新掉落\n[{item.Quality}] {item.DisplayName}  Lv.{item.Level}";
            foreach (var affix in item.Affixes) text += $"\n  {affix.DisplayName} +{affix.Value:0.0}";
            return text;
        }

        private async System.Threading.Tasks.Task RefreshServerProfile()
        {
            var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(await _login.ApiClient.GetProfileAsync());
            _power = Math.Max(0, profile.power);
            if (profileText != null) profileText.text = $"{profile.nickname}  Lv.{profile.level}\n战力 {profile.power:N0}";
            if (currencyText != null) currencyText.text = $"灵砂 {profile.softCurrency:N0}    仙晶 {profile.premiumCurrency:N0}";
        }

        private static string FormatServerLoot(ServerEquipmentDto item, EquipmentItemDto row)
        {
            if (item == null) return row == null ? "服务器已掉落装备，背包同步中" : $"服务器掉落\n[{row.quality}] {row.baseId} Lv.{row.level}";
            var text = $"服务器掉落\n[{item.quality}] {item.baseId} Lv.{item.level}";
            if (item.affixes != null) foreach (var affix in item.affixes) text += $"\n  {affix.id} +{affix.value:0.##}";
            return text + "\n点击“穿戴最新装备”提交服务器";
        }

        private string ExecuteDebugStep()
        {
            _debugStep = (_debugStep + 1) % 5;
            var state = _debugService.State;
            switch (_debugStep)
            {
                case 1:
                    _debugService.AddSoftCurrency(10000); _debugService.AddPremiumCurrency(100); _debugService.AddExp(500); _debugService.LevelUp(5);
                    _softCurrency = state.SoftCurrency; _premiumCurrency = state.PremiumCurrency; _exp = state.Exp; _level = state.Level;
                    RefreshProgressDisplay();
                    return "GM 资源命令：灵砂 +10,000 / 仙晶 +100 / 经验 +500 / 等级 +5";
                case 2:
                    var item = _debugService.GenerateEquipment("weapon_cloudsteel_blade", 10, EquipmentQuality.Mythic, "attack_flat");
                    _inventory.AddEquipment(item); _latestLoot = item; lootText.text = FormatLoot(item);
                    return "GM 装备命令：生成 Mythic 云纹青锋，并指定攻击词条";
                case 3:
                    _debugService.Breakthrough(); _debugService.UnlockStage("stage_1_10"); _debugService.SetRoot("root_fire", 3); _debugService.LearnMethod("method_cinder_scripture");
                    return "GM 进度命令：突破 / 解锁 1-10 / 火灵根 3 / 学习烬阳归藏篇";
                case 4:
                    _debugService.SimulateOffline8Hours(DateTime.UtcNow); _debugService.SimulatePayment(60);
                    return "GM 模拟命令：离线 8 小时 / Mock 充值 60 仙晶\n再次点击执行清空 Debug 存档";
                default:
                    _debugService.ClearSave();
                    return "GM 清档命令已执行；再次点击从资源命令开始。";
            }
        }

#if UNITY_INCLUDE_TESTS
        public void SetPacingSpeedForTests(float speed) => _pacingSpeed = Mathf.Max(1f, speed);
        public void SaveForTests() => SaveProgress();
#endif
    }
}
