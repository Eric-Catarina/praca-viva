using UnityEngine;
using Mirror;
using TMPro; // Se estiver usando TextMeshPro para InputField

public class NetworkUI : MonoBehaviour
{
    public TMP_InputField messageInput; // Arraste o InputField aqui (opcional)
    // Se não tiver o InputField, remova as linhas relacionadas a ele

    public void StartHostButton()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkManager.singleton.StartHost();
            Debug.Log("Host Started");
        }
    }

    public void StartClientButton()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            // O endereço padrão é 'localhost' no NetworkManager
            NetworkManager.singleton.StartClient();
            Debug.Log("Client Started");
        }
    }

    public void StopConnectionButton()
    {
        // Se for host, para host
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
            Debug.Log("Host Stopped");
        }
        // Se for só cliente, para cliente
        else if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
            Debug.Log("Client Stopped");
        }
        // Se for só servidor, para servidor (menos comum para testes simples)
        else if (NetworkServer.active)
        {
            NetworkManager.singleton.StopServer();
            Debug.Log("Server Stopped");
        }
    }

    // Este método seria chamado pelo botão "Send Message" da UI
    public void SendChatMessageButton()
    {
         if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text)) return; // Verifica se tem input

        // Precisamos encontrar o objeto do jogador LOCAL para chamar o comando
        if (NetworkClient.localPlayer != null)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer.GetComponent<PlayerChat>();
            if (localPlayerChat != null)
            {
                 localPlayerChat.CmdSendChatMessage(messageInput.text); // Chama o comando DIRETAMENTE (Mirror lida com o envio)
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
}