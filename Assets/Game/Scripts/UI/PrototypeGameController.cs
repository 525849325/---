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
using ImmortalLoot.SpiritualRoot;
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
        private CharacterStatService _progressionStats;
        private PowerCalculator _powerCalculator;
        private PrototypeLoginController _login;
        private string _serverLatestInstanceId = string.Empty;
        private bool _settlingServerBattle;
        private PendingServerBattleSettlement _pendingServerBattleSettlement;
        private CultivationMethodService _cultivation;
        private DemoPacingSession _pacing;
        private DemoPacingConfig _pacingConfig;
        private VictoryDrivenStageLoop _stageLoop;
        private StageBattleFactory _stageBattleFactory;
        private string _activeBattleStageId = "stage_1_1";
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
        private EquipmentUpgradeEvaluator _upgradeEvaluator;
        private string _pendingSacrificeConfirmationKey = string.Empty;
        private int _skippedPendingRewardWindows;
        private PlayerProgressState _progressState;
        private int _guideStep;
        private static readonly float[] ServerSettlementRetryDelays = { 1f, 2f, 4f, 8f, 15f, 30f };
#if UNITY_INCLUDE_TESTS
        private int _saveOperationCount;
        private long _configuredStageExperienceGranted;
        private bool _battlePausedForTests;
        private static bool _pauseNextBattleForTests;
