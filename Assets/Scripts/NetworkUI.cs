using UnityEngine;
using Mirror;
using Mirror.Discovery; // Necessário para Network Discovery
using TMPro;

public class NetworkUI : MonoBehaviour
{
    [Header("Network Components")]
    [Tooltip("Assign the NetworkManager GameObject (or leave empty if using Singleton)")]
    public NetworkManager networkManager; // Pode continuar usando singleton se preferir

    [Tooltip("Assign the NetworkDiscovery GameObject here")]
    public NetworkDiscovery networkDiscovery; // Referência ao componente Discovery

    [Header("UI Elements")]
    [Tooltip("Assign the InputField for sending chat messages")]
    public TMP_InputField messageInput; // Seu InputField de chat existente

    [Tooltip("Optional: Text to display network status")]
    public TextMeshProUGUI statusText; // Para dar feedback ao usuário

    void Start()
    {
        // Garante que temos as referências necessárias
        if (networkManager == null) networkManager = NetworkManager.singleton;
        if (networkDiscovery == null)
        {
            networkDiscovery = FindObjectOfType<NetworkDiscovery>(); // Tenta encontrar se não foi atribuído
            if (networkDiscovery == null)
                Debug.LogError("NetworkDiscovery component not found or not assigned in the Inspector!");
        }

        // --- IMPORTANTE: Registrar o listener para o evento OnServerFound ---
        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
            UpdateStatus("Idle");
        }
        else
        {
            UpdateStatus("Error: Discovery Missing!");
        }
    }

    void OnDestroy()
    {
        // --- IMPORTANTE: Remover o listener quando o objeto for destruído ---
        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
        }
    }

    // --- Métodos dos Botões ---

    public void StartHostButton()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            UpdateStatus("Starting Host...");
            networkManager.StartHost();
            Debug.Log("Host Started");

            // >>> NOVO: Começa a anunciar o servidor na rede <<<
            if (networkDiscovery != null)
            {
                 networkDiscovery.StopDiscovery(); // Garante que não está procurando ao mesmo tempo
                networkDiscovery.AdvertiseServer();
                UpdateStatus("Hosting & Advertising...");
                Debug.Log("Advertising Server Started");
            } else {
                 UpdateStatus("Hosting (Discovery OFF)");
            }
        }
         else
        {
             Debug.LogWarning("Already connected or hosting.");
             UpdateStatus("Already Active");
        }
    }

    // Renomeie o botão na UI para "Find Game" ou "Join Game"
    public void FindGamesButton() // <<< MÉTODO RENOMEADO/REESCRITO >>>
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            UpdateStatus("Searching for games...");
            // Limpa descobertas anteriores (se houver) e começa a procurar
            if (networkDiscovery != null)
            {
                networkDiscovery.StopDiscovery(); // Para anúncios ou buscas anteriores
                networkDiscovery.StartDiscovery();
                Debug.Log("Started searching for servers...");
            } else {
                UpdateStatus("Error: Discovery Missing!");
                 Debug.LogError("Cannot search for games, NetworkDiscovery is missing!");
            }
        }
         else
        {
             Debug.LogWarning("Already connected or hosting.");
             UpdateStatus("Already Active");
        }
    }

    public void StopConnectionButton()
    {
         bool stoppedSomething = false;
        UpdateStatus("Stopping...");

        // Parar Host
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            networkManager.StopHost();
             // >>> NOVO: Para de anunciar <<<
             if (networkDiscovery != null) networkDiscovery.StopDiscovery();
            Debug.Log("Host Stopped");
             stoppedSomething = true;
        }
        // Parar Cliente
        else if (NetworkClient.isConnected)
        {
            networkManager.StopClient();
            // >>> NOVO: Para de procurar <<<
            if (networkDiscovery != null) networkDiscovery.StopDiscovery();
            Debug.Log("Client Stopped");
             stoppedSomething = true;
        }
        // Parar apenas Servidor (se aplicável)
        else if (NetworkServer.active)
        {
            networkManager.StopServer();
             // >>> NOVO: Para de anunciar <<<
             if (networkDiscovery != null) networkDiscovery.StopDiscovery();
            Debug.Log("Server Stopped");
             stoppedSomething = true;
        }
         else
        {
             // Se não estava ativo, apenas garante que a descoberta parou
             if (networkDiscovery != null) networkDiscovery.StopDiscovery();
        }

         if (stoppedSomething) {
            UpdateStatus("Disconnected");
         } else {
            UpdateStatus("Idle"); // Volta ao estado inicial se nada estava ativo
         }
    }

    // --- Método de Chat (sem alterações) ---
    public void SendChatMessageButton()
    {
        if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text)) return;

        if (NetworkClient.localPlayer != null)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer.GetComponent<PlayerChat>();
            if (localPlayerChat != null)
            {
                localPlayerChat.CmdSendChatMessage(messageInput.text);
                messageInput.text = ""; // Limpa o input field
            }
            else
            {
                Debug.LogError("Local player object does not have PlayerChat component!");
            }
        }
        else
        {
            Debug.LogError("Cannot send message: Local player object not found.");
        }
    }

    // --- Callback do Network Discovery ---

    // Este método será chamado automaticamente pelo NetworkDiscovery quando um servidor for encontrado
    private void OnDiscoveredServer(ServerResponse info)
    {
        // Só tenta conectar se não estivermos já conectados ou tentando conectar
        if (NetworkClient.isConnected || NetworkClient.isConnecting) return;

        Debug.Log($"Discovered Server: {info.EndPoint.Address}");
        UpdateStatus($"Found: {info.EndPoint.Address}. Connecting...");

        // >>> IMPORTANTE: Pare a descoberta ANTES de tentar conectar <<<
        networkDiscovery.StopDiscovery();

        // Define o endereço IP encontrado no NetworkManager
        networkManager.networkAddress = info.EndPoint.Address.ToString();
        // Tenta conectar ao servidor encontrado
        networkManager.StartClient();
    }


    // --- Helper para UI ---
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"Status: {message}";
        }
    }

     // Considere adicionar listeners para eventos do NetworkManager aqui também
     // em Start() e removê-los em OnDestroy() para feedback mais preciso:
     // Ex: networkManager.onClientConnect += () => UpdateStatus("Connected!");
     // Ex: networkManager.onClientError += (conn, error) => UpdateStatus($"Error: {error}");
     // Ex: networkManager.onClientDisconnect += () => UpdateStatus("Disconnected");
}