using System;
using ImmortalLoot.Network;
using UnityEngine;
using UnityEngine.UI;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeLoginController : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://127.0.0.1:5080";
        [SerializeField] private Button serverLoginButton;
        [SerializeField] private Text feedbackText;
        public ImmortalLootApiClient ApiClient { get; private set; }
        public bool IsServerAuthenticated => ApiClient != null && ApiClient.AccessToken.Length > 0;

#if UNITY_INCLUDE_TESTS
        public void UseAuthenticatedClientForTests(ImmortalLootApiClient client)
        {
            ApiClient = client ?? throw new ArgumentNullException(nameof(client));
            if (!IsServerAuthenticated) throw new InvalidOperationException("The injected test client must already be authenticated.");
        }
#endif

        private void Start()
        {
            if (serverLoginButton != null) serverLoginButton.onClick.AddListener(LoginToServer);
        }

        private async void LoginToServer()
        {
            serverLoginButton.interactable = false;
            if (feedbackText != null) feedbackText.text = "正在连接本地权威服务器……";
            try
            {
                ApiClient = new ImmortalLootApiClient(new UnityWebRequestTransport(serverBaseUrl));
                var deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrWhiteSpace(deviceId)) deviceId = Guid.NewGuid().ToString("N");
                var result = await ApiClient.LoginAsync(deviceId, "云游客");
                if (feedbackText != null) feedbackText.text = result.isNewPlayer ? "服务器建档成功" : "服务器存档已载入";
                var loginPage = GameObject.Find("LoginPage");
                if (loginPage != null) loginPage.SetActive(false);
            }
            catch (Exception exception)
            {
                if (feedbackText != null) feedbackText.text = "服务器登录失败：" + exception.Message;
                serverLoginButton.interactable = true;
            }
        }
    }
}
