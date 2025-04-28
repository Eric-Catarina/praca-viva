using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// Adicione a referência à classe Joystick do pacote que você está usando
// Se o script do Joystick estiver em um namespace, você precisará do using correspondente.
// Assumindo que não está em um namespace específico (se estiver, descomente e ajuste):
// using JoystickPack;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
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

    // Armazena o input vindo do Input System (Teclado/Gamepad)
    private Vector2 moveInputFromAction = Vector2.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Kinematic. Consider changing it for direct transform movement in this script.", this);
            rb.gravityScale = 0; // Garante que não tem gravidade em 2D top-down
        }

        // --- REMOVIDO: transform.position = Vector3.zero; ---
        // Esta linha pode interferir com o posicionamento do PlayerPrefab pelo NetworkManager.
        // É melhor deixar o NetworkManager lidar com a posição inicial.
        // ------------------------------------------------------


        // Tenta encontrar o Joystick automaticamente se não for atribuído no Inspector
        // Isso só funciona se houver apenas UM Joystick ativo na cena.
        if (virtualJoystick == null)
        {
            // Tenta encontrar na cena inteira
            virtualJoystick = FindObjectOfType<Joystick>();
            if (virtualJoystick != null)
            {
                 Debug.Log("Virtual Joystick found automatically in scene.");
            } else {
                 // Log de aviso apenas em editor/desenvolvimento
                 #if UNITY_EDITOR || DEVELOPMENT_BUILD
                 Debug.LogWarning("Virtual Joystick reference not set in Inspector and not found automatically. Mobile controls may not work.");
                 #endif
            }
        }
    }

    void FixedUpdate()
    {
        // Apenas o jogador local processa input E se move
        if (!isLocalPlayer)
        {
            return;
        }

        // --- Lógica de Input Condicional ---
        Vector2 currentMoveInput;

        // Verifica se o joystick virtual está atribuído E está sendo usado (magnitude > deadZone)
        if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
            // Debug.Log("Joystick: " + virtualJoystick.Direction); // Log opcional para depuração
            // Prioriza o input do joystick virtual se ele estiver ativo
            currentMoveInput = virtualJoystick.Direction;
        }
        else
        {
            // Caso contrário, usa o input vindo das Actions (Teclado/Gamepad)
            currentMoveInput = moveInputFromAction;
        }
        // ----------------------------------

        // Aplica o movimento usando o input selecionado
        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            // Calcula a nova posição baseada no input e velocidade
            Vector2 newPosition = rb.position + currentMoveInput * moveSpeed * Time.fixedDeltaTime;
            // Move o Rigidbody para a nova posição (suavemente para Kinematic)
            rb.MovePosition(newPosition);
        }
        // Opcional: Se estivesse usando Rigidbody Dynamic, seria algo como:
        // else {
        //    rb.velocity = currentMoveInput * moveSpeed;
        // }

        // Nota: NetworkTransform2D cuidará de sincronizar a posição/rotação
        // do Rigidbody/Transform para os outros clientes.
    }

    // --- Detecção de Colisão Trigger ---
    // Garanta que o PlayerPrefab tem um Collider 2D (não Trigger)
    // e um Rigidbody 2D.
    void OnTriggerEnter2D(Collider2D other)
    {
        // Apenas o jogador local processa input E interage
        // isLocalPlayer garante que este script é do player controlado por este cliente
        if (!isLocalPlayer)
        {
            return;
        }

        // Tenta obter o componente TrashItem do objeto colidido
        TrashItem trashItem = other.GetComponent<TrashItem>();

        // Se colidiu com um item de lixo válido
        if (trashItem != null)
        {
            Debug.Log("Colidiu com um item de lixo.");
             // Opcional: Verifica se o item já foi "coletado" no lado do cliente (se usar isCollected)
             // if (trashItem.isCollected) return;

             // Envia um comando para o servidor para tentar coletar este item
             // Passamos o NetworkIdentity para que o servidor saiba QUAL objeto coletar
             CmdCollectTrash(trashItem.GetComponent<NetworkIdentity>());
        }
    }

    // Comando: Chamado pelo cliente local, executado no SERVIDOR
    [Command]
    void CmdCollectTrash(NetworkIdentity trashNetId)
    {
        // --- CORREÇÃO DA LINHA COM ERRO ---
        // No servidor: Encontra o objeto de lixo usando o NetworkIdentity
        // Usamos TryGetValue para segurança caso o objeto já tenha sido removido por outro cliente/jogador
        if (NetworkServer.spawned.TryGetValue(trashNetId.netId, out NetworkIdentity trashIdentity))
        {
            GameObject trashObject = trashIdentity.gameObject;

            // Verifica se o objeto ainda existe no servidor e tem o componente TrashItem
            if (trashObject != null)
            {
                TrashItem trashItem = trashObject.GetComponent<TrashItem>();

                if (trashItem != null)
                {
                    // Debug.Log($"Server received collection request for {trashItem.type} from {gameObject.name}");

                    // --- Lógica de Coleta no Servidor ---

                    // <<< AÇÃO PRINCIPAL: Destruir o objeto na rede >>>
                    // Isso removerá o objeto para TODOS os clientes sincronizados.
                    // NetworkServer.Destroy é a forma autoritária do servidor de remover um objeto em rede.
                    NetworkServer.Destroy(trashObject);
                    // --------------------------------------------------

                    // Notifica o GameManager no servidor que um item foi coletado
                    // Assumindo que você terá um GameManager Singleton acessível no servidor
                    GameManager.Instance?.IncrementCollectedCount();
                } else {
                    Debug.LogWarning($"Server: Object with netId {trashNetId.netId} found but is missing TrashItem component.");
                }
            } else {
                 // Isso não deveria acontecer se TryGetValue retornou true, mas é uma checagem extra
                 Debug.LogWarning($"Server: TryGetValue found NetworkIdentity for netId {trashNetId.netId} but gameObject is null?");
            }
        }
        else
        {
            // Isso pode acontecer se dois jogadores coletarem o mesmo item quase simultaneamente.
            // O primeiro consegue, o segundo tenta coletar um objeto que já foi destruído no servidor.
            Debug.LogWarning($"Server: Collection request for netId {trashNetId.netId} failed. Object not found or already destroyed.");
        }
    }

    // --- Método chamado pelo PlayerInput (Behavior = Send Messages) ---
    // Este método recebe o input de movimento do novo Input System (Teclado/Gamepad)
    void OnMove(InputValue value)
    {
        // Apenas o jogador local processa input
        if (!isLocalPlayer)
        {
            return;
        }
        // Lê o valor Vector2 da ação "Move" e armazena na variável local
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Network Callbacks ---

    // Chamado em TODOS os clientes quando o objeto Player é spawnado/ativado
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Desabilita o componente PlayerInput e a interação do Joystick
        // se este script NÃO FOR do jogador local.
        // Isso impede que clientes controlem o personagem do outro jogador.
        if (!isLocalPlayer)
        {
            if (playerInput != null) playerInput.enabled = false;
             // Se o seu pacote Joystick tem uma forma de desabilitar a interação (não apenas esconder)
             // virtualJoystick?.GetComponent<CanvasGroup>()?.interactable = false; // Exemplo se usar CanvasGroup
             // Ou simplesmente desativar o GameObject do joystick localmente (se ele for filho da UI do player)
             // virtualJoystick?.gameObject.SetActive(false); // Se o joystick está no prefab do player
        }
        // Se for o jogador local, garante que o input está habilitado (útil se desabilitou antes)
        else
        {
             if (playerInput != null) playerInput.enabled = true;
             // virtualJoystick?.gameObject.SetActive(true); // Se o joystick está no prefab do player
        }
    }

     // Chamado em TODOS os clientes quando o objeto Player é destruído/desativado
     public override void OnStopClient()
    {
        base.OnStopClient();
        // Opcional: Limpeza ou re-habilitação ao sair (se necessário)
         if (playerInput != null) playerInput.enabled = true; // Re-habilita por precaução
         // virtualJoystick?.gameObject.SetActive(true); // Se o joystick está no prefab do player
    }

     // Chamado no SERVIDOR quando o objeto Player é spawnado (para depuração)
     // Chamado no SERVIDOR quando o objeto Player é spawnado (para depuração)
     // 'connectionToClient' refere-se à conexão do cliente que possui este objeto no servidor.
     public override void OnStartServer()
     {
         base.OnStartServer();
         if (connectionToClient != null) // Garante que a conexão existe (deve existir para um player)
         {
             Debug.Log($"Server: Player object for connection {connectionToClient.connectionId} spawned.");
         } else {
             Debug.LogWarning("Server: Player object spawned but connectionToClient is null?");
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
             Debug.LogWarning("Server: Player object stopped but connectionToClient is null?");
         }
     }
}