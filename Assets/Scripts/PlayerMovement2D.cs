using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System.Collections.Generic; // Para Dictionary (usado no Command)
using Cinemachine; // Para Cinemachine

// Adicione a referência à classe Joystick do pacote
// using JoystickPack; // Assumindo o namespace

[RequireComponent(typeof(Rigidbody2D))]
// >>> REMOVIDO RequireComponent(typeof(PlayerInput)) <<< Não forçamos mais aqui, gerenciamos manualmente
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Máximo número de colisores que o Cast pode detectar")]
    [SerializeField] private int maxCollisions = 10;
    [Tooltip("Distância extra a ser adicionada ao Cast para garantir a detecção")]
    [SerializeField] private float collisionSkin = 0.05f;

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick;

    // Referências a componentes
    private Rigidbody2D rb;
    private PlayerInput playerInput; // Apenas obtido, não Required via attribute
    private Collider2D playerCollider;

    private RaycastHit2D[] hitBuffer;
    private RaycastHit2D[] hitBufferList;

    private Vector2 moveInputFromAction = Vector2.zero;

    // Referência para a Câmera Virtual
    private CinemachineVirtualCamera virtualCamera;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // >>> Obtém o PlayerInput, mas assume que ele pode começar desabilitado ou ausente <<<
        playerInput = GetComponent<PlayerInput>();
        // -------------------------------------------------------------------------------
        playerCollider = GetComponent<Collider2D>();

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Kinematic. Changing to Kinematic.", this);
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        rb.gravityScale = 0;

        hitBuffer = new RaycastHit2D[maxCollisions];
        hitBufferList = new RaycastHit2D[maxCollisions]; // Algumas versões do Cast podem usar isso

        // --- Tenta encontrar o Joystick automaticamente ---
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
        // Apenas o jogador local processa input E se move
        if (!isLocalPlayer) return;

        Vector2 currentMoveInput;
        // Verifica se o PlayerInput está habilitado (significa que é o local player)
        // E se o joystick virtual está sendo usado.
        if (playerInput != null && playerInput.enabled && virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
             currentMoveInput = virtualJoystick.Direction.normalized;
        }
        // Se o PlayerInput está habilitado (local player) mas o joystick não está sendo usado (ou não existe)
        else if (playerInput != null && playerInput.enabled)
        {
            currentMoveInput = moveInputFromAction.normalized;
        }
        // Se o PlayerInput não está habilitado (não é o local player), input é zero
        else
        {
             currentMoveInput = Vector2.zero;
        }


        // --- Lógica de Colisão para Rigidbody Kinematic ---
        Vector2 moveDelta = currentMoveInput * moveSpeed * Time.fixedDeltaTime;

        if (moveDelta.magnitude > float.Epsilon)
        {
            int count = playerCollider.Cast(moveDelta, hitBuffer, moveDelta.magnitude + collisionSkin);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hitBuffer[i];
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    float distanceCanMove = hit.fraction * (moveDelta.magnitude + collisionSkin) - collisionSkin;
                    moveDelta = moveDelta.normalized * Mathf.Max(0, distanceCanMove);
                    if (moveDelta.magnitude < float.Epsilon) break;
                }
            }
            rb.MovePosition(rb.position + moveDelta);
        }
    }


    // --- Detecção de Colisão Trigger ---
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
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null after TryGetValue."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found on server."); }
    }


    // --- Método chamado pelo PlayerInput (recebe input do novo Input System) ---
    // IMPORTANTE: Este método SÓ É CHAMADO se o componente PlayerInput estiver HABILITADO.
    void OnMove(InputValue value)
    {
        // isLocalPlayer já é checado pelo fato do PlayerInput estar habilitado apenas para o local player
        moveInputFromAction = value.Get<Vector2>();
         // Debug.Log($"Input System Move: {moveInputFromAction}");
    }

    // --- Network Callbacks ---

    // >>> NOVO: Chamado APENAS no cliente local quando este objeto Player é criado/spawnado <<<
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log("OnStartLocalPlayer called. This is the local player. Enabling input and camera.");

        // Habilita o componente PlayerInput APENAS para o jogador local
        // Assume que o componente já existe no Prefab mas está desabilitado por padrão
        playerInput = GetComponent<PlayerInput>(); // Obtém a referência novamente por segurança
        if (playerInput != null)
        {
            playerInput.enabled = true;
             Debug.Log("PlayerInput component enabled.");
        } else { Debug.LogError("PlayerInput component not found on local player prefab!"); }


        // --- Configurar a Câmera Virtual para seguir ESTE objeto ---
        CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (virtualCamera != null)
        {
            virtualCamera.Follow = this.transform;
            Debug.Log("Cinemachine Camera Follow target set to local player.");
        } else { Debug.LogWarning("Cinemachine Virtual Camera not found!"); }

        // Opcional: Habilitar a UI do Joystick APENAS para o jogador local
        // Isso depende de onde o GameObject do Joystick está na sua cena.
        // Se ele é um prefab filho do PlayerPrefab, ele já só existirá no local player.
        // Se ele está no Canvas principal da cena, talvez precise ser ativado/desativado.
        if (virtualJoystick != null)
        {
            // virtualJoystick.gameObject.SetActive(true); // Se ele começa desativado no prefab da UI
             // virtualJoystick.GetComponent<CanvasGroup>().interactable = true; // Se usar CanvasGroup para interação
        }
    }


    // >>> REMOVIDO: OnStartClient <<< A lógica foi movida para OnStartLocalPlayer

    // Chamado em TODOS os clientes quando o objeto Player é destruído
    public override void OnStopClient()
    {
        base.OnStopClient();
        // Opcional: Limpeza ou re-habilitação
        // playerInput.enabled = true; // Não é mais necessário re-habilitar aqui
        // virtualJoystick?.gameObject.SetActive(false); // Desativar joystick se ele vive no canvas principal
    }

    // >>> REMOVIDO: OnStartServer e OnStopServer <<< Não são essenciais para este problema e podem ser removidos se não tiverem outra lógica

    /*
     public override void OnStartServer()
     {
         base.OnStartServer();
         if (connectionToClient != null) { Debug.Log($"Server: Player {connectionToClient.connectionId} spawned."); } else { }
     }

     public override void OnStopServer()
     {
         base.OnStopServer();
         if (connectionToClient != null) { Debug.Log($"Server: Player {connectionToClient.connectionId} stopped."); } else { }
     }
    */


     // --- Limpeza adicional para a câmera (Opcional) ---
     void OnDestroy()
     {
         // Verifica se o componente PlayerInput ainda existe antes de tentar Kill Controls
         // (Embora a sequência seja morta no OnDestroy da classe que a gerencia)
         // playerInput?.actions?.Disable(); // Opcional: Desabilitar Actions explicitamente

         // Encontra a câmera virtual novamente para limpar a referência
         // Verifica isLocalPlayer para garantir que apenas o player local limpa a câmera
         if (isLocalPlayer)
         {
            CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (virtualCamera != null && virtualCamera.Follow == this.transform)
            {
                virtualCamera.Follow = null;
                Debug.Log("Cinemachine Camera Follow target cleared on player destroy.");
            }
         }

         // Limpa a animação visual se você usou o script PlayerVisualAnimation
         // PlayerVisualAnimation visualAnim = GetComponent<PlayerVisualAnimation>();
         // visualAnim?.StopMoveAnimation(); // Interrompe a animação se estiver rodando
         // A sequência em si deve ser morta no OnDestroy do PlayerVisualAnimation script.
     }
}