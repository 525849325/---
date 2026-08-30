using System;
using System.Threading.Tasks;
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
        public ImmortalLootApiClient ApiClient { get; private set; }
        public bool IsServerAuthenticated => _state == LoginState.ActiveServer && ApiClient != null && ApiClient.AccessToken.Length > 0;
        public bool HasPreparedServerSession => _state == LoginState.PreparedServer && ApiClient != null && ApiClient.AccessToken.Length > 0;
        public bool CanEnterOffline => _state == LoginState.Idle;
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
            if (_state != LoginState.Idle) return false;
            _state = LoginState.ActiveOffline;
            RefreshServerLoginButton();
            return true;
        }

        public bool TryCommitServerEntry()
        {
            if (!HasPreparedServerSession) return false;
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
        public void UseAuthenticatedClientForTests(ImmortalLootApiClient client, PlayerProfileDto profile, InventoryDto inventory)
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
            ServerAuthenticated?.Invoke();
        }
#endif

        private void Start()
        {
            if (serverLoginButton == null) return;
            RefreshServerLoginButton();
            if (Debug.isDebugBuild) serverLoginButton.onClick.AddListener(LoginToServer);
            else if (feedbackText != null) feedbackText.text = "点击进入游戏，载入本机离线修炼进度。";
        }

        private async void LoginToServer()
        {
            if (_state != LoginState.Idle) return;
            _state = LoginState.Authenticating;
            var generation = ++_loginGeneration;
            RefreshServerLoginButton();
            if (feedbackText != null) feedbackText.text = "正在连接本地权威服务器……";
            try
            {
                var client = new ImmortalLootApiClient(new UnityWebRequestTransport(serverBaseUrl));
                var installId = new AnonymousInstallIdProvider(new PlayerPrefsInstallIdStore()).GetOrCreate();
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
            serverLoginButton.gameObject.SetActive(Debug.isDebugBuild);
            serverLoginButton.interactable = _state == LoginState.Idle;
        }

        private void OnDestroy()
        {
            _loginGeneration++;
            ServerAuthenticated = null;
        }
    }
}
