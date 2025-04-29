using UnityEngine;
using DG.Tweening;

public class PlayerVisualAnimation : MonoBehaviour // Este script agora está no GameObject PAI (PlayerPrefab)
{
    [Header("Animated Visual")]
    [Tooltip("Arraste o GameObject filho que tem o SpriteRenderer e será animado")]
    public Transform visualTransform; // Referência ao GO filho

    [Header("Vertical Bounce")]
    public float bounceHeight = 0.1f; // Altura do balanço vertical
    public float verticalBounceDuration = 0.3f; // Duração de um bounce vertical (ida e volta)
    public Ease verticalBounceEase = Ease.InOutSine;

    [Header("Horizontal Sway")]
    public float swayAmount = 0.05f; // Quantidade do balanço horizontal
    public float horizontalSwayDuration = 0.3f; // Duração de um sway horizontal (ida e volta)
    public Ease horizontalSwayEase = Ease.InOutSine;

    [Header("Rotation Wobble")]
    public float wobbleAngle = 5f; // Ângulo máximo de rotação (graus)
    public float wobbleDuration = 0.3f; // Duração de um wobble (ida e volta)
    public Ease wobbleEase = Ease.InOutSine;


    private Sequence moveAnimationSequence;
    private Vector3 originalLocalPosition; // Posição local original do visualTransform
    private Quaternion originalLocalRotation; // Rotação local original do visualTransform

    void Awake()
    {
         if (visualTransform == null)
         {
             Debug.LogError("PlayerVisualAnimation: visualTransform reference is not set! Cannot animate.");
             enabled = false; // Desabilita o script se a referência essencial faltar
             return;
         }

         // Armazena a posição e rotação local original do GO filho
        originalLocalPosition = visualTransform.localPosition;
        originalLocalRotation = visualTransform.localRotation;

        // --- Cria a sequência de animação ---
        moveAnimationSequence = DOTween.Sequence();

        // Ciclo de movimento completo (ida e volta para as posições originais)

        // 1. Mover para Cima + Lado Direito + Girar para um lado
        moveAnimationSequence.Append(visualTransform.DOLocalMoveY(originalLocalPosition.y + bounceHeight, verticalBounceDuration / 2).SetEase(verticalBounceEase));
        moveAnimationSequence.Join(visualTransform.DOLocalMoveX(originalLocalPosition.x + swayAmount, horizontalSwayDuration / 2).SetEase(horizontalSwayEase));
        moveAnimationSequence.Join(visualTransform.DOLocalRotate(originalLocalRotation.eulerAngles + new Vector3(0, 0, wobbleAngle), wobbleDuration / 2).SetEase(wobbleEase));

        // Vamos assumir que a duração total de um ciclo completo (ida e volta) é a mesma para todos os tipos de animação
        // Se suas durações (verticalBounceDuration, etc.) são diferentes, você precisa ajustar a lógica da sequência.
        // Para simplificar, vamos usar apenas verticalBounceDuration / 2 para cada metade do ciclo.
        // Se precisar de mais controle, pode usar SetDuration() nos tweens individuais.
        float halfCycleDuration = verticalBounceDuration / 2; // Ou a duração que preferir para a metade do ciclo

        moveAnimationSequence = DOTween.Sequence(); // Recria a sequência

         // Parte 1 do Ciclo (ex: Perna para cima, para a direita, girar um pouco)
        moveAnimationSequence.Append(visualTransform.DOLocalMoveY(originalLocalPosition.y + bounceHeight, halfCycleDuration).SetEase(verticalBounceEase));
        moveAnimationSequence.Join(visualTransform.DOLocalMoveX(originalLocalPosition.x + swayAmount, halfCycleDuration).SetEase(horizontalSwayEase));
        moveAnimationSequence.Join(visualTransform.DOLocalRotate(originalLocalRotation.eulerAngles + new Vector3(0, 0, wobbleAngle), halfCycleDuration).SetEase(wobbleEase));

         // Parte 2 do Ciclo (ex: Perna para baixo, para a esquerda, voltar a girar, ou simplesmente voltar para o centro/original)
         // Vamos voltar para a posição/rotação original.
         moveAnimationSequence.Append(visualTransform.DOLocalMoveY(originalLocalPosition.y, halfCycleDuration).SetEase(verticalBounceEase));
         moveAnimationSequence.Join(visualTransform.DOLocalMoveX(originalLocalPosition.x, halfCycleDuration).SetEase(horizontalSwayEase));
         moveAnimationSequence.Join(visualTransform.DOLocalRotate(originalLocalRotation.eulerAngles, halfCycleDuration).SetEase(wobbleEase));


        // Configura para loop infinito e começa pausada
        moveAnimationSequence.SetLoops(-1, LoopType.Restart) // Loop infinito
            .SetAutoKill(false) // Não se mata ao terminar um ciclo
            .Pause(); // Começa pausada

        // Garante que o visualTransform começa na posição e rotação original
        visualTransform.localPosition = originalLocalPosition;
        visualTransform.localRotation = originalLocalRotation;
    }

    // Chamado pelo PlayerMovement2D no hook da SyncVar
    public void StartMoveAnimation()
    {
        if (!moveAnimationSequence.IsPlaying())
        {
            // Reseta para a posição original antes de começar (para evitar transições bruscas)
            visualTransform.localPosition = originalLocalPosition;
            visualTransform.localRotation = originalLocalRotation;
            moveAnimationSequence.Play();
        }
    }

    // Chamado pelo PlayerMovement2D no hook da SyncVar
    public void StopMoveAnimation()
    {
         if (moveAnimationSequence.IsPlaying())
        {
            moveAnimationSequence.Pause();
            // Anima suavemente de volta para a posição e rotação original
             visualTransform.DOLocalMove(originalLocalPosition, 0.2f).SetEase(Ease.OutQuad);
             visualTransform.DOLocalRotate(originalLocalRotation.eulerAngles, 0.2f).SetEase(Ease.OutQuad);
        }
    }

    void OnDestroy()
    {
        // Garante que a sequência é morta para evitar vazamento de memória
        if (moveAnimationSequence != null)
        {
            moveAnimationSequence.Kill();
        }
    }
}