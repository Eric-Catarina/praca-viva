using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System.Collections.Generic;
using Cinemachine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Máximo número de colisores que o Cast pode detectar")]
    [SerializeField] private int maxCollisions = 10; // Limite de colisores detectados
    [Tooltip("Distância extra a ser adicionada ao Cast para garantir a detecção")]
    [SerializeField] private float collisionSkin = 0.05f; // Pequena margem extra

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick;
    
    [SerializeField]
    private PlayerVisualAnimations playerVisualAnimations;

    // Referências a componentes
    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private Collider2D playerCollider; // Usaremos este para o Cast

    // Array para armazenar os resultados do Cast
    private RaycastHit2D[] hitBuffer;
    private RaycastHit2D[] hitBufferList; // Usado em algumas versões do Cast

    // Armazena o input de Teclado/Gamepad
    private Vector2 moveInputFromAction = Vector2.zero;

    // Referência para a Câmera Virtual
    private CinemachineVirtualCamera virtualCamera;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        playerCollider = GetComponent<Collider2D>();

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Kinematic. Changing to Kinematic for direct movement logic.", this);
            rb.bodyType = RigidbodyType2D.Kinematic; // Força Kinematic se não for
        }
        rb.gravityScale = 0; // Garante sem gravidade

        // Inicializa os buffers de hit
        hitBuffer = new RaycastHit2D[maxCollisions];
        hitBufferList = new RaycastHit2D[maxCollisions];
        

        // --- Tenta encontrar o Joystick ---
        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<Joystick>();
             #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (virtualJoystick == null) Debug.LogWarning("Virtual Joystick not found.");
             #endif
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        Vector2 currentMoveInput;
        if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
            currentMoveInput = virtualJoystick.Direction.normalized; // Use normalized para consistência de velocidade
        }
        else
        {
            currentMoveInput = moveInputFromAction.normalized; // Use normalized
        }

        // --- Nova lógica de Colisão para Rigidbody Kinematic ---
        // Calcula o vetor de movimento desejado para este FixedUpdate
        Vector2 moveDelta = currentMoveInput * moveSpeed * Time.fixedDeltaTime;

        if (moveDelta.magnitude > float.Epsilon) // Move apenas se houver input significativo
        {
            // Realiza um "Cast" do colisor do player ao longo do caminho desejado
            // para ver se ele colidiria com algo.
            // O Cast retorna o número de colisores detectados.
            // Usamos playerCollider.Cast() diretamente.
            int count = playerCollider.Cast(
                moveDelta,  // A direção e distância do cast (nosso moveDelta)
                hitBuffer,  // Buffer para armazenar os resultados das colisões
                moveDelta.magnitude + collisionSkin // Distância máxima do cast + pequena margem
            );

            // Itera sobre os resultados do Cast
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hitBuffer[i];

                // >>> IMPORTANTE <<<
                // Verifica se o objeto colidido NÃO é o próprio jogador (em colisores múltiplos no prefab, etc.)
                // OU se ele não é um Trigger (não queremos parar em Triggers)
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    // Encontramos uma colisão sólida!

                    // Calcula a distância que podemos realmente percorrer antes de colidir
                    // Multiplicamos pela hit.fraction para obter a distância até o ponto de colisão
                    // Subtraímos collisionSkin para garantir que não "grudamos" na parede
                    float distanceCanMove = hit.fraction * (moveDelta.magnitude + collisionSkin) - collisionSkin;

                    // Ajusta o moveDelta para ser apenas a distância que podemos percorrer
                    moveDelta = moveDelta.normalized * Mathf.Max(0, distanceCanMove); // Garante que a distância não é negativa

                    // Se ajustamos o movimento para 0 ou quase 0, podemos sair do loop
                    // pois a próxima colisão (se houver) já foi tratada.
                    if (moveDelta.magnitude < float.Epsilon)
                    {
                         break; // Sai do loop pois não podemos mais nos mover nessa direção
                    }
                }
                // Se for um trigger, OnTiggerEnter2D será chamado, mas não queremos que isso impeça o movimento aqui.
            }

            // Agora que ajustamos o moveDelta para respeitar colisões, aplicamos o movimento
            rb.MovePosition(rb.position + moveDelta);
            playerVisualAnimations.StartMoveAnimation();

        }
        else
        {
            playerVisualAnimations.StopMoveAnimation();
        }
        // -----------------------------------------------------------

        // Nota: NetworkTransform2D sincroniza a posição/rotação.
    }


    // --- Detecção de Colisão Trigger (para coletar lixo) ---
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
        if (NetworkServer.spawned.TryGetValue(trashNetId.netId, out NetworkIdentity trashIdentity))
        {
            GameObject trashObject = trashIdentity.gameObject;
            if (trashObject != null)
            {
                TrashItem trashItem = trashObject.GetComponent<TrashItem>();
                if (trashItem != null)
                {
                    // Debug.Log($"Server received collection request for {trashItem.type} from player {connectionToClient?.connectionId}");

                    // <<< AÇÃO PRINCIPAL: Destruir o objeto na rede >>>
                    NetworkServer.Destroy(trashObject);
                    // --------------------------------------------------

                    GameManager.Instance?.IncrementCollectedCount();
                } else { Debug.LogWarning($"Server: Object {trashNetId.netId} missing TrashItem."); }
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null after TryGetValue."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found on server."); }
    }

    // --- Método chamado pelo PlayerInput ---
    void OnMove(InputValue value)
    {
        if (!isLocalPlayer) return;
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Network Callbacks ---

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
            if (playerInput != null) playerInput.enabled = false;
            // Desabilitar joystick localmente
        }
        else
        {
             if (playerInput != null) playerInput.enabled = true;
             // Habilitar joystick localmente
        }
    }

     // >>> NOVO: Chamado APENAS no cliente local <<<
     public override void OnStartLocalPlayer()
     {
         base.OnStartLocalPlayer();
         Debug.Log("OnStartLocalPlayer called. This is the local player.");

         // --- Configurar a Câmera Virtual ---
         CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
         if (virtualCamera != null)
         {
             virtualCamera.Follow = this.transform;
             Debug.Log("Cinemachine Virtual Camera Follow target set.");
         } else {
             Debug.LogWarning("Cinemachine Virtual Camera not found!");
         }
     }

    public override void OnStopClient()
    {
        base.OnStopClient();
         if (playerInput != null) playerInput.enabled = true;
    }

     public override void OnStartServer()
     {
         base.OnStartServer();
         if (connectionToClient != null)
         {
             // Debug.Log($"Server: Player {connectionToClient.connectionId} spawned.");
         }
     }

     public override void OnStopServer()
     {
         base.OnStopServer();
         if (connectionToClient != null)
         {
             // Debug.Log($"Server: Player {connectionToClient.connectionId} stopped.");
         }
     }

     // --- Limpeza adicional para a câmera (Opcional) ---
     void OnDestroy()
     {
         // Encontra a câmera virtual novamente para limpar a referência
         CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
         if (isLocalPlayer && virtualCamera != null && virtualCamera.Follow == this.transform)
         {
             virtualCamera.Follow = null;
             Debug.Log("Cinemachine Virtual Camera Follow target cleared on player destroy.");
         }
     }
}