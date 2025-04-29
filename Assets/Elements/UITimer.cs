using TMPro;
using UnityEngine;
// Remover using UnityEngine.Events; se OnTimerEnd não for usado
using UnityEngine.UI;

public class UITimer : MonoBehaviour
{
    [SerializeField] private Image radialTimerImage;  // Referência para a imagem radial
    [SerializeField] private TextMeshProUGUI timerText;  // Referência para o texto

    // REMOVIDO toda a lógica de contador local, Start, Update, ResetTimer, OnTimerEnd

    // --- MÉTODO PARA ATUALIZAR A UI (Chamado pelo GameUI) ---
    // Recebe o tempo restante e a duração total do GameManager (via GameUI).
    public void UpdateTimerUI(float currentTime, float totalDuration)
    {
        // --- Atualiza a Imagem Radial ---
        if (radialTimerImage != null)
        {
            // totalDuration deve ser maior que 0 para evitar divisão por zero
            float fillAmount = (totalDuration > 0) ? Mathf.Clamp01(currentTime / totalDuration) : 0f;
            radialTimerImage.fillAmount = fillAmount;

             // Opcional: Mudar a cor do radial timer com base no tempo restante
             // if (currentTime < totalDuration * 0.25f) radialTimerImage.color = Color.red;
             // else { /* ... etc ... */ }
        } else { Debug.LogWarning("UITimer: radialTimerImage is null."); }


        // --- Atualiza o Texto do Timer ---
        if (timerText != null)
        {
            // Garante que o tempo mostrado não é negativo
            float displayTime = Mathf.Max(0, currentTime);

            // Formata para minutos:segundos
            int minutes = Mathf.FloorToInt(displayTime / 60f);
            int seconds = Mathf.FloorToInt(displayTime % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

             // Opcional: Mudar a cor do texto com base no tempo
             // if (displayTime < 10f) timerText.color = Color.red;
             // else { /* ... etc ... */ }
        } else { Debug.LogWarning("UITimer: timerText is null."); }
         // Debug.Log($"UITimer: Updated UI to {currentTime.ToString("F2")} / {totalDuration.ToString("F2")}"); // Log opcional
    }

    // --- MÉTODO PARA LIDAR COM O FIM DO JOGO (Chamado pelo GameUI) ---
    // Usado para garantir que o timer mostra 00:00 e parar animações visuais (se houver).
    public void OnGameEnd()
    {
        // Garante que o timer mostra 00:00
        UpdateTimerUI(0, 1); // Chama UpdateTimerUI com tempo 0

        // Opcional: Parar qualquer animação de "pulsação" do UI (se houver)
        // DOTween.Kill(this.transform); // Se o UITimer tiver animações próprias
        // Se houver animações DoTween no GO com este script, mate-as aqui.
    }
}