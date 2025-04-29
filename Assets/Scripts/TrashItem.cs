using UnityEngine;
using Mirror;
using DG.Tweening;
using System.Collections.Generic;

// A enumeração TrashType DEVE estar definida em outro arquivo (ex: Utilities.cs)
// using SeuProjeto.Enums;

public class TrashItem : NetworkBehaviour
{
    [SyncVar]
    public TrashType type = TrashType.None;

    private SpriteRenderer spriteRenderer;
    private Collider2D itemCollider;
    private TrashVisualAnimation visualAnimation;

    [Header("Collection Animation")]
    public float collectionDuration = 0.3f;
    public Ease collectionEase = Ease.InBack;

    // ... (outras variáveis e Awake/OnStartClient/UpdateVisualBasedOnType) ...


    [ClientRpc]
    public void RpcAnimateCollection()
    {
        Debug.Log($"Client: Animating collection for {type} trash item (NetId: {netId}).");

        if (itemCollider != null) itemCollider.enabled = false;

        if (visualAnimation != null)
        {
             visualAnimation.StopMoveAnimation(); // Para a animação de flutuação/rotação
        } else {
             // Fallback: Esconder sprite e desabilitar colisor se não tiver visualAnimation
             if (spriteRenderer != null) spriteRenderer.enabled = false;
             // Colisor já desabilitado
        }


        // --- Animação de Coleta (Escalar e Fade Out) ---
        Sequence collectionSequence = DOTween.Sequence();

        // Animação de Escala
        collectionSequence.Append(transform.DOScale(Vector3.zero, collectionDuration).SetEase(collectionEase));

        // Animação de Fade Out
        if (spriteRenderer != null)
        {
             Color startColor = spriteRenderer.color;
             Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
             collectionSequence.Join(spriteRenderer.DOColor(endColor, collectionDuration).SetEase(Ease.Linear));
        }

        collectionSequence.Play();

        // --- NOVO: MATAR TWEENS ASSOCIADOS A ESTE OBJETO APÓS A ANIMAÇÃO DE COLETA ---
        // Matar todos os tweens que afetam este Transform ou este objeto.
        // Usamos OnComplete para matar *depois* que a animação de coleta termina,
        // mas pode haver um pequeno risco se a destruição ocorrer ANTES do OnComplete.
        // A forma mais segura é MATAR *antes* de iniciar a nova animação de coleta
        // ou garantir que a nova animação sobreescreva/substitua as antigas.

        // A melhor abordagem é garantir que a ANIMAÇÃO DE FLUTUAÇÃO/ROTAÇÃO (que roda em loop infinito)
        // seja morta explicitamente quando a animação de coleta é disparada.
        // Já estamos chamando visualAnimation.StopMoveAnimation(); isso DEVERIA matar a sequência.
        // Se o erro persiste, talvez o tween de *coleta* em si (collectionSequence) esteja causando o problema
        // se ele tentar continuar animando no frame em que o objeto é destruído.

        // Uma forma mais agressiva: Mate TODOS os tweens associados a este GameObject *antes* de começar a animação de coleta.
        // DOTween.Kill(this.transform, true); // true = include children
        // DOTween.Kill(this.gameObject, true); // Também mata tweens no GameObject principal e filhos

        // >>> Vamos tentar matar a sequência de coleta APÓS o play, se ela causar o problema <<<
        // Isso é menos provável, mas se o erro apontar para o tween de coleta, pode ser necessário.
        collectionSequence.OnComplete(() => {
            // Isso pode ser arriscado se o objeto for destruído antes do OnComplete rodar
            // DOTween.Kill(collectionSequence);
        });
        // -----------------------------------------------------------------------------

        // A chamada visualAnimation.StopMoveAnimation() é o lugar mais provável para matar a animação em LOOP.
        // Se o erro ainda ocorrer, o problema está no timing entre NetworkServer.Destroy e a execução local do Rpc e Tweens.

        // O OBJETO SERÁ DESTRUÍDO PELO SERVIDOR LOGO APÓS ESTE RPC TERMINAR EM TODOS OS CLIENTES.
    }
    // ... (Resto do script) ...
}