#endif
        private readonly CharacterStats _baseStats = new CharacterStats
        {
            HP = 180f, Attack = 12f, Defense = 3f, CritRate = 0.1f, CritDamage = 1.5f, AttackSpeed = 1f, FireDamage = 0.1f
        };
        public EquipmentInstance LatestLoot => _latestLoot;
        public long Power => _power;
        public int StageNumber => _stageNumber;
        public bool AutoEquipEnabled => _settings?.AutoEquipEnabled ?? true;
        public bool CommercialUnlocked => _latestLoot != null || !string.IsNullOrEmpty(_serverLatestInstanceId) || (_inventory?.State.Equipment.Count ?? 0) > 0;

        private sealed class PendingServerBattleSettlement
        {
            public string StageId;
            public string FinishKey;
            public string SessionId;
            public bool RewardWindowEligible;
            public bool ConsumeRewardWindow;
            public int RetryAttempt;
        }

        private void Start()
        {
#if UNITY_INCLUDE_TESTS
            _battlePausedForTests = _pauseNextBattleForTests;
            _pauseNextBattleForTests = false;
#endif
            _settings = new GameSettingsService(new PlayerPrefsSettingsStore());
            _settings.ApplySound();
            PrototypeVisualTheme.Apply(FindAnyObjectByType<Canvas>());
            _catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            _commercialProducts = CommercialEntitlementService.LoadProducts(new ResourcesConfigSource());
            _validationTelemetry = new ValidationFunnelTracker(new JsonlValidationEventSink(Path.Combine(Application.persistentDataPath, "validation-funnel.jsonl")));
            _feedback = gameObject.GetComponent<PrototypeCombatFeedback>() ?? gameObject.AddComponent<PrototypeCombatFeedback>();
            _feedback.Initialize();
            _saveRepository = JsonPlayerSaveRepository.CreateDefault();
            var saved = LoadSnapshotSafely();
            _progressState = PlayerProgressStateCodec.Deserialize(saved?.ProgressJson);
            _stageLoop = new VictoryDrivenStageLoop(_catalog, _progressState.Stage, _progressState.CurrentStageId);
            _generator = new EquipmentGenerator(new SystemRandomSource(), _catalog);
            _drops = new DropTableService(_catalog, _generator, new SystemRandomSource());
            _inventory = new InventoryService(RestoreInventory(saved), _catalog);
            _latestLoot = _inventory.State.PendingEquipment;
            _decomposition = new EquipmentDecompositionService(_inventory, DecompositionFormulaLoader.Load(new ResourcesConfigSource()));
            _loadout = new EquipmentLoadoutService(_catalog);
            _stats = new CharacterStatService();
            _stats.AddProvider(new EquipmentStatProvider(_catalog, _loadout));
            PrepareCultivationState(_progressState.Cultivation);
            _cultivation = new CultivationMethodService(_catalog, _progressState.Realm, _progressState.Cultivation);
            _buildIndex = ResolveBuildIndex(_progressState.Cultivation.PrimaryMethodId);
            if (!HasValidSavedBuild(_progressState.Cultivation) && !TryEquipFirstLearnedBuild(out _buildIndex))
                ClearActiveCultivationBuild();
            var cultivationStats = new CultivationMethodStatProvider(_cultivation);
            _stats.AddProvider(cultivationStats);
            _progressionStats = new CharacterStatService();
            _progressionStats.AddProvider(cultivationStats);
            _powerCalculator = PowerCalculator.Load(new ResourcesConfigSource());
            _upgradeEvaluator = new EquipmentUpgradeEvaluator(_catalog, _powerCalculator);
            _pacingConfig = DemoPacingLoader.Load(new ResourcesConfigSource());
            _pacing = new DemoPacingSession(_pacingConfig);
            _stageBattleFactory = new StageBattleFactory(
                _catalog,
                new MonsterFactory(_catalog),
                new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new SystemRandomSource()));
            RestoreProgress(saved);
            ClaimOfflineProgress(saved);
            _pacingSpeed = DevelopmentPlaytestOptions.Speed;
            _login = FindAnyObjectByType<PrototypeLoginController>();
            if (equipLatestButton != null) equipLatestButton.onClick.AddListener(EquipLatest);
            RefreshProgressDisplay();
            if (_latestLoot != null && lootText != null)
            {
                lootText.text = FormatLoot(_latestLoot) + "\n\n已从存档恢复到待领取区。请先清理背包，再穿戴或领取。";
                lootText.color = PrototypeVisualTheme.QualityColor(_latestLoot.Quality);
            }
            _validationTelemetry.TrackOnce("session_started", _pacing.ElapsedSeconds, _stageNumber, _power);
            _validationTelemetry.TrackOnce("battle_visible", _pacing.ElapsedSeconds, _stageNumber, _power);
            if (guideText != null)
            {
                if (!string.IsNullOrEmpty(_saveLoadWarning)) guideText.text = _saveLoadWarning;
                else if (!string.IsNullOrEmpty(_offlineRewardSummary)) guideText.text = _offlineRewardSummary;
                else if (_guideStep > 0) guideText.text = $"引导进度已恢复：{_guideStep}/4。继续推进当前成长目标。";
            }
            SpawnEnemy();
        }

        private void Update()
        {
#if UNITY_INCLUDE_TESTS
            if (!_battlePausedForTests)
            {
                _pacing.Advance(Time.unscaledDeltaTime * _pacingSpeed);
                _battle.Tick(Time.deltaTime * _pacingSpeed);
            }
#else
            _pacing.Advance(Time.unscaledDeltaTime * _pacingSpeed);
            _battle.Tick(Time.deltaTime * _pacingSpeed);
#endif
            enemyHealth.value = _battle.Enemy.Hp / _battle.Enemy.MaxHp;
            var stage = _catalog.Stages[_activeBattleStageId];
            var bossLabel = stage.IsBossStage ? "BOSS · " : string.Empty;
            statusText.text = $"{bossLabel}{stage.Name}  1-{stage.StageNumber}\n{_battle.Enemy.Id}  {_battle.Enemy.Hp:0}/{_battle.Enemy.MaxHp:0}\n已击败：{_kills}";
            statusText.color = stage.IsBossStage ? PrototypeVisualTheme.Gold : PrototypeVisualTheme.TextPrimary;
            if (!_playtestQuitRequested && _pacing.IsComplete && _pacing.PendingRewards == 0 &&
                !_settlingServerBattle && _pendingServerBattleSettlement == null && DevelopmentPlaytestOptions.AutoQuit)
            {
                _playtestQuitRequested = true;
                Debug.Log($"PLAYTEST_COMPLETE elapsed={_pacing.ElapsedSeconds:0} kills={_kills} rewardWindows={_pacing.GeneratedRewardWindows} consumed={_pacing.ConsumedRewardWindows} pending={_pacing.PendingRewards} inventory={_inventory.State.Equipment.Count} power={_power}");
                Application.Quit(0);
            }
        }

        private void SpawnEnemy()
        {
            if (_pendingServerBattleSettlement != null) return;
            _activeBattleStageId = _stageLoop.CurrentStageId;
            var stage = _stageLoop.CurrentStage;
            var playerStats = _stats.Calculate(_baseStats);
            playerStats.HP = Mathf.Max(playerStats.HP, 9999f);
            var player = new BattleActor("player", playerStats, 0.7f, new[] { _catalog.Skills["skill_ember_brand"] });
            _battle = _stageBattleFactory.Create(_activeBattleStageId, player);
            _battle.Finished += HandleBattleFinished;
            _battle.EventRaised += value =>
            {
                if (value.Type == BattleEventType.BasicAttack || value.Type == BattleEventType.SkillCast || value.Type == BattleEventType.DamageOverTime)
                    _feedback.PlayHit(value.IsCritical);
            };
            if (stage.IsBossStage) _feedback.PlayBossAppearance();
        }

        private async void HandleBattleFinished(BattleState state)
        {
            if (state == BattleState.Defeat)
            {
                RecordBattleDefeat(scheduleRetry: true);
                return;
            }
            if (state != BattleState.Victory) return;

            var completedStageId = _activeBattleStageId;
            if (!_catalog.Stages.TryGetValue(completedStageId, out var completedStage))
                throw new ConfigException($"Completed stage '{completedStageId}' was not found.");
            var serverAuthenticated = _login != null && _login.IsServerAuthenticated;
            if (serverAuthenticated)
            {
                if (_settlingServerBattle || _pendingServerBattleSettlement != null) return;
                _settlingServerBattle = true;
                var nonce = Guid.NewGuid().ToString("N");
                var hadPendingRewardWindow = _pacing.PendingRewards > 0;
                BattleStartDto started;
                try
                {
                    started = ImmortalLootApiClient.Parse<BattleStartDto>(await _login.ApiClient.StartBattleAsync(
                        completedStageId, "ui-start-" + nonce));
                    if (!Guid.TryParse(started.sessionId, out _) ||
                        !string.Equals(started.stageId, completedStageId, StringComparison.Ordinal) ||
                        !string.Equals(started.status, "Started", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Server battle start response was incomplete or mismatched.");
                }
                catch (Exception exception)
                {
                    _settlingServerBattle = false;
                    RecordBattleDefeat(scheduleRetry: true);
                    if (guideText != null) guideText.text = "服务器战斗启动失败，保留原关重试：" + exception.Message;
                    return;
                }
                _pendingServerBattleSettlement = new PendingServerBattleSettlement
                {
                    StageId = completedStageId,
                    FinishKey = "ui-finish-" + nonce,
                    SessionId = started.sessionId,
                    RewardWindowEligible = completedStage.IsBossStage || hadPendingRewardWindow,
                    ConsumeRewardWindow = hadPendingRewardWindow
                };
                await ConfirmPendingServerSettlementAsync();
                return;
            }

            SettleLocalVictory(completedStage);
        }

        private async System.Threading.Tasks.Task ConfirmPendingServerSettlementAsync()
        {
            var pendingSettlement = _pendingServerBattleSettlement;
            if (pendingSettlement == null)
            {
                _settlingServerBattle = false;
                return;
            }
            BattleFinishDto finished;
            try
            {
                finished = ImmortalLootApiClient.Parse<BattleFinishDto>(await _login.ApiClient.FinishBattleAsync(
                    Guid.Parse(pendingSettlement.SessionId), pendingSettlement.FinishKey, pendingSettlement.RewardWindowEligible));
                if (!Guid.TryParse(finished.sessionId, out var confirmedSessionId) ||
                    confirmedSessionId != Guid.Parse(pendingSettlement.SessionId) ||
                    !string.Equals(finished.status, "Finished", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Server battle finish response was incomplete or mismatched.");
            }
            catch (Exception exception)
            {
                _settlingServerBattle = false;
                ScheduleServerSettlementRetry(pendingSettlement, exception);
                return;
            }

            _pendingServerBattleSettlement = null;
            CancelInvoke(nameof(RetryPendingServerSettlement));
            if (!_catalog.Stages.TryGetValue(pendingSettlement.StageId, out var completedStage))
            {
                _settlingServerBattle = false;
                if (guideText != null) guideText.text = $"服务器已确认结算，但本地缺少关卡配置：{pendingSettlement.StageId}";
                return;
            }
            try
            {
                ApplyConfirmedServerVictory(pendingSettlement, completedStage, finished);
            }
            catch (Exception exception)
            {
                _settlingServerBattle = false;
                if (guideText != null) guideText.text = "服务器已确认结算，但本地进度应用失败：" + exception.Message;
                return;
            }

            string postConfirmationWarning = null;
            try { SaveProgress(); }
            catch (Exception exception) { postConfirmationWarning = "本地进度暂未落盘：" + exception.Message; }
            try
            {
                await SynchronizeConfirmedServerVictoryAsync(pendingSettlement, completedStage, finished);
            }
            catch (Exception exception)
            {
                postConfirmationWarning = string.IsNullOrEmpty(postConfirmationWarning)
                    ? "资料同步失败：" + exception.Message
                    : postConfirmationWarning + "；资料同步失败：" + exception.Message;
            }
            finally
            {
                if (!string.IsNullOrEmpty(postConfirmationWarning) && guideText != null)
                    guideText.text = "服务器结算与本地进度已确认，但" + postConfirmationWarning;
                _settlingServerBattle = false;
                CancelInvoke(nameof(SpawnEnemy));
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
            }
        }

        private void ApplyConfirmedServerVictory(PendingServerBattleSettlement pendingSettlement, StageConfig completedStage, BattleFinishDto finished)
        {
            RecordCompletedStageVictory(completedStage);
            _kills++;
            if (pendingSettlement.ConsumeRewardWindow) _pacing.TryConsumeBattleReward();
            if (!string.IsNullOrWhiteSpace(finished.equipmentInstanceId))
                _serverLatestInstanceId = finished.equipmentInstanceId;
        }

        private async System.Threading.Tasks.Task SynchronizeConfirmedServerVictoryAsync(
            PendingServerBattleSettlement pendingSettlement,
            StageConfig completedStage,
            BattleFinishDto finished)
        {
            EquipmentItemDto row = null;
            if (!string.IsNullOrWhiteSpace(finished.equipmentInstanceId))
            {
                var inventory = ImmortalLootApiClient.Parse<InventoryDto>(await _login.ApiClient.GetInventoryAsync());
                if (inventory.equipment != null)
                    foreach (var candidate in inventory.equipment)
                        if (candidate.instanceId == finished.equipmentInstanceId) { row = candidate; break; }
                var item = row == null || string.IsNullOrEmpty(row.instanceJson) ? null : JsonUtility.FromJson<ServerEquipmentDto>(row.instanceJson);
                lootText.text = FormatServerLoot(item, row) + $"\n\n服务器奖励：经验 +{finished.rewardExp:N0} · 灵砂 +{finished.rewardSoftCurrency:N0}";
                _validationTelemetry.TrackOnce("first_equipment_drop", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, row?.quality ?? string.Empty);
                if (Enum.TryParse(row?.quality, true, out EquipmentQuality serverQuality)) _feedback.PlayLoot(serverQuality);
            }
            else if (guideText != null && !completedStage.IsBossStage)
            {
                guideText.text = pendingSettlement.RewardWindowEligible
                    ? $"服务器已记录通过 {completedStage.Name}；本场未生成新装备。"
                    : $"服务器已记录通过 {completedStage.Name}；本场尚未到装备结算窗口。";
            }
            if (completedStage.IsBossStage)
            {
                _guideStep = Math.Max(_guideStep, 4);
                _validationTelemetry.TrackOnce("first_boss_defeated", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, row?.quality ?? string.Empty);
                if (guideText != null) guideText.text = "服务器 Boss 已击败：可领取挂机收益并准备境界突破";
            }
            await RefreshServerProfile();
        }

        private void ScheduleServerSettlementRetry(PendingServerBattleSettlement pendingSettlement, Exception exception)
        {
            CancelInvoke(nameof(SpawnEnemy));
            CancelInvoke(nameof(RetryPendingServerSettlement));
            var delayIndex = Math.Min(pendingSettlement.RetryAttempt, ServerSettlementRetryDelays.Length - 1);
            var delaySeconds = ServerSettlementRetryDelays[delayIndex];
            pendingSettlement.RetryAttempt++;
            if (guideText != null)
                guideText.text = $"服务器结算确认中：关卡与奖励窗口已冻结，将在 {delaySeconds:0} 秒后用同一凭据重试。\n{exception.Message}";
            Invoke(nameof(RetryPendingServerSettlement), delaySeconds);
        }

        private async void RetryPendingServerSettlement()
        {
            if (_pendingServerBattleSettlement == null || _settlingServerBattle) return;
            _settlingServerBattle = true;
            await ConfirmPendingServerSettlementAsync();
        }

        private void RecordBattleDefeat(bool scheduleRetry)
        {
            _stageLoop.RecordDefeat();
            if (guideText != null)
                guideText.text = $"挑战失败：保留 1-{_stageLoop.CurrentStageNumber}，稍后自动重试（本关失败 {_stageLoop.DefeatsOnCurrentStage} 次）。";
            if (scheduleRetry) Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
        }

        private void SettleLocalVictory(StageConfig completedStage)
        {
            var transition = RecordCompletedStageVictory(completedStage);
            var shouldCheckpoint = transition.Advanced ||
                                   transition.ClearResult.IsFirstClear ||
                                   completedStage.IsBossStage;
            _kills++;
            if (transition.ClearResult.IsFirstClear)
                _premiumCurrency += completedStage.FirstClearPremiumCurrency;
            if (completedStage.IsBossStage)
                GrantConfiguredStageRewards(completedStage);

            if (_inventory.State.PendingEquipment != null)
            {
                var skippedRewardWindows = 0;
                while (_pacing.TryConsumeBattleReward()) skippedRewardWindows++;
                _skippedPendingRewardWindows += skippedRewardWindows;
                shouldCheckpoint |= skippedRewardWindows > 0;
                if (guideText != null) guideText.text = skippedRewardWindows > 0
                    ? $"待领取区仍有装备：已明确跳过 {skippedRewardWindows} 个新装备窗口，不会在重启后补发。已结算 {completedStage.Name}，请到背包处理待领取装备。"
                    : $"待领取区仍有装备：已结算 {completedStage.Name}，新的装备结算暂停至待领取区清空。";
                if (completedStage.IsBossStage)
                {
                    _guideStep = Math.Max(_guideStep, 4);
                    _validationTelemetry.TrackOnce("first_boss_defeated", _pacing.ElapsedSeconds, completedStage.StageNumber, _power);
                }
                RefreshProgressDisplay();
                if (shouldCheckpoint) SaveProgress();
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
                return;
            }

            var consumedEquipmentWindow = _pacing.TryConsumeBattleReward();
            if (!completedStage.IsBossStage && consumedEquipmentWindow)
                GrantConfiguredStageRewards(completedStage);
            if (!completedStage.IsBossStage && !consumedEquipmentWindow)
            {
                if (guideText != null)
                    guideText.text = $"已通过 {completedStage.Name} · 修炼积累 {_pacing.ElapsedMinutes}/{_pacingConfig.durationMinutes} 分钟\n下一次装备结算按 {_pacingConfig.equipmentDropSeconds} 秒节奏触发";
                RefreshProgressDisplay();
                if (shouldCheckpoint) SaveProgress();
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
                return;
            }

            var source = completedStage.IsBossStage ? DropSourceType.Boss : DropSourceType.Stage;
            var sourceId = completedStage.MonsterGroup != null && completedStage.MonsterGroup.Length > 0
                ? completedStage.MonsterGroup[0]
                : completedStage.Id;
            var dropTableId = completedStage.IsBossStage
                ? completedStage.DropTableId
                : "drop_prototype_equipment";
            var drop = _drops.Roll(
                dropTableId,
                new DropContext(source, _level, sourceId, transition.ClearResult.IsFirstClear))[0];
            if (drop.Equipment == null)
            {
                if (drop.ItemId == "soft_currency") _softCurrency += Math.Max(0, drop.Count);
                if (lootText != null) lootText.text = $"{completedStage.Name} 阶段掉落：{drop.ItemId} +{drop.Count}";
                RefreshProgressDisplay();
                SaveProgress();
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
                return;
            }

            var overflowReward = new DecompositionReward();
            _latestLoot = drop.Equipment;
            _guideStep = Math.Max(_guideStep, 1);
            if (!TryMakeRoomForLoot(out overflowReward))
            {
                _inventory.StorePendingEquipment(_latestLoot);
                _pendingSacrificeConfirmationKey = string.Empty;
                lootText.text = FormatLoot(_latestLoot) + "\n\n背包已满且没有可安全回收的装备；掉落已保存到待领取区。\n新的装备结算会暂停，直到你清理背包并领取。";
                lootText.color = PrototypeVisualTheme.QualityColor(_latestLoot.Quality);
                _validationTelemetry.TrackOnce("first_equipment_drop", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, _latestLoot.Quality.ToString());
                _feedback.PlayLoot(_latestLoot.Quality);
                SaveProgress();
                Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
                return;
            }
            _inventory.AddEquipment(_latestLoot);
            _validationTelemetry.TrackOnce("first_equipment_drop", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, _latestLoot.Quality.ToString());
            _feedback.PlayLoot(_latestLoot.Quality);
            var autoUpgrade = _settings.AutoEquipEnabled
                ? _upgradeEvaluator.Evaluate(_progressionStats.Calculate(_baseStats), _loadout.Equipped, _latestLoot)
                : new EquipmentUpgradeDecision(false, 0);
            if (autoUpgrade.ShouldEquip)
            {
                _loadout.Equip(_latestLoot);
                _guideStep = Math.Max(_guideStep, 2);
            }
            var overflowSummary = overflowReward.SoftCurrency > 0
                ? $"\n安全回收低价值装备：灵砂 +{overflowReward.SoftCurrency:N0} · 强化石 +{overflowReward.EnhancementMaterial}"
                : string.Empty;
            lootText.text = FormatLoot(_latestLoot) + (autoUpgrade.ShouldEquip
                ? $"\n\n自动换装成功 · 战力预计 +{autoUpgrade.PowerGain}"
                : "\n\n点击“穿戴最新装备”进行比较") + overflowSummary;
            lootText.color = PrototypeVisualTheme.QualityColor(_latestLoot.Quality);
            if (completedStage.IsBossStage)
            {
                _guideStep = Math.Max(_guideStep, 4);
                _validationTelemetry.TrackOnce("first_boss_defeated", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, _latestLoot.Quality.ToString());
                if (guideText != null) guideText.text = "Boss 已击败：可领取挂机收益并准备境界突破";
            }
            RefreshProgressDisplay();
            if (autoUpgrade.ShouldEquip)
            {
                _feedback.PlayEquip();
                _validationTelemetry.TrackOnce("first_equipment_equipped", _pacing.ElapsedSeconds, completedStage.StageNumber, _power, _latestLoot.Quality.ToString(), autoUpgrade.PowerGain);
            }
            SaveProgress();
            Invoke(nameof(SpawnEnemy), 0.65f / _pacingSpeed);
        }

        private void GrantConfiguredStageRewards(StageConfig completedStage)
        {
#if UNITY_INCLUDE_TESTS
            _configuredStageExperienceGranted += Math.Max(0, completedStage.RewardExp);
#endif
            GrantExperience(completedStage.RewardExp);
            _softCurrency += completedStage.RewardSoftCurrency;
        }

        private VictoryDrivenStageTransition RecordCompletedStageVictory(StageConfig completedStage)
        {
            if (!string.Equals(_stageLoop.CurrentStageId, completedStage.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot settle '{completedStage.Id}' while current stage is '{_stageLoop.CurrentStageId}'.");
            var transition = _stageLoop.RecordVictory(_pacing.CurrentStageNumber);
            _stageNumber = _stageLoop.CurrentStageNumber;
            return transition;
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
            if (_inventory.State.PendingEquipment != null &&
                _inventory.State.PendingEquipment.InstanceId == _latestLoot.InstanceId)
            {
                if (!_inventory.TryClaimPendingEquipment(out var claimedPending))
                {
                    if (guideText != null) guideText.text = "待领取装备已安全保存，但背包仍满。请先到背包分解可回收装备。";
                    return;
                }
                _latestLoot = claimedPending;
            }
            var before = _power;
            _loadout.Equip(_latestLoot);
            _guideStep = Math.Max(_guideStep, 2);
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
                JsonPlayerSaveRepository.TryQuarantine(JsonPlayerSaveRepository.DefaultPath);
                return null;
            }
        }

        private static InventoryState RestoreInventory(PlayerSaveSnapshot saved)
        {
            return InventoryStateCodec.Deserialize(saved?.InventoryJson, minimumEquipmentCapacity: 120);
        }

        private void RestoreProgress(PlayerSaveSnapshot saved)
        {
            var realm = _progressState.Realm;
            _level = Math.Max(1, realm.PlayerLevel);
            _exp = Math.Max(0, realm.Experience);
            _realmStage = Math.Max(1, realm.RealmStage);
            _guideStep = Math.Max(0, _progressState.GuideStep);
            _taskClaimed = _progressState.TaskClaimed;
            var fireRootProgress = GetFireRootProgress();
            _spiritualRootPoints = Math.Clamp(fireRootProgress.Level, 0, GetFireRootMaxLevel());
            fireRootProgress.Level = _spiritualRootPoints;
            if (saved != null)
            {
                _kills = Math.Max(0, saved.Kills);
                _softCurrency = Math.Max(0, saved.SoftCurrency);
                _premiumCurrency = Math.Max(0, saved.PremiumCurrency);
            }
            _baseStats.Attack += (_level - 1) * 2f;
            _baseStats.HP += (_level - 1) * 15f;
            _baseStats.FireDamage += _spiritualRootPoints * 0.01f;
            _pacing.Restore(saved?.StageElapsedSeconds ?? 0d);
            _stageNumber = _stageLoop.CurrentStageNumber;
            if (saved == null) return;
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
            var stageRate = _stageLoop.CurrentStage.AfkRewardRate;
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
            var progress = CaptureProgressState();
            _saveRepository.Save(new PlayerSaveSnapshot
            {
                PlayerId = "local-player", Nickname = "云游剑客", Level = _level, Exp = _exp,
                RealmId = progress.Realm.RealmId, RealmStage = _realmStage,
                Kills = _kills, SoftCurrency = _softCurrency,
                PremiumCurrency = _premiumCurrency, StageElapsedSeconds = _pacing.ElapsedSeconds,
                LastActiveUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = InventoryStateCodec.Serialize(_inventory.State),
                EquippedInstanceIdsJson = JsonUtility.ToJson(equipped),
                ProgressJson = PlayerProgressStateCodec.Serialize(progress)
            });
#if UNITY_INCLUDE_TESTS
            _saveOperationCount++;
#endif
        }

        private PlayerProgressState CaptureProgressState()
        {
            _progressState.CurrentStageId = _stageLoop.CurrentStageId;
            _progressState.GuideStep = Math.Max(0, _guideStep);
            _progressState.TaskClaimed = _taskClaimed;
            _progressState.Realm.PlayerLevel = Math.Max(1, _level);
            _progressState.Realm.Experience = Math.Max(0, _exp);
            _progressState.Realm.RealmStage = Math.Max(1, _realmStage);
            GetFireRootProgress().Level = Math.Clamp(_spiritualRootPoints, 0, GetFireRootMaxLevel());
            return _progressState;
        }

        private SpiritualRootProgress GetFireRootProgress()
        {
            _progressState.SpiritualRoots.Roots.RemoveAll(value => value == null);
            var progress = _progressState.SpiritualRoots.Roots.Find(value => value.RootId == "root_fire");
            if (progress != null) return progress;
            progress = new SpiritualRootProgress { RootId = "root_fire" };
            _progressState.SpiritualRoots.Roots.Add(progress);
            return progress;
        }

        private int GetFireRootMaxLevel()
        {
            return _catalog.SpiritualRoots.TryGetValue("root_fire", out var config) ? Math.Max(0, config.MaxLevel) : 0;
        }

        private bool TryMakeRoomForLoot(out DecompositionReward reward)
        {
            reward = new DecompositionReward();
            if (_inventory.State.Equipment.Count < _inventory.State.EquipmentCapacity) return true;
            var protectedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var equipped in _loadout.Equipped.Values) protectedIds.Add(equipped.InstanceId);
            var candidate = InventoryOverflowPolicy.SelectDiscardCandidate(_inventory.State.Equipment, protectedIds);
            if (candidate == null) return false;
            reward = _decomposition.Decompose(candidate.InstanceId);
            if (reward.EnhancementMaterial > 0)
                _inventory.AddStack("item_enhancement_stone", reward.EnhancementMaterial, ItemCategory.Material);
            if (reward.EquipmentEssence > 0)
                _inventory.AddStack("item_equipment_essence", reward.EquipmentEssence, ItemCategory.Material);
            _softCurrency += reward.SoftCurrency;
            return true;
        }

        private string ExecuteInventoryAction()
        {
            var sorted = _inventory.QueryEquipment(EquipmentSortMode.QualityDescending);
            var targets = new List<string>();
            foreach (var item in sorted)
            {
                if (!IsEquipped(item) && !item.IsLocked && item.Quality <= EquipmentQuality.Epic)
                    targets.Add(item.InstanceId);
            }

            var reward = new DecompositionReward();
            foreach (var id in targets) reward += _decomposition.Decompose(id);
            if (targets.Count > 0) _pendingSacrificeConfirmationKey = string.Empty;

            var pendingSummary = string.Empty;
            if (_inventory.TryClaimPendingEquipment(out var claimedPending))
            {
                _latestLoot = claimedPending;
                _pendingSacrificeConfirmationKey = string.Empty;
                pendingSummary = $"\n待领取装备 [{claimedPending.Quality}] {claimedPending.DisplayName} 已收入背包。";
                ShowPendingResolved(claimedPending, "待领取装备已安全收入背包，可立即穿戴；新的装备结算已恢复。");
            }
            else if (_inventory.State.PendingEquipment != null)
            {
                var equippedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var equipped in _loadout.Equipped.Values) equippedIds.Add(equipped.InstanceId);
                var sacrifice = InventoryOverflowPolicy.SelectExplicitSacrificeCandidate(_inventory.State.Equipment, equippedIds);
                if (sacrifice == null)
                {
                    pendingSummary = "\n待领取装备仍安全保留；当前没有可牺牲的未穿戴装备。";
                }
                else
                {
                    var pending = _inventory.State.PendingEquipment;
                    var pendingIsUpgrade = InventoryOverflowPolicy.IsHigherValue(pending, sacrifice);
                    var confirmationKey = (pendingIsUpgrade ? "replace|" : "discard|") + pending.InstanceId + "|" + sacrifice.InstanceId;
                    if (!string.Equals(_pendingSacrificeConfirmationKey, confirmationKey, StringComparison.Ordinal))
                    {
                        _pendingSacrificeConfirmationKey = confirmationKey;
                        return pendingIsUpgrade
                            ? $"背包已满，且全部未穿戴装备均受保护。\n最低价值候选：[{sacrifice.Quality}] {sacrifice.DisplayName} Lv.{sacrifice.Level}\n待领取装备价值更高；再次执行将永久牺牲候选并领取 [{pending.Quality}] {pending.DisplayName}。本次尚未修改任何装备。"
                            : $"背包已满，且全部未穿戴装备均受保护。\n待领取 [{pending.Quality}] {pending.DisplayName} 不高于最低价值旧装备。\n再次执行将放弃并分解待领取装备、保留全部旧装备；本次尚未修改任何装备。";
                    }

                    if (pendingIsUpgrade)
                    {
                        if (!_inventory.TryReplaceEquipmentWithPending(sacrifice.InstanceId, out claimedPending, out var replaced))
                            throw new InvalidOperationException("Pending equipment replacement could not be completed atomically.");
                        reward += _decomposition.Calculate(replaced);
                        _latestLoot = claimedPending;
                        pendingSummary = $"\n已牺牲 [{replaced.Quality}] {replaced.DisplayName}，待领取装备 [{claimedPending.Quality}] {claimedPending.DisplayName} 已收入背包。";
                        ShowPendingResolved(claimedPending, "牺牲替换已完成，待领取装备可立即穿戴；新的装备结算已恢复。");
                    }
                    else
                    {
                        if (!_inventory.TryDiscardPendingEquipment(out var discardedPending))
                            throw new InvalidOperationException("Pending equipment discard could not be completed atomically.");
                        reward += _decomposition.Calculate(discardedPending);
                        _latestLoot = null;
                        pendingSummary = $"\n已放弃并分解待领取 [{discardedPending.Quality}] {discardedPending.DisplayName}；全部旧装备保持不变。";
                        if (lootText != null) lootText.text = "待领取装备已按二次确认分解；新的装备结算已恢复。";
                        if (guideText != null) guideText.text = "低价值待领取装备已分解，全部旧装备保持不变；新的装备结算已恢复。";
                    }
                    _pendingSacrificeConfirmationKey = string.Empty;
                }
            }

            if (reward.EnhancementMaterial > 0)
                _inventory.AddStack("item_enhancement_stone", reward.EnhancementMaterial, ItemCategory.Material);
            if (reward.EquipmentEssence > 0)
                _inventory.AddStack("item_equipment_essence", reward.EquipmentEssence, ItemCategory.Material);
            _softCurrency += reward.SoftCurrency;
            RefreshProgressDisplay();
            SaveProgress();
            return $"背包已按品质降序筛选 · 批量分解 Epic 及以下 {targets.Count} 件\n灵砂 +{reward.SoftCurrency:N0} · 强化石 +{reward.EnhancementMaterial} · 装备精华 +{reward.EquipmentEssence}\n装备 {_inventory.State.Equipment.Count}/{_inventory.State.EquipmentCapacity} · 材料 {_inventory.State.Materials.Count} 类 · 消耗品 {_inventory.State.Consumables.Count} 类\n自动分解时 Legendary/Mythic、锁定及已穿戴装备受保护；牺牲替换或放弃待领取均须二次确认{pendingSummary}";
        }

        private void ShowPendingResolved(EquipmentInstance claimed, string guide)
        {
            if (lootText != null)
            {
                lootText.text = FormatLoot(claimed) + "\n\n" + guide;
                lootText.color = PrototypeVisualTheme.QualityColor(claimed.Quality);
            }
            if (guideText != null) guideText.text = guide;
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
                    if (_login != null && _login.IsServerAuthenticated)
                        return "在线模式装备操作由服务器背包处理；本地待领取区不会被误报为已穿戴。";
                    if (_latestLoot == null) return "尚无装备，先完成一场战斗。";
                    var comparison = new EquipmentComparisonService(_catalog).Compare(_progressionStats.Calculate(_baseStats), _loadout.Equipped, _latestLoot);
                    var comparisonText = $"穿戴比较：攻击 {comparison.AttackDelta:+0.##;-0.##;0} · 生命 {comparison.HpDelta:+0.##;-0.##;0} · 防御 {comparison.DefenseDelta:+0.##;-0.##;0}";
                    if (_inventory.State.PendingEquipment != null && _inventory.State.Equipment.Count >= _inventory.State.EquipmentCapacity)
                        return comparisonText + "\n装备位于待领取区；请先在背包分解可回收装备。";
                    var latestName = _latestLoot.DisplayName;
                    EquipLatest();
                    return $"{comparisonText}\n已穿戴 {latestName}\n统一战力更新为 {_power}";
                case "InventoryPage": return ExecuteInventoryAction();
                case "CultivationPage":
                    _realmStage = Math.Min(10, _realmStage + 1);
                    var equippedBuild = TryEquipNextLearnedBuild(out var nextBuildIndex);
                    if (equippedBuild) _buildIndex = nextBuildIndex;
                    _guideStep = Math.Max(_guideStep, 3);
                    RefreshProgressDisplay();
                    _validationTelemetry.TrackOnce("first_realm_breakthrough", _pacing.ElapsedSeconds, _stageNumber, _power, value: _realmStage);
                    SaveProgress();
                    var buildSummary = equippedBuild
                        ? $"已学习并装备 {BuildName()}"
                        : HasValidSavedBuild(_progressState.Cultivation)
                            ? "没有其他完整已学习组合，保留当前功法"
                            : "当前没有完整已学习的主辅功法组合，保持安全未装配";
                    return $"境界突破至 {_realmStage} 阶\n{buildSummary}\n主修：{PrimaryMethodName()} · 辅修：{AuxiliaryMethodName()}\n统一属性服务重算战力 {_power}";
                case "SpiritualRootPage":
                    var fireRootMaxLevel = GetFireRootMaxLevel();
                    if (_spiritualRootPoints >= fireRootMaxLevel)
                    {
                        _spiritualRootPoints = fireRootMaxLevel;
                        GetFireRootProgress().Level = fireRootMaxLevel;
                        SaveProgress();
                        return $"火灵根已达上限 {_spiritualRootPoints} 点，本次未重复发放成长。";
                    }
                    _spiritualRootPoints++;
                    _baseStats.FireDamage += 0.01f;
                    GetFireRootProgress().Level = _spiritualRootPoints;
                    RefreshProgressDisplay();
                    SaveProgress();
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
                    SaveProgress();
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
            $"设置\n\n声音：{(_settings.SoundEnabled ? "开启" : "关闭")}\n震动：{(_settings.VibrationEnabled ? "开启" : "关闭")}\n自动换装：{(_settings.AutoEquipEnabled ? "开启（仅提升战力）" : "关闭")}\n进度会在暂停、退出和关键成长节点自动保存。";

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

        public string ToggleAutoEquipSetting()
        {
            _settings.ToggleAutoEquip();
            return SettingsSummary();
        }

        public string SaveNowFromSettings()
        {
            SaveProgress();
            return SettingsSummary() + "\n\n进度已安全保存。";
        }

        public string LegalNotice() =>
            "隐私政策与用户协议\n\n本候选版保存本地游戏进度与不含个人身份的验证事件。Development 构建中，只有玩家主动选择本地服务器登录时，才会发送应用随机生成的匿名安装 ID；不会读取设备唯一标识。\n不读取通讯录、定位、相册或广告标识。\n正式外测前须由发行主体补充主体名称、联系邮箱和最终法律文本。";

        private string BuildName() => _buildIndex == 0 ? "火修燃烧" : _buildIndex == 1 ? "雷修暴击" : "血修吸血";

        private void PrepareCultivationState(CultivationMethodState state)
        {
            state.LearnedMethodIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !_catalog.CultivationMethods.ContainsKey(id));
            if (state.LearnedMethodIds.Count == 0)
                foreach (var methodId in _catalog.CultivationMethods.Keys) state.LearnedMethodIds.Add(methodId);
            if (!string.IsNullOrEmpty(state.PrimaryMethodId) &&
                (!_catalog.CultivationMethods.TryGetValue(state.PrimaryMethodId, out var primary) || !primary.IsPrimary ||
                 !state.LearnedMethodIds.Contains(state.PrimaryMethodId)))
                state.PrimaryMethodId = string.Empty;
            for (var i = 0; i < state.AuxiliaryMethodIds.Length; i++)
            {
                var id = state.AuxiliaryMethodIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!_catalog.CultivationMethods.TryGetValue(id, out var auxiliary) || auxiliary.IsPrimary ||
                    !state.LearnedMethodIds.Contains(id))
                    state.AuxiliaryMethodIds[i] = string.Empty;
            }
            if (!string.IsNullOrEmpty(state.AuxiliaryMethodIds[0]) && state.AuxiliaryMethodIds[0] == state.AuxiliaryMethodIds[1])
                state.AuxiliaryMethodIds[1] = string.Empty;
        }

        private bool HasValidSavedBuild(CultivationMethodState state)
        {
            return !string.IsNullOrEmpty(state.PrimaryMethodId) &&
                   state.LearnedMethodIds.Contains(state.PrimaryMethodId);
        }

        private static int ResolveBuildIndex(string primaryMethodId)
        {
            if (primaryMethodId == "method_thunder_pulse") return 1;
            if (primaryMethodId == "method_crimson_well") return 2;
            return 0;
        }

        private bool TryEquipFirstLearnedBuild(out int buildIndex)
        {
            for (var index = 0; index < 3; index++)
            {
                if (!TryEquipBuild(index)) continue;
                buildIndex = index;
                return true;
            }
            buildIndex = 0;
            return false;
        }

        private bool TryEquipNextLearnedBuild(out int buildIndex)
        {
            for (var offset = 1; offset <= 3; offset++)
            {
                var candidate = (_buildIndex + offset) % 3;
                if (!TryEquipBuild(candidate)) continue;
                buildIndex = candidate;
                return true;
            }
            buildIndex = _buildIndex;
            return false;
        }

        private bool TryEquipBuild(int index)
        {
            var primary = index == 0 ? "method_cinder_scripture" : index == 1 ? "method_thunder_pulse" : "method_crimson_well";
            var auxiliary = index == 0 ? "method_ember_breath" : index == 1 ? "method_quick_spark" : "method_blood_return";
            var state = _progressState.Cultivation;
            if (!state.LearnedMethodIds.Contains(primary) || !state.LearnedMethodIds.Contains(auxiliary)) return false;
            for (var slot = 0; slot < state.AuxiliaryMethodIds.Length; slot++) state.AuxiliaryMethodIds[slot] = string.Empty;
            _cultivation.EquipPrimary(primary);
            _cultivation.EquipAuxiliary(0, auxiliary);
            return true;
        }

        private void ClearActiveCultivationBuild()
        {
            var state = _progressState.Cultivation;
            state.PrimaryMethodId = string.Empty;
            for (var slot = 0; slot < state.AuxiliaryMethodIds.Length; slot++) state.AuxiliaryMethodIds[slot] = string.Empty;
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

#if UNITY_INCLUDE_TESTS
        public static void PauseNextBattleForTests() => _pauseNextBattleForTests = true;
        public void ResumeBattleForTests() => _battlePausedForTests = false;
        public void SetPacingSpeedForTests(float speed) => _pacingSpeed = Mathf.Max(1f, speed);
        public void AdvancePacingForTests(double seconds) => _pacing.Advance(Math.Max(0d, seconds));
        public void SaveForTests() => SaveProgress();
        public int SkippedPendingRewardWindowsForTests => _skippedPendingRewardWindows;
        public int PendingRewardWindowsForTests => _pacing.PendingRewards;
        public int SaveOperationCountForTests => _saveOperationCount;
        public double PacingElapsedSecondsForTests => _pacing.ElapsedSeconds;
        public long ExperienceForTests => _exp;
        public long ConfiguredStageExperienceGrantedForTests => _configuredStageExperienceGranted;
        public long SoftCurrencyForTests => _softCurrency;
        public long PremiumCurrencyForTests => _premiumCurrency;
        public string CurrentStageIdForTests => _stageLoop.CurrentStageId;
        public string ActiveBattleStageIdForTests => _activeBattleStageId;
        public int DefeatsOnCurrentStageForTests => _stageLoop.DefeatsOnCurrentStage;
        public string ServerLatestInstanceIdForTests => _serverLatestInstanceId;
        public bool HasPendingServerSettlementForTests => _pendingServerBattleSettlement != null;
        public void ResolveCurrentBattleForTests() => _battle.SkipToResult();
        public void RespawnCurrentBattleForTests()
        {
            if (_pendingServerBattleSettlement != null) return;
            CancelInvoke(nameof(SpawnEnemy));
            SpawnEnemy();
        }
        public void RetryPendingServerSettlementForTests()
        {
            CancelInvoke(nameof(RetryPendingServerSettlement));
            RetryPendingServerSettlement();
        }
        public void RecordDefeatForTests() => RecordBattleDefeat(scheduleRetry: false);
        public PlayerProgressState ProgressForTests =>
            PlayerProgressStateCodec.Deserialize(PlayerProgressStateCodec.Serialize(CaptureProgressState()));
#endif
    }
}
