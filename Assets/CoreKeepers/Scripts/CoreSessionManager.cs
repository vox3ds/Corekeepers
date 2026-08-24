using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoreKeepers
{
    [RequireComponent(typeof(NetworkManager))]
    public sealed class CoreSessionManager : MonoBehaviour
    {
        public const string MenuSceneName = "Menu";
        public const string DebugSceneName = "DebugScene";
        public const int PlayerLimit = 4;
        public const string NicknameProperty = "nickname";
        private const string UnityProfileName = "corekeepers-player";

        private readonly List<ISessionInfo> availableSessions = new();
        private ISession activeSession;
        private bool initialized;
        private bool busy;
        private bool loadingScene;
        private bool refreshing;
        private float nextRefresh;
        private string status = "Connecting to Unity services...";

        public static CoreSessionManager Instance { get; private set; }
        public IReadOnlyList<ISessionInfo> AvailableSessions => availableSessions;
        public ISession ActiveSession => activeSession;
        public bool Initialized => initialized;
        public bool Busy => busy;
        public bool IsInLobby => activeSession != null;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsOpenGame => activeSession != null && !activeSession.IsPrivate;
        public string Status => status;
        public string JoinCode => activeSession?.Code ?? string.Empty;
        public int SessionPlayerCount => activeSession?.PlayerCount ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CoreSettings.ApplyAudioSettings();
        }

        private async void Start()
        {
            if (Instance == this)
                await InitializeServicesAsync();
        }

        private async void Update()
        {
            if (activeSession == null || loadingScene || refreshing || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + 1f;
            refreshing = true;
            try
            {
                await activeSession.RefreshAsync();
            }
            catch (Exception exception)
            {
                if (!loadingScene)
                    status = FriendlyError(exception);
            }
            finally
            {
                refreshing = false;
            }
        }

        public void CreateCampaign(bool openGame)
        {
            if (!CanStartOnlineAction())
                return;

            RunAction(async () =>
            {
                var options = new SessionOptions
                {
                    Name = $"{CoreSettings.Nickname}'s Campaign",
                    MaxPlayers = PlayerLimit,
                    IsPrivate = !openGame
                }.WithRelayNetwork();
                AddNickname(options.PlayerProperties);
                activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                status = "Campaign lobby ready.";
            });
        }

        public void JoinByCode(string code)
        {
            if (!CanStartOnlineAction() || string.IsNullOrWhiteSpace(code))
                return;

            RunAction(async () =>
            {
                activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    code.Trim().ToUpperInvariant(), CreateJoinOptions());
                status = $"Joined {activeSession.Name}.";
            });
        }

        public void JoinOpenSession(string sessionId)
        {
            if (!CanStartOnlineAction() || string.IsNullOrWhiteSpace(sessionId))
                return;

            RunAction(async () =>
            {
                activeSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, CreateJoinOptions());
                status = $"Joined {activeSession.Name}.";
            });
        }

        public void RefreshOpenSessions()
        {
            if (!initialized || busy || activeSession != null)
                return;
            RunAction(RefreshOpenSessionsAsync);
        }

        public void SetOpenGame(bool openGame)
        {
            if (activeSession == null || !IsHost || busy || activeSession.IsPrivate == !openGame)
                return;

            RunAction(async () =>
            {
                var hostSession = activeSession.AsHost();
                hostSession.IsPrivate = !openGame;
                await hostSession.SavePropertiesAsync();
                await activeSession.RefreshAsync();
                status = openGame ? "The campaign is now discoverable." : "The campaign is code only.";
            });
        }

        public void LeaveLobby()
        {
            if (activeSession == null || busy)
                return;

            RunAction(async () =>
            {
                await activeSession.LeaveAsync();
                activeSession = null;
                availableSessions.Clear();
                status = "Lobby closed.";
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
            });
        }

        public void StartCampaignGame()
        {
            if (activeSession == null || !IsHost || loadingScene)
                return;
            CoreLaunchContext.Set(CoreLaunchMode.Campaign);
            LoadDebugScene();
        }

        public void StartDebugHost()
        {
            if (loadingScene)
                return;

            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                status = "NetworkManager is missing.";
                return;
            }

            if (!manager.IsListening && !manager.StartHost())
            {
                status = "Could not start the debug host.";
                return;
            }

            CoreLaunchContext.Set(CoreLaunchMode.DebugHost);
            status = "Starting debug host...";
            LoadDebugScene();
        }

        public void StartOnlineDebugHost()
        {
            if (!CanStartOnlineAction())
                return;

            RunAction(async () =>
            {
                var options = new SessionOptions
                {
                    Name = $"{CoreSettings.Nickname}'s Debug Session",
                    MaxPlayers = PlayerLimit,
                    IsPrivate = true
                }.WithRelayNetwork();
                AddNickname(options.PlayerProperties);
                activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                await WaitForNetworkReadyAsync();
                CoreLaunchContext.Set(CoreLaunchMode.DebugHost);
                status = "Debug session ready. Share the join code.";
                LoadDebugScene();
            });
        }

        private static async Task WaitForNetworkReadyAsync()
        {
            var timeoutAt = Time.realtimeSinceStartup + 10f;
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                if (Time.realtimeSinceStartup >= timeoutAt)
                    throw new TimeoutException("Relay session was created, but NetworkManager did not start in time.");
                await Task.Yield();
            }
        }

        public IReadOnlyList<string> GetPlayerNicknames()
        {
            if (activeSession?.Players == null)
                return Array.Empty<string>();

            return activeSession.Players
                .OrderBy(player => player.Joined)
                .Select((player, index) => player.Properties != null &&
                    player.Properties.TryGetValue(NicknameProperty, out var property) &&
                    !string.IsNullOrWhiteSpace(property.Value)
                        ? property.Value
                        : $"Player {index + 1}")
                .ToArray();
        }

        public void NotifyNicknameChanged()
        {
            status = "Nickname saved. It will be used for new sessions.";
        }

        private void LoadDebugScene()
        {
            loadingScene = true;
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && manager.NetworkConfig.EnableSceneManagement)
                manager.SceneManager.LoadScene(DebugSceneName, LoadSceneMode.Single);
            else
                SceneManager.LoadScene(DebugSceneName);
        }

        private bool CanStartOnlineAction()
        {
            if (!CoreSettings.HasNickname)
            {
                status = "Choose a nickname first.";
                return false;
            }
            return initialized && !busy && activeSession == null;
        }

        private async Task InitializeServicesAsync()
        {
            try
            {
                busy = true;
                await UnityServices.InitializeAsync(new InitializationOptions()
                    .SetProfile(UnityProfileName)
                    .SetEnvironmentName("production"));
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                initialized = true;
                status = "Online services ready.";
                await RefreshOpenSessionsAsync();
            }
            catch (Exception exception)
            {
                status = FriendlyError(exception);
                Debug.LogException(exception);
            }
            finally
            {
                busy = false;
            }
        }

        private async Task RefreshOpenSessionsAsync()
        {
            var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions { Count = 20 });
            availableSessions.Clear();
            availableSessions.AddRange(results.Sessions.Where(session => !session.IsLocked && session.AvailableSlots > 0));
            status = availableSessions.Count == 0 ? "No open campaigns found." : $"Open campaigns: {availableSessions.Count}.";
        }

        private JoinSessionOptions CreateJoinOptions()
        {
            var options = new JoinSessionOptions();
            AddNickname(options.PlayerProperties);
            return options;
        }

        private static void AddNickname(IDictionary<string, PlayerProperty> properties)
        {
            properties[NicknameProperty] = new PlayerProperty(
                CoreSettings.Nickname, VisibilityPropertyOptions.Member);
        }

        private async void RunAction(Func<Task> action)
        {
            try
            {
                busy = true;
                status = "Please wait...";
                await action();
            }
            catch (Exception exception)
            {
                status = FriendlyError(exception);
                Debug.LogException(exception);
            }
            finally
            {
                busy = false;
            }
        }

        private static string FriendlyError(Exception exception)
        {
            if (exception.Message.Contains("project", StringComparison.OrdinalIgnoreCase))
                return "Online setup is incomplete. Link the Unity Cloud project and enable Multiplayer Services.";
            return $"Error: {exception.Message}";
        }
    }
}
