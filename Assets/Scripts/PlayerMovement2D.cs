using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Cinemachine; // Para Cinemachine

// Adicione a referência à classe Joystick do pacote
// using JoystickPack; // Assumindo o namespace

[RequireComponent(typeof(Rigidbody2D))]
// [RequireComponent(typeof(PlayerInput))] // PlayerInput continua gerenciado manualmente
[RequireComponent(typeof(Collider2D))] // Ainda precisa de um colisor
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick;

    // Referências a componentes
    private Rigidbody2D rb;
    private PlayerInput playerInput; // Obtido, assume que pode começar desabilitado
    private Collider2D playerCollider;

    private Vector2 moveInputFromAction = Vector2.zero;

    private CinemachineVirtualCamera virtualCamera;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        playerCollider = GetComponent<Collider2D>();

        // --- Mudar o Body Type para Dynamic ---
        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Dynamic. Changing to Dynamic for physics-based movement.", this);
            rb.bodyType = RigidbodyType2D.Dynamic; // Força Dynamic
        }
        // --- Configurações importantes para Dynamic em 2D Top-Down ---
        rb.gravityScale = 0; // Sem gravidade
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Impede rotação no eixo Z
        // Pode ajustar Drag Linear/Angular se quiser "fricção" no movimento
        // rb.drag = 5f; // Exemplo
        // rb.angularDrag = 5f; // Exemplo
        // --------------------------------------------------------


        // --- REMOVER: hitBuffer, hitBufferList, maxCollisions, collisionSkin ---
        // Não precisamos mais desses para a colisão manual
        // hitBuffer = new RaycastHit2D[maxCollisions]; // REMOVER
        // hitBufferList = new RaycastHit2D[maxCollisions]; // REMOVER


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
        // Apenas o jogador local controla a velocidade do Rigidbody
        if (!isLocalPlayer)
        {
             // Para jogadores remotos, o Rigidbody Dynamic AINDA EXISTE E INTERAGE COM A FISICA NO CLIENTE
             // A posição é sincronizada pelo NetworkTransform2D, mas o Rigidbody local
             // pode ser empurrado por outros objetos.
             // Se você NÃO quer que o Rigidbody remoto interaja com a física local (exceto pela posição sincronizada),
             // pode mudar o BodyType para Kinematic para !isLocalPlayer no OnStartClient.
             // Mas para simplicidade e para eles poderem colidir com paredes NO CLIENTE, mantenha Dynamic.
             // O NetworkTransform2D geralmente gerencia a autoridade de movimento corretamente.

            return; // Sai do FixedUpdate para jogadores remotos, não processa input ou define velocity
        }

        // --- Lógica de Input (mesma de antes) ---
        Vector2 currentMoveInput;
        if (playerInput != null && playerInput.enabled && virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
             currentMoveInput = virtualJoystick.Direction.normalized;
        }
        else if (playerInput != null && playerInput.enabled)
        {
            currentMoveInput = moveInputFromAction.normalized;
        }
        else
        {
             currentMoveInput = Vector2.zero;
        }
        // -----------------------------------------

        // --- APLICAR VELOCIDADE AO RIGIDBODY DYNAMIC ---
        // A física do Unity cuidará da colisão automaticamente.
        rb.velocity = currentMoveInput * moveSpeed;
        // ---------------------------------------------

        // NOTA: Não usamos rb.MovePosition ou lógica de Cast aqui com Rigidbody Dynamic.
    }


    // --- Detecção de Colisão Trigger (para coletar lixo) ---
    // OnTriggerEnter2D AINDA funciona com Rigidbody Dynamic (se o colisor não for trigger e o outro for, ou ambos forem triggers)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return;

        TrashItem trashItem = other.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            Debug.Log("Local player collided with TrashItem trigger. Sending CmdCollectTrash.");
            CmdCollectTrash(trashItem.GetComponent<NetworkIdentity>());
        }
    }

    // Comando: Chamado pelo cliente local, executado no SERVIDOR
    [Command]
    void CmdCollectTrash(NetworkIdentity trashNetId)
    {
        // Lógica de coleta no servidor permanece a mesma, pois é autoritária
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
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null after TryGetValue."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found on server."); }
    }


    // --- Método chamado pelo PlayerInput ---
    void OnMove(InputValue value)
    {
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Network Callbacks ---

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log("OnStartLocalPlayer called. This is the local player. Enabling input and camera.");

        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = true;
             Debug.Log("PlayerInput component enabled.");
        } else { Debug.LogError("PlayerInput component not found on local player prefab!"); }

        CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (virtualCamera != null)
        {
            virtualCamera.Follow = this.transform;
            Debug.Log("Cinemachine Camera Follow target set to local player.");
        } else { Debug.LogWarning("Cinemachine Virtual Camera not found!"); }

        if (virtualJoystick != null) { /* Habilitar joystick UI */ }
    }

     // REMOVIDA a lógica de desabilitar playerInput aqui
    /*
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer) { ... }
    }
    */


    public override void OnStopClient()
    {
        base.OnStopClient();
    }

     void OnDestroy()
     {
         if (isLocalPlayer)
         {
            CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (virtualCamera != null && virtualCamera.Follow == this.transform)
            {
                virtualCamera.Follow = null;
                Debug.Log("Cinemachine Camera Follow target cleared on player destroy.");
            }
         }
     }
}