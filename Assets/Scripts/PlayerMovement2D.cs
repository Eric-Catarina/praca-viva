using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System.Collections.Generic; // Para Dictionary
using Cinemachine; // <<< ADICIONADO para Cinemachine

// Adicione a referência à classe Joystick do pacote
// using JoystickPack;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Collider2D))] // Garante um colisor
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick; // Referência ao Joystick do pacote

    // Referências a componentes
    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private Collider2D playerCollider;

    // Armazena o input de Teclado/Gamepad
    private Vector2 moveInputFromAction = Vector2.zero;

    // --- Referência para a Câmera Virtual (não precisa atribuir no Inspector, será encontrada) ---
    private CinemachineVirtualCamera virtualCamera;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        playerCollider = GetComponent<Collider2D>();

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Kinematic.", this);
            rb.gravityScale = 0;
        }

        // --- Tenta encontrar o Joystick automaticamente ---
        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<Joystick>();
             #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (virtualJoystick == null) Debug.LogWarning("Virtual Joystick reference not set and not found.");
             #endif
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer)
        {
            return; // Apenas o jogador local se move
        }

        Vector2 currentMoveInput;
        if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
            currentMoveInput = virtualJoystick.Direction;
        }
        else
        {
            currentMoveInput = moveInputFromAction;
        }

        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            Vector2 newPosition = rb.position + currentMoveInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(newPosition);
        }
    }

    // --- Detecção de Colisão Trigger ---
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return; // Apenas o jogador local interage

        TrashItem trashItem = other.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            Debug.Log("Colidiu com um item de lixo.");
            CmdCollectTrash(trashItem.GetComponent<NetworkIdentity>());
        }
    }

    // Comando: Chamado pelo cliente local, executado no SERVIDOR
    [Command]
    void CmdCollectTrash(NetworkIdentity trashNetId)
    {
        if (NetworkServer.spawned.TryGetValue(trashNetId.netId, out NetworkIdentity trashIdentity))
        {
            GameObject trashObject = trashIdentity.gameObject;
            if (trashObject != null)
            {
                TrashItem trashItem = trashObject.GetComponent<TrashItem>();
                if (trashItem != null)
                {
                    NetworkServer.Destroy(trashObject);
                    GameManager.Instance?.IncrementCollectedCount();
                } else { Debug.LogWarning($"Server: Object {trashNetId.netId} missing TrashItem."); }
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found."); }
    }

    // --- Método chamado pelo PlayerInput ---
    void OnMove(InputValue value)
    {
        if (!isLocalPlayer) return;
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Network Callbacks ---

    // Chamado em TODOS os clientes quando o objeto Player é spawnado/ativado
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
            if (playerInput != null) playerInput.enabled = false;
            // Desabilitar joystick localmente se aplicável
        }
        else
        {
             if (playerInput != null) playerInput.enabled = true;
             // Habilitar joystick localmente se aplicável
        }
    }

     // >>> NOVO: Chamado APENAS no cliente local quando este objeto Player é criado/spawnado <<<
     public override void OnStartLocalPlayer()
     {
         base.OnStartLocalPlayer();
         Debug.Log("OnStartLocalPlayer called. This is the local player.");

         // --- Configurar a Câmera Virtual para seguir ESTE objeto ---
         // Encontra a Câmera Virtual na cena (assumindo que só há uma para os jogadores)
         virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

         if (virtualCamera != null)
         {
             // Define o Transform deste objeto como o alvo a ser seguido pela câmera
             virtualCamera.Follow = this.transform;
             Debug.Log("Cinemachine Virtual Camera Follow target set to local player.");
         } else {
             Debug.LogWarning("Cinemachine Virtual Camera not found in scene!");
         }
         // ---------------------------------------------------------
     }

    // Chamado em TODOS os clientes quando o objeto Player é destruído
     public override void OnStopClient()
    {
        base.OnStopClient();
        // Limpeza ou re-habilitação
         if (playerInput != null) playerInput.enabled = true;
         // Desabilitar joystick localmente se aplicável
    }

     // Chamado no SERVIDOR quando o objeto Player é spawnado (para depuração)
     public override void OnStartServer()
     {
         base.OnStartServer();
         if (connectionToClient != null)
         {
             Debug.Log($"Server: Player object for connection {connectionToClient.connectionId} spawned.");
         } else {
             // Debug.Log("Server: Player object spawned for Host.");
         }
     }

     // Chamado no SERVIDOR quando o objeto Player é destruído (para depuração)
     public override void OnStopServer()
     {
         base.OnStopServer();
         if (connectionToClient != null)
         {
             Debug.Log($"Server: Player object for connection {connectionToClient.connectionId} stopped.");
         } else {
             // Debug.Log("Server: Player object stopped for Host.");
         }
     }

     // >>> NOVO: Limpeza adicional para a câmera (Opcional) <<<
     // Se o objeto Player for destruído enquanto a câmera ainda o segue
     // É bom limpar a referência para evitar erros.
     void OnDestroy()
     {
         if (isLocalPlayer && virtualCamera != null && virtualCamera.Follow == this.transform)
         {
             virtualCamera.Follow = null;
             Debug.Log("Cinemachine Virtual Camera Follow target cleared on player destroy.");
         }
     }
}