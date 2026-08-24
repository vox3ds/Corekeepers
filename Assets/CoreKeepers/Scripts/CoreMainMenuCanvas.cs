using System;
using UnityEngine;
using UnityEngine.UI;

namespace CoreKeepers
{
    public sealed class CoreMainMenuCanvas : MonoBehaviour
    {
        [Header("Main")]
        [SerializeField] private Text nicknameText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button campaignButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button pvpButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button debugButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button editNicknameButton;
        [SerializeField] private InputField joinCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Toggle openGameToggle;

        [Header("Open Games Slots")]
        [SerializeField] private GameObject[] openGameRows;
        [SerializeField] private Text[] openGameLabels;
        [SerializeField] private Button[] openGameButtons;

        [Header("Panels")]
        [SerializeField] private GameObject nicknameModal;
        [SerializeField] private InputField nicknameInput;
        [SerializeField] private Button nicknameConfirmButton;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button optionsSaveButton;
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private GameObject comingSoonPanel;
        [SerializeField] private Button comingSoonBackButton;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Text lobbyCodeText;
        [SerializeField] private Text lobbyPlayersText;
        [SerializeField] private Text lobbyModeText;
        [SerializeField] private Button lobbyStartButton;
        [SerializeField] private Button lobbyBackButton;

        private float nextUiRefresh;
        private CoreSessionManager Sessions => CoreSessionManager.Instance;

        private void Awake()
        {
            campaignButton.onClick.AddListener(CreateCampaign);
            hostButton.onClick.AddListener(CreateCampaign);
            pvpButton.onClick.AddListener(() => comingSoonPanel.SetActive(true));
            optionsButton.onClick.AddListener(OpenOptions);
            debugButton.onClick.AddListener(() => Sessions?.StartOnlineDebugHost());
            quitButton.onClick.AddListener(Quit);
            editNicknameButton.onClick.AddListener(OpenNickname);
            joinButton.onClick.AddListener(() => Sessions?.JoinByCode(joinCodeInput.text));
            refreshButton.onClick.AddListener(() => Sessions?.RefreshOpenSessions());
            nicknameConfirmButton.onClick.AddListener(SaveNickname);
            optionsSaveButton.onClick.AddListener(SaveOptions);
            optionsBackButton.onClick.AddListener(() => optionsPanel.SetActive(false));
            comingSoonBackButton.onClick.AddListener(() => comingSoonPanel.SetActive(false));
            lobbyStartButton.onClick.AddListener(() => Sessions?.StartCampaignGame());
            lobbyBackButton.onClick.AddListener(LeaveLobby);
            openGameToggle.onValueChanged.AddListener(OnOpenGameChanged);

            for (var index = 0; index < openGameButtons.Length; index++)
            {
                var slot = index;
                openGameButtons[index].onClick.AddListener(() => JoinOpenGame(slot));
            }
        }

        private void Start()
        {
            nicknameInput.text = CoreSettings.Nickname;
            nicknameModal.SetActive(!CoreSettings.HasNickname);
            optionsPanel.SetActive(false);
            comingSoonPanel.SetActive(false);
            lobbyPanel.SetActive(false);
            masterSlider.value = CoreSettings.MasterVolume;
            musicSlider.value = CoreSettings.MusicVolume;
            sfxSlider.value = CoreSettings.SfxVolume;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RefreshUi();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUiRefresh)
                return;
            nextUiRefresh = Time.unscaledTime + 0.25f;
            RefreshUi();
        }

        private void RefreshUi()
        {
            nicknameText.text = string.IsNullOrWhiteSpace(CoreSettings.Nickname) ? "Choose Nickname" : CoreSettings.Nickname;
            statusText.text = Sessions?.Status ?? "Starting online systems...";
            var ready = Sessions != null && Sessions.Initialized && !Sessions.Busy && CoreSettings.HasNickname;
            campaignButton.interactable = ready && !Sessions.IsInLobby;
            hostButton.interactable = ready && !Sessions.IsInLobby;
            joinButton.interactable = ready && !string.IsNullOrWhiteSpace(joinCodeInput.text) && !Sessions.IsInLobby;
            refreshButton.interactable = ready && !Sessions.IsInLobby;

            var inLobby = Sessions != null && Sessions.IsInLobby;
            lobbyPanel.SetActive(inLobby);
            if (inLobby)
            {
                lobbyCodeText.text = $"JOIN CODE\n{Sessions.JoinCode}";
                var names = Sessions.GetPlayerNicknames();
                lobbyPlayersText.text = $"PLAYERS {Sessions.SessionPlayerCount}/{CoreSessionManager.PlayerLimit}\n" +
                                        string.Join("\n", names);
                lobbyModeText.text = Sessions.IsOpenGame ? "OPEN GAME" : "PRIVATE / CODE ONLY";
                lobbyStartButton.gameObject.SetActive(Sessions.IsHost);
                lobbyStartButton.interactable = Sessions.IsHost && !Sessions.Busy;
                openGameToggle.SetIsOnWithoutNotify(Sessions.IsOpenGame);
            }

            for (var index = 0; index < openGameRows.Length; index++)
            {
                var visible = Sessions != null && index < Sessions.AvailableSessions.Count && !inLobby;
                openGameRows[index].SetActive(visible);
                if (!visible)
                    continue;
                var session = Sessions.AvailableSessions[index];
                openGameLabels[index].text = $"{session.Name}\n{session.MaxPlayers - session.AvailableSlots}/{session.MaxPlayers}";
                openGameButtons[index].interactable = session.AvailableSlots > 0 && !Sessions.Busy;
            }
        }

        private void CreateCampaign()
        {
            Sessions?.CreateCampaign(openGameToggle.isOn);
        }

        private void JoinOpenGame(int slot)
        {
            if (Sessions == null || slot < 0 || slot >= Sessions.AvailableSessions.Count)
                return;
            Sessions.JoinOpenSession(Sessions.AvailableSessions[slot].Id);
        }

        private void OpenNickname()
        {
            nicknameInput.text = CoreSettings.Nickname;
            nicknameModal.SetActive(true);
            nicknameInput.ActivateInputField();
        }

        private void SaveNickname()
        {
            if (!CoreSettings.TrySetNickname(nicknameInput.text))
                return;
            nicknameModal.SetActive(false);
            Sessions?.NotifyNicknameChanged();
        }

        private void OpenOptions()
        {
            masterSlider.value = CoreSettings.MasterVolume;
            musicSlider.value = CoreSettings.MusicVolume;
            sfxSlider.value = CoreSettings.SfxVolume;
            optionsPanel.SetActive(true);
        }

        private void SaveOptions()
        {
            CoreSettings.SetVolumes(masterSlider.value, musicSlider.value, sfxSlider.value);
            optionsPanel.SetActive(false);
        }

        private void OnOpenGameChanged(bool value)
        {
            if (Sessions != null && Sessions.IsInLobby && Sessions.IsHost)
                Sessions.SetOpenGame(value);
        }

        private void LeaveLobby()
        {
            Sessions?.LeaveLobby();
            lobbyPanel.SetActive(false);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
