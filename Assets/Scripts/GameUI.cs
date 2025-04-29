using UnityEngine;
using TMPro;
using Mirror;
// Não precisa de using UnityEngine.UI; se usar TMPro e ter referências para GameObjects/Image
// Não precisa de using UnityEngine.Events;

public class GameUI : MonoBehaviour
{
     // --- Referências para o UITimer ---
     [Header("UI References")]
    // Remova a referência timerText se ela está DENTRO do UITimer script
    // public TextMeshProUGUI timerText;

    public TextMeshProUGUI trashCountText;
    public GameObject resultScreenPanel;
    public TextMeshProUGUI resultText;

    [Tooltip("Referência ao script UITimer na hierarquia (com Imagem Radial e Texto)")]
    public UITimer uiTimer; // <<< Referência para o script UITimer


     // Opcional: Singleton para acesso fácil
     // public static GameUI Instance;
     // void Awake() { if(Instance == null) Instance = this; else if (Instance != this) Destroy(gameObject); }


     void Awake()
     {
          // Tenta encontrar o UITimer se não for atribuído no Inspector
          if (uiTimer == null)
          {
              uiTimer = FindObjectOfType<UITimer>();
               if (uiTimer == null) Debug.LogError("GameUI: UITimer script not found in scene!");
          }

          // Opcional: Certifica que a tela de resultado começa escondida
          if (resultScreenPanel != null) resultScreenPanel.SetActive(false);
     }


    // --- Métodos Chamados pelo GameManager (via Hooks ou RPCs) ---

    // Chamado pelo GameManager.OnCollectedTrashChanged (no cliente)
    public void UpdateCollectedTrashUI(int collected, int total)
    {
         if (trashCountText != null)
         {
             trashCountText.text = $"Lixo: {collected} / {total}";
         } else { Debug.LogWarning("GameUI: trashCountText is null."); }
    }

    // Chamado pelo GameManager.OnTimeChanged (no cliente)
    // Este método CHAMA o método no UITimer para fazer a atualização visual.
    public void UpdateTimerUI(float currentTime, float totalDuration)
    {
        if (uiTimer != null)
        {
             uiTimer.UpdateTimerUI(currentTime, totalDuration);
        } else {
             Debug.LogWarning("GameUI: UITimer reference is null. Cannot update timer UI.");
             // Fallback: Se não tiver UITimer dedicado, pode atualizar o texto aqui se timerText estiver configurado
             // if (timerText != null) { /* ... formatting logic ... */ }
        }
    }

     // Chamado pelo GameManager.RpcEndGame (no cliente)
     public void ShowResultScreen(bool win)
     {
          if (resultScreenPanel != null)
          {
              resultScreenPanel.SetActive(true);
              if (resultText != null)
              {
                  resultText.text = win ? "VITÓRIA!" : "DERROTA!";
                  resultText.color = win ? Color.green : Color.red;
              } else { Debug.LogWarning("GameUI: resultText is null."); }

              // --- Notificar o UITimer que o jogo terminou (para parar animações visuais) ---
              if (uiTimer != null)
              {
                  uiTimer.OnGameEnd(); // Diz ao UITimer para finalizar sua UI visual (ex: mostrar 00:00)
              } else { Debug.LogWarning("GameUI: UITimer reference is null."); }

              // Opcional: Esconder a UI do jogo principal, mostrar botões de voltar ao lobby, etc.
              // FindObjectOfType<LobbyManager>()?.ShowLobbyUI(); // Chamar isto por um botão na tela de resultado
         } else { Debug.LogWarning("GameUI: resultScreenPanel is null. Cannot show result screen."); }
     }

     // Opcional: Método para esconder a tela de resultado
     public void HideResultScreen()
     {
         if (resultScreenPanel != null)
         {
             resultScreenPanel.SetActive(false);
         }
     }

    // ... (Outros métodos se GameUI gerencia mais coisas) ...
}