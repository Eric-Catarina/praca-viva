using UnityEngine;
using UnityEngine.UI; // Para LayoutRebuilder
using TMPro;
using DG.Tweening; // Namespace do DoTween

public class NotificationManager : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("O Prefab do painel de notificação (com CanvasGroup e TextMeshProUGUIs dentro)")]
    public GameObject notificationPrefab; // Arraste seu Prefab aqui

    [Tooltip("O GameObject que contém o VerticalLayoutGroup onde as notificações serão adicionadas")]
    public RectTransform notificationContainer; // Arraste o GameObject pai (com VerticalLayoutGroup) aqui

    [Header("Animation Settings")]
    [Tooltip("Quanto tempo a notificação fica totalmente visível")]
    public float displayDuration = 3.0f;
    [Tooltip("Duração do fade in e movimento inicial")]
    public float fadeInDuration = 0.4f;
    [Tooltip("Duração do fade out final")]
    public float fadeOutDuration = 0.6f;
    [Tooltip("Distância vertical que a notificação sobe ao aparecer")]
    public float moveUpDistance = 50f;

    // Método público para ser chamado quando uma mensagem chegar
    public void ShowNotification(string senderName, string message)
    {
        if (notificationPrefab == null || notificationContainer == null)
        {
            Debug.LogError("Notification Prefab or Container not set in NotificationManagerSimple!");
            return;
        }

        // 1. Instancia o Prefab diretamente como filho do container
        // O VerticalLayoutGroup no 'notificationContainer' vai posicioná-lo automaticamente.
        GameObject notificationInstance = Instantiate(notificationPrefab, notificationContainer);

        // 2. Encontra os componentes de texto dentro da instância recém-criada
        // Adapte "SenderText" e "MessageText" para os nomes reais dos GameObjects no seu Prefab
        TextMeshProUGUI senderText = notificationInstance.transform.Find("SenderText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI messageText = notificationInstance.transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();

        // 3. Define o texto
        if (senderText != null) senderText.text = senderName;
        if (messageText != null) messageText.text = message;

        // 4. Força o Layout Group a recalcular imediatamente (útil se o tamanho do texto mudar)
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContainer); // Recalcula o container pai

        // 5. Pega referências para animar (CanvasGroup é essencial para fade)
        CanvasGroup canvasGroup = notificationInstance.GetComponent<CanvasGroup>();
        RectTransform rectTransform = notificationInstance.GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            Debug.LogError("Notification Prefab needs a CanvasGroup component for animations!");
            Destroy(notificationInstance); // Destroi se não puder animar
            return;
        }

        // --- Animação com DoTween Sequence ---

        // 6. Define o estado inicial ANTES da animação
        canvasGroup.alpha = 0f; // Começa totalmente transparente
        // Move para baixo inicialmente para poder "subir" para a posição correta
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y - moveUpDistance);

        // 7. Cria a sequência de animação
        Sequence sequence = DOTween.Sequence();

        // A. Aparecer: Fade In + Mover para cima (para a posição final definida pelo Layout Group)
        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad));
        sequence.Join(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + moveUpDistance, fadeInDuration).SetEase(Ease.OutQuad));

        // B. Esperar: Fica visível pela duração definida
        sequence.AppendInterval(displayDuration);

        // C. Desaparecer: Fade Out
        sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad));

        // D. Ao completar: Destruir o GameObject da notificação
        sequence.OnComplete(() =>
        {
            // Garante que estamos destruindo o objeto correto, mesmo que haja um pequeno delay
            if (notificationInstance != null)
            {
                Destroy(notificationInstance);
            }
        });

        // 8. Inicia a animação
        sequence.Play();
    }
}