using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class NotificationManager : MonoBehaviour // Ou NotificationManagerSimple
{
    [Header("Setup")]
    [Tooltip("Prefab com CanvasGroup, LayoutElement e UM TextMeshProUGUI filho (ex: 'CombinedText')")]
    public GameObject notificationPrefab;
    [Tooltip("Container com VerticalLayoutGroup")]
    public RectTransform notificationContainer;

    [Header("Animation Settings")]
    public float appearDuration = 0.5f;
    [Range(1f, 3f)] public float appearScaleOvershoot = 1.7f;
    public float appearWobbleIntensity = 10f;
    public int appearWobbleVibrato = 10;
    public float displayDuration = 3.0f;
    public float disappearDuration = 0.4f;
    [Range(0.5f, 2f)] public float disappearScalePull = 1.0f;
    public Ease heightShrinkEase = Ease.InQuad;

    // --- VOLUMES (Opcional, para ajustar aqui) ---
    [Header("Sound Volumes")]
    [Range(0f, 1f)] public float appearSoundVolume = 0.8f;
    [Range(0f, 1f)] public float disappearSoundVolume = 0.6f;

    public void ShowNotification(string senderName, string message)
    {
        // ... (Verificações iniciais e instanciação) ...
        if (notificationPrefab == null || notificationContainer == null) { return; }
        GameObject notificationInstance = Instantiate(notificationPrefab, notificationContainer);
        TextMeshProUGUI combinedText = notificationInstance.transform.Find("CombinedText")?.GetComponent<TextMeshProUGUI>();
        if (combinedText == null) { /* ... erro ... */ Destroy(notificationInstance); return; }
        string formattedMessage = $"<b>{senderName}</b>: \"{message}\"";
        combinedText.text = formattedMessage;
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContainer);
        CanvasGroup canvasGroup = notificationInstance.GetComponent<CanvasGroup>();
        LayoutElement layoutElement = notificationInstance.GetComponent<LayoutElement>();
        RectTransform rectTransform = notificationInstance.GetComponent<RectTransform>();
        if (canvasGroup == null || layoutElement == null) { /* ... erro ... */ Destroy(notificationInstance); return; }
        float initialHeight = rectTransform.sizeDelta.y;
        if (layoutElement.preferredHeight < 0) layoutElement.preferredHeight = initialHeight;


        // --- Animação com DoTween Sequence ---
        Sequence sequence = DOTween.Sequence();

        // 1. Estado Inicial para Aparecer
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;

        // --- Parte A: Aparecer ---
        Tween fadeIn = canvasGroup.DOFade(1f, appearDuration * 0.6f).SetEase(Ease.Linear);
        Tween scaleUp = rectTransform.DOScale(1f, appearDuration).SetEase(Ease.OutBack, appearScaleOvershoot);
        Tween wobble = rectTransform.DOShakeRotation(appearDuration, new Vector3(0, 0, appearWobbleIntensity), appearWobbleVibrato, 90, false);

        // --- TOCAR SOM DE APARECER ---
        // Usamos o operador ?. (null-conditional) por segurança, caso o SoundManager não esteja pronto
        SoundManager.Instance?.PlayNotificationAppearSound(appearSoundVolume);
        // ----------------------------

        sequence.Append(fadeIn).Join(scaleUp).Join(wobble);

        // --- Parte B: Esperar ---
        sequence.AppendInterval(displayDuration);

        // --- Parte C: Desaparecer ---
        Tween fadeOut = canvasGroup.DOFade(0f, disappearDuration).SetEase(Ease.InQuad);
        Tween scaleDown = rectTransform.DOScale(0.1f, disappearDuration).SetEase(Ease.InBack, disappearScalePull);
        Tween heightDown = DOTween.To(() => layoutElement.preferredHeight, x => layoutElement.preferredHeight = x, 0f, disappearDuration).SetEase(heightShrinkEase);

        // --- TOCAR SOM DE DESAPARECER ---
        // Toca o som logo quando a animação de saída começa
        sequence.AppendCallback(() => {
            SoundManager.Instance?.PlayNotificationDisappearSound(disappearSoundVolume);
        });
        // -------------------------------

        sequence.Append(fadeOut).Join(scaleDown).Join(heightDown);


        // --- Parte D: Limpeza ---
        sequence.OnComplete(() => { if (notificationInstance != null) Destroy(notificationInstance); });

        sequence.SetUpdate(UpdateType.Normal, true).Play();
    }
}

    