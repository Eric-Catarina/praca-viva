using UnityEngine;
using DG.Tweening;

public class PlayerVisualAnimations : MonoBehaviour
{
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
    private Vector3 originalLocalPosition; // Posição local original do sprite (ou GameObject com o script)
    private Quaternion originalLocalRotation; // Rotação local original

    void Awake()
    {
         // Armazena a posição e rotação local original
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;


        // Cria a sequência de animação combinada e repetitiva
        moveAnimationSequence = DOTween.Sequence();

        // --- Adiciona Animação de Balanço Vertical ---
        // Move para cima e volta para a posição original Y
        moveAnimationSequence.Append(
            transform.DOLocalMoveY(originalLocalPosition.y + bounceHeight, verticalBounceDuration / 2)
                .SetEase(verticalBounceEase)
        );
        moveAnimationSequence.Append(
            transform.DOLocalMoveY(originalLocalPosition.y, verticalBounceDuration / 2)
                .SetEase(verticalBounceEase)
        );


        // --- Adiciona Animação de Balanço Horizontal (Sway) ---
        // Insere os tweens de balanço horizontal em paralelo com os tweens verticais
        // para que aconteçam simultaneamente.
        // A sequência de append/join é um pouco mais complexa quando se mistura
        // append (serial) com join (paralelo).
        // Uma forma é criar tweens separados e adicioná-los à sequência principal.

        // Tween 1 do Sway: Mover para um lado
        Tween swayRight = transform.DOLocalMoveX(originalLocalPosition.x + swayAmount, horizontalSwayDuration / 2).SetEase(horizontalSwayEase);
        // Tween 2 do Sway: Mover de volta para o centro
        Tween swayLeftAndBack = transform.DOLocalMoveX(originalLocalPosition.x - swayAmount, horizontalSwayDuration).SetEase(horizontalSwayEase); // Vai para o outro lado e volta

        // A forma mais simples de adicionar algo em paralelo em uma sequência serial
        // que já está definida é usar Insert. Vamos recriar a sequência de forma mais clara.

        // Vamos construir a sequência para ter um ciclo completo de movimento (vertical + lateral + rotação) e depois repetir esse ciclo.
        moveAnimationSequence = DOTween.Sequence();

        // Ciclo de movimento completo (ida e volta)

        // 1. Mover para Cima + Lado Direito + Girar para um lado
        moveAnimationSequence.Append(transform.DOLocalMoveY(originalLocalPosition.y + bounceHeight, verticalBounceDuration / 2).SetEase(verticalBounceEase));
        moveAnimationSequence.Join(transform.DOLocalMoveX(originalLocalPosition.x + swayAmount, horizontalSwayDuration / 2).SetEase(horizontalSwayEase));
        moveAnimationSequence.Join(transform.DOLocalRotate(originalLocalRotation.eulerAngles + new Vector3(0, 0, wobbleAngle), wobbleDuration / 2).SetEase(wobbleEase));


        // 2. Mover para Baixo + Lado Esquerdo + Girar para o outro lado
        moveAnimationSequence.Append(transform.DOLocalMoveY(originalLocalPosition.y, verticalBounceDuration / 2).SetEase(verticalBounceEase)); // Volta para posição original Y
        moveAnimationSequence.Join(transform.DOLocalMoveX(originalLocalPosition.x - swayAmount, horizontalSwayDuration / 2).SetEase(horizontalSwayEase));
        moveAnimationSequence.Join(transform.DOLocalRotate(originalLocalRotation.eulerAngles - new Vector3(0, 0, wobbleAngle), wobbleDuration / 2).SetEase(wobbleEase));

        // 3. Voltar para o centro X e Rotação Original (Pode ser feito junto com o passo 2 ou separado)
        // Vamos ajustar o passo 2 para ir para baixo E voltar para o centro X/Rotação
         moveAnimationSequence = DOTween.Sequence();

        // Ciclo de movimento completo (ida e volta)

        // 1. Mover para Cima + Lado Direito + Girar para um lado
        moveAnimationSequence.Append(transform.DOLocalMoveY(originalLocalPosition.y + bounceHeight, verticalBounceDuration / 2).SetEase(verticalBounceEase));
        moveAnimationSequence.Join(transform.DOLocalMoveX(originalLocalPosition.x + swayAmount, horizontalSwayDuration / 2).SetEase(horizontalSwayEase));
        moveAnimationSequence.Join(transform.DOLocalRotate(originalLocalRotation.eulerAngles + new Vector3(0, 0, wobbleAngle), wobbleDuration / 2).SetEase(wobbleEase));

        // 2. Mover para Baixo E para o Centro (Y, X, Rotação)
        // A duração total de um ciclo é bounceDuration ou swayDuration ou wobbleDuration, se forem iguais
        // Vamos supor que todas as durações *de ida e volta* são as mesmas para simplificar o loop
        float fullCycleDuration = verticalBounceDuration; // Ou swayDuration, ou wobbleDuration, se forem iguais

        moveAnimationSequence.Append(transform.DOLocalMoveY(originalLocalPosition.y, fullCycleDuration / 2).SetEase(verticalBounceEase));
        moveAnimationSequence.Join(transform.DOLocalMoveX(originalLocalPosition.x, fullCycleDuration / 2).SetEase(horizontalSwayEase)); // Volta para posição original X
        moveAnimationSequence.Join(transform.DOLocalRotate(originalLocalRotation.eulerAngles, fullCycleDuration / 2).SetEase(wobbleEase)); // Volta para rotação original Z

        // Configura para loop infinito e começa pausada
        moveAnimationSequence.SetLoops(-1, LoopType.Restart)
            .SetAutoKill(false)
            .Pause();

        // Garante que a posição local e rotação inicial são as armazenadas
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }

    // Chame este método do seu script de movimento (PlayerMovement2D)
    // quando o jogador começar a se mover
    public void StartMoveAnimation()
    {
        if (!moveAnimationSequence.IsPlaying())
        {
            moveAnimationSequence.Play();
        }
    }

    // Chame este método do seu script de movimento (PlayerMovement2D)
    // quando o jogador parar
    public void StopMoveAnimation()
    {
         if (moveAnimationSequence.IsPlaying())
        {
            moveAnimationSequence.Pause();
            // Opcional: Retornar o sprite à posição e rotação original suavemente
             transform.DOLocalMove(originalLocalPosition, 0.1f).SetEase(Ease.OutQuad);
             transform.DOLocalRotate(originalLocalRotation.eulerAngles, 0.1f).SetEase(Ease.OutQuad);
        }
    }

    void OnDestroy()
    {
        // Garante que a sequência é morta para evitar vazamento de memória
        moveAnimationSequence.Kill();
    }
}