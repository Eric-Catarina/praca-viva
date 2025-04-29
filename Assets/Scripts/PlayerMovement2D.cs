using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Cinemachine;

// using JoystickPack; // Adicione se estiver usando o pacote

// A enumeração TrashType DEVE estar definida em outro arquivo (ex: Utilities.cs)
// e não aqui.
// using SeuProjeto.Enums; // Adicione se a enum estiver em um namespace

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick;

    // Referências a componentes
    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private Collider2D playerCollider;

    private Vector2 moveInputFromAction = Vector2.zero;

    private CinemachineVirtualCamera virtualCamera;

    // --- Referência para o script de animação visual (no mesmo GO) ---
    private PlayerVisualAnimation visualAnimator;

    // --- Sincroniza o estado de movimento para que a animação visual rode em todos os clientes ---
    [SyncVar(hook = nameof(OnIsMovingChanged))]
    private bool isMoving = false;
    // ---------------------------------------------------------------------------------------


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        playerCollider = GetComponent<Collider2D>();

        // >>> Obtém a referência para o script de animação visual <<<
        // Este script DEVE estar no mesmo GameObject que PlayerMovement2D
        visualAnimator = GetComponent<PlayerVisualAnimation>();
        if(visualAnimator == null) Debug.LogWarning("PlayerVisualAnimation script not found on player prefab!");


        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Dynamic. Changing to Dynamic.", this);
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // --- Tenta encontrar o Joystick ---
        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<Joystick>();
             #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (virtualJoystick == null) Debug.LogWarning("Virtual Joystick not found in scene.");
             #endif
        }
    }

    void FixedUpdate()
    {
        // FixedUpdate roda em todos os clientes e no servidor.
        // A lógica de input E de sincronização de `isMoving` só roda para o local player no servidor.
        // A aplicação de velocidade roda para o local player no cliente.
        // A sincronização da posição via NetworkTransform2D cuida do resto para players remotos.

        Vector2 currentMoveInput = Vector2.zero; // Input para este frame

        // --- Processa Input APENAS no cliente local ---
        // Verifica se o PlayerInput está habilitado (que SÓ ACONTECE em OnStartLocalPlayer)
        if (isLocalPlayer && playerInput != null && playerInput.enabled)
        {
            if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
            {
                 currentMoveInput = virtualJoystick.Direction.normalized;
            }
            else
            {
                 currentMoveInput = moveInputFromAction.normalized;
            }
        }
        // ------------------------------------------------

        // --- APLICA VELOCIDADE NO CLIENTE LOCAL ---
        // Somente o local player define a velocity
        if (isLocalPlayer)
        {
            rb.velocity = currentMoveInput * moveSpeed;

            // --- SINCRONIZA O ESTADO DE MOVIMENTO PARA O SERVIDOR ---
            // Usa um threshold pequeno para evitar sincronização constante devido a ruído de input.
            bool currentlyMoving = currentMoveInput.magnitude > 0.1f; // Threshold de 0.1f
            if (currentlyMoving != isMoving) // Só envia comando se o estado mudou
            {
                 CmdSetIsMoving(currentlyMoving);
            }
            // -------------------------------------------------------

        }
        // NOTA: Rigidbody Dynamic no cliente remoto seguirá a posição sincronizada pelo NetworkTransform2D,
        // mas também colidirá com a física local. A Layer Collision Matrix impede colisão com outros players.

    }

    // Comando: Chamado pelo cliente local (em FixedUpdate), executado no SERVIDOR
    [Command]
    void CmdSetIsMoving(bool moving)
    {
        // No servidor, apenas define a SyncVar. O hook fará o resto nos clientes.
        isMoving = moving;
        // Debug.Log($"Server: Player {connectionToClient?.connectionId} isMoving set to {isMoving}");
    }


    // --- Detecção de Colisão Trigger (para coletar lixo) ---
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return; // Apenas o jogador local interage

        TrashItem trashItem = other.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            Debug.Log("Local player collided with TrashItem trigger. Sending CmdCollectTrash.");
            CmdCollectTrash(trashItem.GetComponent<NetworkIdentity>());
        }
    }

    // Comando: Chamado pelo cliente local (no OnTriggerEnter2D), executado no SERVIDOR
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
                    // --- AÇÃO CRUCIAL: Chamar o RPC para animar ANTES de destruir ---
                    // O RPC será executado em TODOS os clientes *antes* que o objeto seja destruído.
                    trashItem.RpcAnimateCollection();
                    // --------------------------------------------------------------

                    // --- AÇÃO AUTORITÁRIA: Destruir o objeto na rede (SERVER) ---
                    NetworkServer.Destroy(trashObject);
                    // ----------------------------------------------------------

                    // Notifica o GameManager no servidor
                    GameManager.Instance?.IncrementCollectedCount();
                } else { Debug.LogWarning($"Server: Object {trashNetId.netId} missing TrashItem."); }
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null after TryGetValue."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found on server."); }
    }


    // --- Método chamado pelo PlayerInput ---
    // Este método é chamado apenas para o PlayerInput habilitado (o local player)
    void OnMove(InputValue value)
    {
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Hook da SyncVar `isMoving` ---
    // Chamado em TODOS os clientes (incluindo o Host/cliente local)
    // sempre que a SyncVar `isMoving` muda no servidor.
    void OnIsMovingChanged(bool oldIsMoving, bool newIsMoving)
    {
        // Debug.Log($"IsMoving SyncVar changed: {newIsMoving}");

        // Inicia ou para a animação visual com base no estado sincronizado
        if (visualAnimator != null)
        {
            if (newIsMoving)
            {
                 visualAnimator.StartMoveAnimation();
            }
            else
            {
                 visualAnimator.StopMoveAnimation();
            }
        } else {
             Debug.LogWarning("PlayerVisualAnimation script reference is null. Cannot control animation.");
        }
    }


    // --- Network Callbacks ---

    // Chamado em TODOS os clientes quando o objeto Player é spawnado/ativado.
    public override void OnStartClient()
    {
        base.OnStartClient();
        // Desabilitar visualmente/interativamente elementos UI como joystick para jogadores remotos
        // A lógica de habilitação/desabilitação do PlayerInput foi movida para OnStartLocalPlayer.
        if (!isLocalPlayer)
        {
            // Ex: if (virtualJoystick != null) virtualJoystick.gameObject.SetActive(false);
            // Ex: if (virtualJoystick != null) virtualJoystick.GetComponent<CanvasGroup>().interactable = false;
        }
    }

     // Chamado APENAS no cliente local quando este objeto Player é criado/spawnado.
     public override void OnStartLocalPlayer()
     {
         base.OnStartLocalPlayer();
         Debug.Log("OnStartLocalPlayer called. This is the local player. Enabling input and camera.");

         // Habilita o componente PlayerInput APENAS para o jogador local.
         // Assume que o componente já existe no Prefab mas está desabilitado por padrão.
         playerInput = GetComponent<PlayerInput>();
         if (playerInput != null)
         {
             playerInput.enabled = true;
             Debug.Log("PlayerInput component enabled for local player.");
         } else {
              Debug.LogError("PlayerInput component NOT FOUND on local player prefab! Cannot enable input.");
         }

         // Configurar a Câmera Virtual para seguir ESTE objeto (o local player).
         CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
         if (virtualCamera != null)
         {
             virtualCamera.Follow = this.transform;
             Debug.Log("Cinemachine Camera Follow target set to local player.");
         } else { Debug.LogWarning("Cinemachine Virtual Camera not found!"); }

         // Opcional: Habilitar visualmente/interativamente elementos UI como joystick APENAS para o jogador local.
         if (virtualJoystick != null)
         {
            //  virtualJoystick.gameObject.SetActive(true); // Se ele começa desativado
            //  virtualJoystick.GetComponent<CanvasGroup>().interactable = true; // Se usar CanvasGroup
         }
     }

    // Chamado em TODOS os clientes quando o objeto Player é destruído
    public override void OnStopClient()
    {
        base.OnStopClient();
        // Lógica de limpeza ao sair (se aplicável)
    }

     // Limpeza adicional para a câmera e sequências DoTween
     void OnDestroy()
     {
         // Limpa a referência da câmera virtual APENAS se este for o jogador local que a controlava
         if (isLocalPlayer)
         {
            CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (virtualCamera != null && virtualCamera.Follow == this.transform)
            {
                virtualCamera.Follow = null;
                Debug.Log("Cinemachine Camera Follow target cleared on player destroy.");
            }
         }
         // A sequência de animação visual (se usou PlayerVisualAnimation) deve ser morta no OnDestroy daquele script.
     }
}