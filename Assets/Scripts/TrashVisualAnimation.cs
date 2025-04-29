using UnityEngine;
using DG.Tweening;

public class TrashVisualAnimation : MonoBehaviour // Este script está no Prefab do lixo
{
    [Header("Animation Settings")]
    [Tooltip("Altura máxima do balanço vertical")]
    public float floatHeight = 0.1f;
    [Tooltip("Duração de um ciclo completo de flutuação (para cima e para baixo)")]
    public float floatDuration = 1.5f;
    [Tooltip("Ease function para a flutuação")]
    public Ease floatEase = Ease.InOutSine;
    [Tooltip("Angulo máximo de rotação (eixo Z)")]
    public float rotateAngle = 5f;
    [Tooltip("Duração de um ciclo completo de rotação (ida e volta)")]
    public float rotateDuration = 2f;
    [Tooltip("Ease function para a rotação")]
    public Ease rotateEase = Ease.InOutSine;

    private Sequence animationSequence; // Sequência para flutuação/rotação
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;


    void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        // Cria e configura a sequência de animação de flutuação/rotação
        SetupAnimationSequence();
    }

    // Configura a sequência de animação (chamado em Awake)
    void SetupAnimationSequence()
    {
         // Garante que qualquer sequência anterior é morta
         if (animationSequence != null && animationSequence.IsPlaying())
         {
              animationSequence.Kill();
         }

        animationSequence = DOTween.Sequence();

        // --- Animação de Flutuação Vertical (Loop Infinito PingPong) ---
        Tween floatTween = transform.DOLocalMoveY(originalLocalPosition.y + floatHeight, floatDuration / 2)
            .SetEase(floatEase)
            .SetLoops(-1, LoopType.Yoyo); // Yoyo faz ir e voltar

        // --- Animação de Rotação (Loop Infinito PingPong) ---
        Tween rotateTween = transform.DOLocalRotate(originalLocalRotation.eulerAngles + new Vector3(0, 0, rotateAngle), rotateDuration / 2)
            .SetEase(rotateEase)
            .SetLoops(-1, LoopType.Yoyo);


        // Adiciona os tweens à sequência para que rodem em paralelo
        animationSequence.Append(floatTween);
        animationSequence.Join(rotateTween);

        // Configurações adicionais da sequência
        animationSequence.SetAutoKill(false); // Não se mata ao terminar um ciclo
        animationSequence.Pause(); // Começa pausada! Precisamos chamar Play() para iniciar.

        // Garante que o objeto começa na posição e rotação original antes de Play()
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }


    // --- Métodos Públicos para Controlar a Animação ---

    // Chamado para iniciar a animação (se estiver parada)
    public void StartMoveAnimation() // Mantendo o nome para consistência
    {
        if (!animationSequence.IsPlaying())
        {
             // Opcional: Reseta para a posição/rotação original antes de tocar (se não estiver já lá)
             transform.localPosition = originalLocalPosition;
             transform.localRotation = originalLocalRotation;
             animationSequence.Play();
             // Debug.Log("Trash Visual Animation: Play");
        }
    }

    // Chamado para parar a animação
    public void StopMoveAnimation() // Mantendo o nome para consistência
    {
         if (animationSequence.IsPlaying())
        {
            animationSequence.Pause();
             // Opcional: Anima suavemente de volta para a posição/rotação original
             transform.DOLocalMove(originalLocalPosition, 0.2f).SetEase(Ease.OutQuad);
             transform.DOLocalRotate(originalLocalRotation.eulerAngles, 0.2f).SetEase(Ease.OutQuad);
            // Debug.Log("Trash Visual Animation: Pause & Return to Original");
        }
        // Se já não estiver tocando, não faz nada.
    }
    // -----------------------------------------------


    void OnDestroy()
    {
        // Garante que a sequência é morta quando o GameObject é destruído
        if (animationSequence != null)
        {
            animationSequence.Kill();
        }
    }

     // Opcional: Chamar StartMoveAnimation() automaticamente quando o objeto é ativado na cena
     // (se você quiser que eles flutuem assim que aparecem)
     void OnEnable()
     {
         StartMoveAnimation();
     }
     // E talvez StopMoveAnimation() no OnDisable() se o objeto for desativado em vez de destruído
     void OnDisable()
     {
         StopMoveAnimation();
     }
}