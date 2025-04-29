using UnityEngine;
using Mirror;
using Mirror.Discovery;
using TMPro;
using System;

public class NetworkUI : MonoBehaviour
{
    [Header("Network Components")]
    [Tooltip("Assign the NetworkManager GameObject (or leave empty if using Singleton)")]
    public NetworkManager networkManager;

    [Tooltip("Assign the NetworkDiscovery component/GameObject here")]
    public NetworkDiscovery networkDiscovery;

    [Header("UI Elements")]
    [Tooltip("Assign the InputField for sending chat messages")]
    public TMP_InputField messageInput;

    [Tooltip("Optional: Text to display network status messages")]
    public TextMeshProUGUI statusText;

    [Header("Managers")]
    [Tooltip("Assign the GameObject/Component responsible for showing notifications")]
    public NotificationManager notificationManager;

    #region Unity Lifecycle & Event Subscription

    void Awake()
    {
        InitializeReferences();
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (networkDiscovery != null )
        {
            networkDiscovery.StopDiscovery();
        }
    }

    void InitializeReferences()
    {
        if (networkManager == null) networkManager = NetworkManager.singleton;
        if (networkDiscovery == null) networkDiscovery = FindObjectOfType<NetworkDiscovery>();
        if (notificationManager == null) notificationManager = FindObjectOfType<NotificationManager>();

        if (networkManager == null) Debug.LogError("FATAL: NetworkManager not found!");
        if (networkDiscovery == null) Debug.LogError("NetworkDiscovery component not found! Discovery features will fail.");
        if (notificationManager == null) Debug.LogWarning("NotificationManager not found! Chat messages will not pop up.");
        if (messageInput == null) Debug.LogWarning("Message InputField not assigned in NetworkUI.");
    }

    void SubscribeToEvents()
    {
        PlayerChat.OnMessageReceived += HandleChatMessageReceived;

        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
        }

    }

    void UnsubscribeFromEvents()
    {
        PlayerChat.OnMessageReceived -= HandleChatMessageReceived;

        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
        }

    }

    #endregion

    #region Chat Handling

    private void HandleChatMessageReceived(string sender, string message)
    {
        if (notificationManager != null)
        {
            notificationManager.ShowNotification(sender, message);
        }
        else
        {
            Debug.Log($"[CHAT] {sender}: {message}");
        }
    }

    public void SendChatMessageButton()
    {
        if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text))
        {
            Debug.LogWarning("Cannot send empty message.");
            return;
        }

        GameObject localPlayerObject = NetworkClient.localPlayer?.gameObject;
        if (localPlayerObject != null)
        {
            PlayerChat localPlayerChat = localPlayerObject.GetComponent<PlayerChat>();
            if (localPlayerChat != null)
            {
                string messageToSend = messageInput.text;
                messageInput.text = "";

                localPlayerChat.CmdSendChatMessage(messageToSend);
            }
            else
            {
                Debug.LogError("Local player GameObject does not have PlayerChat component!");
            }
        }
        else
        {
            Debug.LogError("Cannot send message: NetworkClient.localPlayer not found. Are you connected?");
        }
    }

    #endregion

    #region Network Connection & Discovery Buttons

    public void StartHostButton()
    {
        if (NetworkClient.isConnected || NetworkServer.active) {
            Debug.LogWarning("Already connected or hosting.");
             UpdateStatus("Already Active");
            return;
        }

        UpdateStatus("Starting Host...");
        networkManager.StartHost();
        Debug.Log("NetworkManager: Host Started");

        if (networkDiscovery != null)
        {
             networkDiscovery.StopDiscovery();
            networkDiscovery.AdvertiseServer();
            UpdateStatus("Hosting & Advertising...");
            Debug.Log("NetworkDiscovery: Advertising Server");
        } else {
             UpdateStatus("Hosting (Discovery OFF)");
        }
    }

    public void FindGamesButton()
    {
        if (NetworkClient.isConnected || NetworkServer.active) {
             Debug.LogWarning("Already connected or hosting.");
             UpdateStatus("Already Active");
             return;
        }
        if (networkDiscovery == null) {
             Debug.LogError("Cannot search: NetworkDiscovery not available.");
             UpdateStatus("Error: Discovery Missing!");
             return;
        }


        UpdateStatus("Searching for games...");
        networkDiscovery.StopDiscovery();
        networkDiscovery.StartDiscovery();
        Debug.Log("NetworkDiscovery: Started searching...");
    }

    public void StopConnectionButton()
    {
         bool stoppedSomething = false;
        UpdateStatus("Stopping Connection...");

        if (networkDiscovery != null ) {
            networkDiscovery.StopDiscovery();
            Debug.Log("NetworkDiscovery: Stopped");
        }

        if (NetworkServer.active && NetworkClient.isConnected) {
            networkManager.StopHost();
            Debug.Log("NetworkManager: Host Stopped");
             stoppedSomething = true;
        }
        else if (NetworkClient.isConnected) {
            networkManager.StopClient();
            Debug.Log("NetworkManager: Client Stopped");
             stoppedSomething = true;
        }
        else if (NetworkServer.active) {
            networkManager.StopServer();
            Debug.Log("NetworkManager: Server Stopped");
             stoppedSomething = true;
        }

        if (stoppedSomething) {
             UpdateStatus("Disconnected");
        } else {
             UpdateStatus("Idle");
             Debug.Log("StopConnection: Nothing was active.");
        }
    }

    #endregion

    #region Network Discovery Callback

    private void OnDiscoveredServer(ServerResponse info)
    {
        if (NetworkClient.isConnected || NetworkClient.isConnecting || NetworkServer.active) return;

        Debug.Log($"Discovered Server at: {info.EndPoint.Address}");
        UpdateStatus($"Found Server. Connected!");

        networkDiscovery.StopDiscovery();

        networkManager.networkAddress = info.EndPoint.Address.ToString();
        networkManager.StartClient();
    }

    #endregion

    #region UI Helper

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"Status: {message}";
        }
    }

    #endregion

    #region (Optional) NetworkManager Event Handlers for Feedback


    #endregion
}