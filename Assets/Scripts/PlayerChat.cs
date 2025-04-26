using UnityEngine;
using Mirror;
using System; // <<< ADICIONADO para usar Action (eventos)

public class PlayerChat : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))] // SyncVar sincroniza a variável do servidor para os clientes
    public string playerName = "Player"; // Nome padrão

    // --- EVENTO ESTÁTICO PARA NOTIFICAR A UI ---
    // Qualquer script (como NetworkUI) pode se inscrever neste evento.
    // Argumentos: <nome_remetente, mensagem_recebida>
    public static event Action<string, string> OnMessageReceived;
    // ------------------------------------------

    // Este hook é chamado automaticamente em TODOS os clientes quando a SyncVar playerName muda
    void OnNameChanged(string oldName, string newName)
    {
        // Mantém o nome do GameObject atualizado para facilitar a depuração na hierarquia
        gameObject.name = $"Player [{newName}]";
        // Debug.Log($"Player name updated from {oldName} to {newName}"); // Log opcional
    }

    // Chamado no cliente quando este jogador específico entra no jogo (é o jogador local)
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Define um nome único inicial e pede ao servidor para defini-lo via Command
        string uniqueName = $"Player_{netId}"; // Usar netId garante unicidade na sessão
        CmdSetPlayerName(uniqueName);

        Debug.Log($"Initialized Local Player: {uniqueName}");
    }

    // Commands são chamados pelo cliente, mas EXECUTADOS NO SERVIDOR
    [Command]
    public void CmdSetPlayerName(string newName)
    {
        // Validação no servidor é uma boa prática
        if (string.IsNullOrWhiteSpace(newName)) return;

        playerName = newName; // Mudar a SyncVar no SERVIDOR fará com que ela sincronize e chame o hook nos clientes
        // Debug.Log($"Server set player name to: {newName}"); // Log opcional no servidor
    }

    // Comando para enviar uma mensagem de chat: chamado pela UI do cliente, executado no SERVIDOR
    [Command]
    public void CmdSendChatMessage(string message)
    {
        // Validação no servidor
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning($"Player {playerName} tried to send an empty message.");
            return;
        }

        // Debug.Log($"Server received chat from {playerName}: {message}"); // Log opcional no servidor

        // O servidor recebeu, agora distribui para todos os clientes via ClientRpc
        RpcReceiveChatMessage(playerName, message); // Usa o playerName atual do jogador no servidor
    }

    // ClientRpcs são chamados pelo servidor, mas EXECUTADOS EM TODOS OS CLIENTES conectados
    [ClientRpc]
    public void RpcLogMessage(string message) // Este RPC parece ser para logs gerais, mantido como estava
    {
        // Executado em todos os clientes
        Debug.Log($"Client Log Message: {message}");
    }

    [ClientRpc]
    public void RpcReceiveChatMessage(string senderName, string message)
    {
        // Este código é executado em CADA cliente quando uma mensagem de chat chega.

        // <<< MODIFICADO: Invoca o evento estático >>>
        // A UI (NetworkUI) estará ouvindo este evento para mostrar a notificação.
        OnMessageReceived?.Invoke(senderName, message);
        // ---------------------------------------------

        // O antigo Debug.Log foi movido ou substituído pela lógica do evento/UI.
        // Você pode adicionar um log aqui se ainda quiser confirmação no console do cliente:
        // Debug.Log($"Client received RPC: [{senderName}] {message} - Invoking OnMessageReceived event.");
    }

    // --- Método antigo para chamar o Command, agora redundante ---
    /*
     // Este método NÃO é mais necessário aqui, pois o NetworkUI.SendChatMessageButton
     // agora encontra o PlayerChat local e chama CmdSendChatMessage diretamente.
     public void SendChatMessage(string message)
     {
         if (!isLocalPlayer)
         {
             Debug.LogError("Trying to send message from non-local player object!");
             return;
         }
         if (string.IsNullOrWhiteSpace(message)) return;
         Debug.Log($"Local client sending message: {message}");
         CmdSendChatMessage(message);
     }
    */
}