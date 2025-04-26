using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Certifique-se que este script esteja anexado a um objeto na cena
// e as referências 'notificationPrefab' e 'notificationContainer' estejam corretamente atribuídas no Inspector.
public class NotificationManager : MonoBehaviour // Ou NotificationManagerSimple
{
    [Header("Setup")]
    [Tooltip("IMPORTANTE: O Prefab DEVE ter os componentes CanvasGroup e LayoutElement em seu objeto raiz.")]
    public GameObject notificationPrefab;

    [Tooltip("IMPORTANTE: O Container DEVE ter um VerticalLayoutGroup configurado corretamente.")]
    public RectTransform notificationContainer;

    [Header("Appear Animation")]
    public float appearDuration = 0.5f;
    [Range(1f, 3f)] public float appearScaleOvershoot = 1.7f;
    public float appearWobbleIntensity = 10f;
    public int appearWobbleVibrato = 10;

    [Header("Display & Disappear Animation")]
    public float displayDuration = 3.0f;
    public float disappearDuration = 0.4f;
    [Range(0.5f, 2f)] public float disappearScalePull = 1.0f;
    [Tooltip("Ease para a animação de encolhimento de altura")]
    public Ease heightShrinkEase = Ease.InQuad;

    public void ShowNotification(string senderName, string message)
    {
        // --- Verificações Iniciais ---
        if (notificationPrefab == null) { Debug.LogError("Notification Prefab not assigned!"); return; }
        if (notificationContainer == null) { Debug.LogError("Notification Container not assigned!"); return; }
        if (notificationContainer.GetComponent<VerticalLayoutGroup>() == null) { Debug.LogWarning("Notification Container is missing VerticalLayoutGroup!"); }

        // --- Instanciação e Configuração do Texto ---
        GameObject notificationInstance = Instantiate(notificationPrefab, notificationContainer);

        TextMeshProUGUI senderText = notificationInstance.transform.Find("SenderText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI messageText = notificationInstance.transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();

        if (senderText != null) senderText.text = senderName;
        else Debug.LogWarning("Could not find 'SenderText' TMP_Text on prefab instance.");

        if (messageText != null) messageText.text = message;
        else Debug.LogWarning("Could not find 'MessageText' TMP_Text on prefab instance.");

        // --- Forçar Layout e Obter Componentes ---
        // Força o VLG a calcular posições/tamanhos imediatamente
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContainer);

        CanvasGroup canvasGroup = notificationInstance.GetComponent<CanvasGroup>();
        RectTransform rectTransform = notificationInstance.GetComponent<RectTransform>();
        LayoutElement layoutElement = notificationInstance.GetComponent<LayoutElement>(); // <<< Pega o LayoutElement

        // --- Verificações de Componentes Cruciais ---
        if (canvasGroup == null) { Debug.LogError("Prefab is MISSING CanvasGroup component!"); Destroy(notificationInstance); return; }
        if (layoutElement == null) { Debug.LogError("Prefab is MISSING LayoutElement component! Smooth vertical movement will FAIL."); Destroy(notificationInstance); return; }

        // Define a altura preferida inicial baseada no tamanho calculado pelo VLG,
        // se ela não estiver definida (> -1) no prefab. Isso é importante para a animação de altura.
        float initialHeight = rectTransform.sizeDelta.y;
        if (layoutElement.preferredHeight < 0) {
             layoutElement.preferredHeight = initialHeight;
        }


        // --- Animação com DoTween Sequence ---
        Sequence sequence = DOTween.Sequence();

        // 1. Estado Inicial para Aparecer
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;

        // --- Parte A: Aparecer (Animações de entrada) ---
        Tween fadeIn = canvasGroup.DOFade(1f, appearDuration * 0.6f).SetEase(Ease.Linear);
        Tween scaleUp = rectTransform.DOScale(1f, appearDuration).SetEase(Ease.OutBack, appearScaleOvershoot);
        Tween wobble = rectTransform.DOShakeRotation(appearDuration, new Vector3(0, 0, appearWobbleIntensity), appearWobbleVibrato, 90, false);
        sequence.Append(fadeIn);
        sequence.Join(scaleUp);
        sequence.Join(wobble);

        // --- Parte B: Esperar (Duração visível) ---
        sequence.AppendInterval(displayDuration);

        // --- Parte C: Desaparecer (Animações de saída) ---
        Tween fadeOut = canvasGroup.DOFade(0f, disappearDuration).SetEase(Ease.InQuad);
        Tween scaleDown = rectTransform.DOScale(0.1f, disappearDuration).SetEase(Ease.InBack, disappearScalePull);

        // --- Animar a Altura Preferida do LayoutElement para Zero ---
        // Esta é a chave para o movimento suave dos elementos abaixo no VLG
        Tween heightDown = DOTween.To(
            () => layoutElement.preferredHeight,   // Getter: Lê a altura atual
            x => layoutElement.preferredHeight = x, // Setter: Define a nova altura animada
            0f,                                    // Target Value: Altura final zero
            disappearDuration                      // Duration: Mesma dos outros fades/scales
        ).SetEase(heightShrinkEase);               // Aplica a Ease definida
        // ---------------------------------------------------------

        // Adiciona os tweens de desaparecer para executarem em paralelo
        sequence.Append(fadeOut);
        sequence.Join(scaleDown);
        sequence.Join(heightDown); // <<< Junta a animação de altura

        // --- Parte D: Limpeza (Após a animação completar) ---
        sequence.OnComplete(() =>
        {
            // Garante que a instância ainda existe antes de tentar destruir
            if (notificationInstance != null)
            {
                Destroy(notificationInstance);
            }
        });

        // Configura a atualização e inicia a animação
        sequence.SetUpdate(UpdateType.Normal, true); // Ignora Time.timeScale
        sequence.Play();
    }

    // Se precisar do helper para atrasar destruição (alternativa/diagnóstico):
    /*
    private System.Collections.IEnumerator DestroyAfterDelay(GameObject instance, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (instance != null)
        {
            Destroy(instance);
        }
    }
    */
}