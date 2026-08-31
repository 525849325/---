using System;
using System.Threading.Tasks;
using ImmortalLoot.Debugging;
using ImmortalLoot.Network;
using ImmortalLoot.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeLoginController : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://127.0.0.1:5080";
        [SerializeField] private Button serverLoginButton;
        [SerializeField] private Text feedbackText;
        private LoginState _state;
        private int _loginGeneration;
        private GameSettingsService _settings;
        private Button _privacyAcceptButton;
        private Button _privacyDeclineButton;
        private Button _privacyLegalButton;
        public ImmortalLootApiClient ApiClient { get; private set; }
        public bool IsServerAuthenticated => _state == LoginState.ActiveServer && ApiClient != null && ApiClient.AccessToken.Length > 0;
        public bool HasPreparedServerSession => _state == LoginState.PreparedServer && ApiClient != null && ApiClient.AccessToken.Length > 0;
        public bool CanEnterOffline => _state == LoginState.Idle && _settings != null && _settings.PrivacyConsentDecided;
        public PlayerProfileDto ServerProfile { get; private set; }
        public InventoryDto ServerInventory { get; private set; }
        public event Action ServerAuthenticated;

        private enum LoginState
        {
            Idle,
            Authenticating,
            PreparedServer,
            ActiveOffline,
            ActiveServer
        }

        public bool TryCommitOfflineEntry()
        {
            if (!CanEnterOffline) return false;
            _state = LoginState.ActiveOffline;
            RefreshServerLoginButton();
            return true;
        }

        public bool TryCommitServerEntry()
        {
            if (!HasPreparedServerSession || _settings == null || !_settings.PrivacyAccepted) return false;
            _state = LoginState.ActiveServer;
            RefreshServerLoginButton();
            return true;
        }

        public void CancelPreparedServerEntry()
        {
            if (_state != LoginState.PreparedServer) return;
            ApiClient = null;
            ServerProfile = null;
            ServerInventory = null;
            _state = LoginState.Idle;
            RefreshServerLoginButton();
        }

#if UNITY_INCLUDE_TESTS
        public void UseAuthenticatedClientForTests(
            ImmortalLootApiClient client,
            PlayerProfileDto profile,
            InventoryDto inventory,
            bool notifyAuthenticated = true)
        {
            if (_state != LoginState.Idle) throw new InvalidOperationException("A gameplay entry mode is already committed.");
            var authenticated = client ?? throw new ArgumentNullException(nameof(client));
            if (authenticated.AccessToken.Length == 0) throw new InvalidOperationException("The injected test client must already be authenticated.");
            ValidateServerSnapshot(profile, inventory);
            ApiClient = authenticated;
            ServerProfile = profile;
            ServerInventory = inventory;
            _state = LoginState.PreparedServer;
            RefreshServerLoginButton();
            if (notifyAuthenticated) ServerAuthenticated?.Invoke();
        }
