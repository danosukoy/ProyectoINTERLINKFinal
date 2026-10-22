using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks.Data;

public class LobbyItemData : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private string lobbyId;

    private void Awake()
    {
        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinClicked);
    }

    public void Setup(string displayId, uint currentPlayers, uint maxPlayers, string id)
    {
        lobbyId = id;

        if (lobbyIdText != null)
            lobbyIdText.text = string.IsNullOrEmpty(displayId) ? "Sala Steam" : displayId;

        if (playerCountText != null)
            playerCountText.text = maxPlayers > 0 ? $"{currentPlayers}/{maxPlayers}" : "---";

        if (joinButton != null)
            joinButton.interactable = maxPlayers == 0 || currentPlayers < maxPlayers;
    }

    private void OnJoinClicked()
    {
        if (string.IsNullOrEmpty(lobbyId)) return;

        if (SteamLobbyManager.Instance != null && ulong.TryParse(lobbyId, out ulong steamId))
        {
            SteamLobbyManager.Instance.JoinLobby(new Lobby(steamId));
        }
    }
}
