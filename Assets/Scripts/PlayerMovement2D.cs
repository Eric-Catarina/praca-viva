using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// Adicione a referência à classe Joystick do pacote que você está usando
// Se o script do Joystick estiver em um namespace, você precisará do using correspondente.
// Assumindo que não está em um namespace específico:
// using JoystickPack; // Exemplo se estivesse em um namespace

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
            Debug.LogWarning("Rigidbody2D BodyType is not Kinematic.", this);
            rb.gravityScale = 0;
        }
        
        transform.position = Vector3.zero;

        // Tenta encontrar o Joystick automaticamente se não for atribuído
        // Isso só funciona se houver apenas UM Joystick ativo na cena.
        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<Joystick>();
            if (virtualJoystick != null)
            {
                 Debug.Log("Virtual Joystick found automatically.");
            } else {
                 // Desativa o log em builds, pois é esperado não encontrar em PC
                 #if UNITY_EDITOR || DEVELOPMENT_BUILD
                 Debug.LogWarning("Virtual Joystick reference not set in Inspector and not found automatically. Mobile controls may not work.");
                 #endif
            }

        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer)
        {
            return; // Apenas o jogador local se move
        }

        // --- Lógica de Input Condicional ---
        Vector2 currentMoveInput;

        // Verifica se o joystick virtual está atribuído E está sendo usado
        if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
        {
        Debug.Log("Joystick: " + virtualJoystick.Direction);
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
            Vector2 newPosition = rb.position + currentMoveInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(newPosition);
        }
        // else { rb.velocity = currentMoveInput * moveSpeed; } // Para Rigidbody Dynamic
    }

    // --- Método chamado pelo PlayerInput (Behavior = Send Messages) ---
    // Atualiza a variável que armazena o input vindo de Teclado/Gamepad
    void OnMove(InputValue value)
    {
        if (!isLocalPlayer)
        {
            return;
        }
        // Lê o valor Vector2 da ação "Move" e armazena
        moveInputFromAction = value.Get<Vector2>();
    }

    // --- Habilitar/Desabilitar Input (Sem alterações necessárias aqui) ---
    public override void OnStartClient()
    {
        if (!isLocalPlayer && playerInput != null)
        {
            playerInput.enabled = false;
        }
         // Garante que o joystick só seja interativo para o jogador local
         // (pode não ser necessário dependendo de como o Canvas está configurado)
         /*
         if (virtualJoystick != null) {
             virtualJoystick.gameObject.SetActive(isLocalPlayer);
         }
         */
    }

     public override void OnStopClient()
    {
         if (playerInput != null) {
             playerInput.enabled = true;
         }
         /*
          if (virtualJoystick != null) {
             virtualJoystick.gameObject.SetActive(false); // Esconde ao desconectar
         }
         */
    }
}