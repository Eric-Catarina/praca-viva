using UnityEngine;
using Mirror;

public class PlayerChat : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))] // SyncVar sincroniza a variável do servidor para os clientes
    public string playerName = "Player"; // Nome padrão

    // Este hook é chamado automaticamente em TODOS os clientes quando a SyncVar playerName muda
    void OnNameChanged(string _oldName, string _newName)
    {
        Debug.Log($"Player name changed from {_oldName} to {_newName}");
        gameObject.name = _newName; // Renomeia o GameObject na cena para clareza
    }

    // Chamado no cliente quando este jogador entra no jogo
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Define um nome único para o jogador local e pede ao servidor para atualizá-lo
        string uniqueName = $"Player [{netId}]"; // netId é um ID único de rede
        CmdSetPlayerName(uniqueName);

        // Só o jogador local pode controlar este objeto
        Debug.Log($"Started Local Player: {uniqueName}");

        // Exemplo: Se você tiver um InputField e botão para chat
        // FindObjectOfType<ChatUI>()?.AssignLocalPlayer(this); // (Necessitaria de um script ChatUI)
    }

    // Commands são chamados pelo cliente, mas EXECUTADOS NO SERVIDOR
    [Command]
    public void CmdSetPlayerName(string newName)
    {
        // Validação no servidor (opcional mas recomendado)
        if (string.IsNullOrWhiteSpace(newName)) return;

        playerName = newName; // Mudar a SyncVar no SERVIDOR fará com que ela sincronize para os clientes
        Debug.Log($"Server received CmdSetPlayerName: {newName}");

        // Exemplo de como o servidor pode enviar uma mensagem de volta para TODOS os clientes
        RpcLogMessage($"Player {newName} has joined!");
    }

    // Comando para enviar uma mensagem de chat
    [Command]
    public void CmdSendChatMessage(string message)
    {
        // Validação no servidor
        if (string.IsNullOrWhiteSpace(message)) return;

        Debug.Log($"Server received chat message from {playerName}: {message}");

        // Envia a mensagem para todos os clientes
        RpcReceiveChatMessage(playerName, message);
    }

    // ClientRpcs são chamados pelo servidor, mas EXECUTADOS EM TODOS OS CLIENTES
    [ClientRpc]
    public void RpcLogMessage(string message)
    {
        // Executado em todos os clientes
        Debug.Log($"Client received log message: {message}");
    }

    [ClientRpc]
    public void RpcReceiveChatMessage(string senderName, string message)
    {
        // Executado em todos os clientes
        Debug.Log($"CHAT [{senderName}]: {message}");
        // Aqui você atualizaria a UI do chat no cliente
        // FindObjectOfType<ChatUI>()?.DisplayMessage(senderName, message); // (Necessitaria de um script ChatUI)
    }


    // --- Exemplo de como chamar o Command (precisaria de UI) ---
     
     // Este método seria chamado pelo botão "Send Message" da UI
     
     public void SendChatMessage(string message)
     {
         if (!isLocalPlayer) // Só o jogador local pode enviar comandos por este objeto
         {
             Debug.LogError("Trying to send message from non-local player object!");
             return;
         }

         if (string.IsNullOrWhiteSpace(message)) return;

         Debug.Log($"Local client sending message: {message}");
         CmdSendChatMessage(message); // Chama o comando no servidor
     }
     
}