#endif

        private void Start()
        {
            _settings = GameSettingsService.CreateRuntime();
            if (serverLoginButton == null) return;
            CreatePrivacyControls();
            RefreshServerLoginButton();
            if (DevelopmentServerSupported) serverLoginButton.onClick.AddListener(LoginToServer);
            else if (feedbackText != null) feedbackText.text = "点击进入游戏，载入本机离线修炼进度。";
        }

        private async void LoginToServer()
        {
            if (!CanStartServerLogin())
            {
                if (feedbackText != null) feedbackText.text = "需先明确允许，才会创建匿名安装 ID 并连接 Development 本地服务器。";
                RefreshServerLoginButton();
                return;
            }
            _state = LoginState.Authenticating;
            var generation = ++_loginGeneration;
            RefreshServerLoginButton();
            if (feedbackText != null) feedbackText.text = "正在连接本地权威服务器……";
            try
            {
                var client = new ImmortalLootApiClient(
                    new UnityWebRequestTransport(serverBaseUrl),
                    () => DevelopmentServerSupported && _settings != null && _settings.PrivacyAccepted);
                if (!CanStartServerLogin()) throw new InvalidOperationException("Privacy consent changed before login started.");
                var installId = AnonymousInstallIdProvider.CreateRuntime().GetOrCreate();
                var result = await client.LoginAsync(installId, "云游客");
                var profileTask = client.GetProfileAsync();
                var inventoryTask = client.GetInventoryAsync();
                await Task.WhenAll(profileTask, inventoryTask);
                var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(profileTask.Result);
                var inventory = ImmortalLootApiClient.Parse<InventoryDto>(inventoryTask.Result);
                ValidateServerSnapshot(profile, inventory);
                if (this == null || generation != _loginGeneration || _state != LoginState.Authenticating) return;
                ApiClient = client;
                ServerProfile = profile;
                ServerInventory = inventory;
                _state = LoginState.PreparedServer;
                if (feedbackText != null) feedbackText.text = result.isNewPlayer ? "服务器建档成功" : "服务器存档已载入";
                RefreshServerLoginButton();
                ServerAuthenticated?.Invoke();
            }
            catch (Exception exception)
            {
                if (this == null || generation != _loginGeneration || _state != LoginState.Authenticating) return;
                ApiClient = null;
                ServerProfile = null;
                ServerInventory = null;
                _state = LoginState.Idle;
                if (feedbackText != null) feedbackText.text = "服务器登录失败：" + exception.Message;
                RefreshServerLoginButton();
            }
        }

        private static void ValidateServerSnapshot(PlayerProfileDto profile, InventoryDto inventory)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.playerId) ||
                string.IsNullOrWhiteSpace(profile.currentStageId) || profile.clearedStageIds == null)
                throw new InvalidOperationException("Server profile did not contain authoritative stage state.");
            if (inventory == null || inventory.items == null || inventory.equipment == null)
                throw new InvalidOperationException("Server inventory snapshot was incomplete.");
        }

        private void RefreshServerLoginButton()
        {
            if (serverLoginButton == null) return;
            serverLoginButton.gameObject.SetActive(DevelopmentServerSupported);
            serverLoginButton.interactable = CanStartServerLogin();
            RefreshPrivacyControls();
        }

        public void AcceptPrivacyChoice()
        {
            if (_settings == null) _settings = GameSettingsService.CreateRuntime();
            _settings.AcceptPrivacy();
            PlaytestTelemetryRecorder.EnsureInstalled();
            RefreshServerLoginButton();
        }

        public void DeclinePrivacyChoice()
        {
            if (_settings == null) _settings = GameSettingsService.CreateRuntime();
            _settings.DeclinePrivacy();
            RevokeConsentAndClearSession();
        }

        public void RevokeConsentAndClearSession()
        {
            _loginGeneration++;
            ApiClient = null;
            ServerProfile = null;
            ServerInventory = null;
            if (_state != LoginState.ActiveOffline) _state = LoginState.Idle;
            AnonymousInstallIdProvider.CreateRuntime().Clear();
            PlaytestTelemetryRecorder.StopAfterWithdrawal();
            RefreshServerLoginButton();
        }

        private bool CanStartServerLogin()
        {
            return DevelopmentServerSupported && _state == LoginState.Idle &&
                   _settings != null && _settings.PrivacyAccepted;
        }

        private void CreatePrivacyControls()
        {
            _privacyAcceptButton = CreatePrivacyButton("PrivacyAcceptButton", "允许验证", new Vector2(-160f, -70f), AcceptPrivacyChoice);
            _privacyDeclineButton = CreatePrivacyButton("PrivacyDeclineButton", "仅离线游玩", new Vector2(160f, -70f), DeclinePrivacyChoice);
            _privacyLegalButton = CreatePrivacyButton("PrivacyLegalButton", "查看隐私说明", new Vector2(0f, -175f), ShowPrivacyNotice);
            _privacyAcceptButton.GetComponent<RectTransform>().sizeDelta = new Vector2(290f, 90f);
            _privacyDeclineButton.GetComponent<RectTransform>().sizeDelta = new Vector2(290f, 90f);
            _privacyLegalButton.GetComponent<RectTransform>().sizeDelta = new Vector2(620f, 80f);

            var enter = FindIncludingInactive("EnterGameButton")?.GetComponent<RectTransform>();
            if (enter != null) enter.anchoredPosition = new Vector2(0f, -280f);
            serverLoginButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -400f);
            if (feedbackText != null)
            {
                feedbackText.rectTransform.anchoredPosition = new Vector2(0f, -525f);
                feedbackText.rectTransform.sizeDelta = new Vector2(800f, 120f);
                feedbackText.fontSize = 20;
            }
        }

        private Button CreatePrivacyButton(string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var button = Instantiate(serverLoginButton, serverLoginButton.transform.parent);
            button.name = name;
            button.GetComponent<RectTransform>().anchoredPosition = position;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            return button;
        }

        private void RefreshPrivacyControls()
        {
            if (_settings == null) return;
            var undecided = _settings.PrivacyConsent == PrivacyConsentState.Unknown;
            if (_privacyAcceptButton != null) _privacyAcceptButton.gameObject.SetActive(undecided);
            if (_privacyDeclineButton != null) _privacyDeclineButton.gameObject.SetActive(undecided);
            if (_privacyLegalButton != null) _privacyLegalButton.gameObject.SetActive(undecided);
            if (feedbackText == null) return;
            if (undecided)
                feedbackText.text = "请选择：允许本地验证数据与 Development 登录，或仅离线游玩。离线核心内容不要求同意。";
            else if (_settings.PrivacyConsent == PrivacyConsentState.Declined)
                feedbackText.text = "已选择仅离线游玩：不会记录验证事件，也不会创建安装 ID 或发起服务器请求。";
            else if (DevelopmentServerSupported)
                feedbackText.text = "已允许验证；可进入离线游戏，或连接 127.0.0.1 Development 服务器。";
            else
                feedbackText.text = "已允许本地验证；当前 RC 仍为纯离线版本。";
        }

        private void ShowPrivacyNotice()
        {
            if (feedbackText == null) return;
            feedbackText.text = "隐私摘要：仅离线游玩始终可用。明确允许后才记录无直接身份字段的本地验证事件，并开放 127.0.0.1 Development 登录；不读取设备唯一标识、通讯录、定位、相册或广告标识。可在设置中随时撤回。";
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private static bool DevelopmentServerSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private void OnDestroy()
        {
            _loginGeneration++;
            ServerAuthenticated = null;
        }
    }
}
