using UnityEngine;
using Mirror;
using Mirror.Discovery; // Necessário para Network Discovery
using TMPro;
using System; // Necessário para Action (eventos)

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
    public NotificationManager notificationManager; // Sua classe simplificada pode chamar NotificationManagerSimple

    #region Unity Lifecycle & Event Subscription

    void Awake()
    {
        // Garante referências e se inscreve nos eventos ANTES de qualquer Start
        InitializeReferences();
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        // IMPORTANTE: Se desinscrever para evitar memory leaks e erros
        UnsubscribeFromEvents();
         // Garante que a descoberta pare se este objeto for destruído
        if (networkDiscovery != null )
        {
            networkDiscovery.StopDiscovery();
        }
    }

    void InitializeReferences()
    {
        if (networkManager == null) networkManager = NetworkManager.singleton;
        if (networkDiscovery == null) networkDiscovery = FindObjectOfType<NetworkDiscovery>();
        if (notificationManager == null) notificationManager = FindObjectOfType<NotificationManager>(); // Ajuste o tipo se renomeou para NotificationManagerSimple

        // Log de erros se algo essencial estiver faltando
        if (networkManager == null) Debug.LogError("FATAL: NetworkManager not found!");
        if (networkDiscovery == null) Debug.LogError("NetworkDiscovery component not found! Discovery features will fail.");
        if (notificationManager == null) Debug.LogWarning("NotificationManager not found! Chat messages will not pop up.");
        if (messageInput == null) Debug.LogWarning("Message InputField not assigned in NetworkUI.");
    }

    void SubscribeToEvents()
    {
        // Inscreve-se no evento estático do PlayerChat
        PlayerChat.OnMessageReceived += HandleChatMessageReceived;

        // Inscreve-se no evento de descoberta de servidor
        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
        }

        // (Opcional mas recomendado) Inscreva-se nos eventos do NetworkManager para feedback
        // networkManager.onClientConnect += OnClientConnect;
        // networkManager.onClientDisconnect += OnClientDisconnect;
        // networkManager.onServerError += OnServerError;
        // networkManager.onClientError += OnClientError;
    }

    void UnsubscribeFromEvents()
    {
        // Desinscreve-se do evento estático do PlayerChat
        PlayerChat.OnMessageReceived -= HandleChatMessageReceived;

        // Desinscreve-se do evento de descoberta
        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
        }

        // (Opcional) Desinscreva-se dos eventos do NetworkManager
        // networkManager.onClientConnect -= OnClientConnect;
        // networkManager.onClientDisconnect -= OnClientDisconnect;
        // networkManager.onServerError -= OnServerError;
        // networkManager.onClientError -= OnClientError;
    }

    #endregion

    #region Chat Handling

    // Este método é CHAMADO PELO EVENTO estático PlayerChat.OnMessageReceived
    private void HandleChatMessageReceived(string sender, string message)
    {
        if (notificationManager != null)
        {
            notificationManager.ShowNotification(sender, message);
        }
        else
        {
            // Fallback: Loga no console se não puder mostrar notificação
            Debug.Log($"[CHAT] {sender}: {message}");
        }
    }

    // Método chamado pelo BOTÃO "Send" da UI
    public void SendChatMessageButton()
    {
        if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text))
        {
            Debug.LogWarning("Cannot send empty message.");
            return;
        }

        // Precisamos do objeto do JOGADOR LOCAL para enviar o comando
        GameObject localPlayerObject = NetworkClient.localPlayer?.gameObject;
        if (localPlayerObject != null)
        {
            PlayerChat localPlayerChat = localPlayerObject.GetComponent<PlayerChat>();
            if (localPlayerChat != null)
            {
                string messageToSend = messageInput.text;
                messageInput.text = ""; // Limpa o input

                localPlayerChat.CmdSendChatMessage(messageToSend); // Envia o comando
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
             networkDiscovery.StopDiscovery(); // Garante que não está procurando
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
        networkDiscovery.StopDiscovery(); // Para anúncios ou buscas anteriores
        networkDiscovery.StartDiscovery();
        Debug.Log("NetworkDiscovery: Started searching...");
    }

    public void StopConnectionButton()
    {
         bool stoppedSomething = false;
        UpdateStatus("Stopping Connection...");

        // Para o NetworkDiscovery primeiro, se estiver ativo
        if (networkDiscovery != null ) {
            networkDiscovery.StopDiscovery();
            Debug.Log("NetworkDiscovery: Stopped");
        }

        // Para o NetworkManager
        if (NetworkServer.active && NetworkClient.isConnected) { // Era Host
            networkManager.StopHost();
            Debug.Log("NetworkManager: Host Stopped");
             stoppedSomething = true;
        }
        else if (NetworkClient.isConnected) { // Era só Cliente
            networkManager.StopClient();
            Debug.Log("NetworkManager: Client Stopped");
             stoppedSomething = true;
        }
        else if (NetworkServer.active) { // Era só Servidor dedicado
            networkManager.StopServer();
            Debug.Log("NetworkManager: Server Stopped");
             stoppedSomething = true;
        }

        if (stoppedSomething) {
             UpdateStatus("Disconnected");
        } else {
             UpdateStatus("Idle"); // Se nada estava ativo, volta para Idle
             Debug.Log("StopConnection: Nothing was active.");
        }
    }

    #endregion

    #region Network Discovery Callback

    // Método chamado pelo NetworkDiscovery.OnServerFound
    private void OnDiscoveredServer(ServerResponse info)
    {
        // Ignora se já estamos conectados, tentando conectar ou se somos o host
        if (NetworkClient.isConnected || NetworkClient.isConnecting || NetworkServer.active) return;

        Debug.Log($"Discovered Server at: {info.EndPoint.Address}");
        UpdateStatus($"Found Server. Connected!");

        // Para a descoberta antes de tentar conectar
        networkDiscovery.StopDiscovery();

        // Define o endereço e inicia o cliente
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

    // void OnClientConnect() { UpdateStatus("Connected!"); Debug.Log("Client Connected!"); }
    // void OnClientDisconnect() { UpdateStatus("Disconnected"); Debug.Log("Client Disconnected"); }
    // void OnClientError(NetworkConnection conn, TransportError error, string reason) { UpdateStatus($"Client Error: {reason}"); Debug.LogError($"Client Error: {error} - {reason}"); }
    // void OnServerError(NetworkConnection conn, TransportError error, string reason) { UpdateStatus($"Server Error: {reason}"); Debug.LogError($"Server Error: {error} - {reason}"); }

    #endregion
}