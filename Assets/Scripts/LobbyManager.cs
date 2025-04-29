using UnityEngine;
using Mirror;
using TMPro; // Para TextMeshProUGUI

public class LobbyManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("O painel principal do Lobby")]
    public GameObject lobbyPanel; // Certifique-se que este campo está no Inspector
    [Tooltip("O painel principal da UI durante o jogo")]
    public GameObject gameUIPanel; // Certifique-se que este campo está no Inspector

    [Tooltip("Texto para mostrar a dificuldade selecionada")]
    public TextMeshProUGUI selectedDifficultyText; // Opcional

    private int currentSelectedDifficulty = 0; // 0 = Fácil, 1 = Médio, 2 = Difícil

    // O NetworkManager.singleton já é acessível globalmente.
    // [SerializeField] private NetworkManager networkManager; // Não é estritamente necessário

    // --- Métodos Chamados pelos Botões da UI ---

    void Start()
    {
        // Garante que a UI correta está visível no início
        // Se a cena começa com o lobby visível por padrão, ShowLobbyUI() em Start é bom.
        // Se você usa callbacks do NetworkManager para transições, pode não precisar.
        // ShowLobbyUI(); // Descomente se quiser controlar a visibilidade aqui
        SetSelectedDifficulty(0); // Define a dificuldade padrão (Fácil) e atualiza o texto inicial
    }

    // Chamado pelos botões de seleção de dificuldade
    public void SetDifficultyEasy() { SetSelectedDifficulty(0); }
    public void SetDifficultyMedium() { SetSelectedDifficulty(1); }
    public void SetDifficultyHard() { SetSelectedDifficulty(2); }

    void SetSelectedDifficulty(int index)
    {
         if (index >= 0 && index <= 2) // Validação básica dos índices
         {
             currentSelectedDifficulty = index;
             Debug.Log($"Difficulty selected: {index}");
             // Atualiza o texto na UI (Opcional)
    
             StartHostButton();
             
         } else {
              Debug.LogWarning($"Invalid difficulty index passed: {index}");
         }
    }

    // --- NOVO MÉTODO: Chamado pelo botão "Start Host" ---
    public void StartHostButton()
    {
         // Verifica se o GameManager Singleton existe.
         // Ele deve existir na cena e ser um NetworkBehaviour.
         if (GameManager.Instance != null)
         {
             // PASSO CRUCIAL: Define a dificuldade no GameManager ANTES de iniciar o Host.
             // Essa dificuldade será usada no OnStartServer do GameManager.
             GameManager.Instance.SetDifficultyIndex(currentSelectedDifficulty);
             Debug.Log($"Setting GameManager difficulty to index {currentSelectedDifficulty} and attempting to Start Host...");

             // Inicia o Host do NetworkManager

             // A transição de UI (Lobby -> Jogo) deve ser gerenciada pelos callbacks
             // do NetworkManager em OnEnable/OnDisable deste script ou em outro lugar.
             // Ex: ShowGameUI(); // Se você não usar os callbacks em OnEnable/OnDisable
         } else {
             Debug.LogError("GameManager Instance not found in the scene! Cannot start game.");
         }
    }

    // --- Adicione os outros métodos do LobbyManager aqui ---
    // StartClientButton(), StopConnectionButton(), ShowLobbyUI(), ShowGameUI()
    // E os NetworkManager Callbacks em OnEnable/OnDisable

     // Exemplo dos outros métodos (copie-os da minha resposta anterior)
     /*
     public void StartClientButton() { ... NetworkManager.singleton.StartClient(); ... }
     public void StopConnectionButton() { ... NetworkManager.singleton.StopHost(); NetworkManager.singleton.StopClient(); ... }
     public void ShowLobbyUI() { if (lobbyPanel != null) lobbyPanel.SetActive(true); if (gameUIPanel != null) gameUIPanel.SetActive(false); }
     public void ShowGameUI() { if (lobbyPanel != null) lobbyPanel.SetActive(false); if (gameUIPanel != null) gameUIPanel.SetActive(true); }

     void OnEnable() { if (NetworkManager.singleton != null) { ... } }
     void OnDisable() { if (NetworkManager.singleton != null) { ... } }
     */
}