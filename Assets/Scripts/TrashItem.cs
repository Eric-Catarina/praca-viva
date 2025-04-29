using UnityEngine;
using Mirror;
using DG.Tweening;

// A enum TrashType NÃO é mais necessária neste script.

public class TrashItem : NetworkBehaviour
{
    // [SyncVar] TrashType type; // REMOVIDO - Lixo não tem mais tipo sincronizado

    // Referência ao Sprite Renderer
    private SpriteRenderer spriteRenderer;
    // Referência ao Collider 2D
    private Collider2D itemCollider;

    // Referência ao script de animação visual (flutuação/rotação)
    private TrashVisualAnimation visualAnimation; // Ainda útil para a animação de flutuação

    [Header("Collection Animation")]
    public float collectionDuration = 0.3f;
    public Ease collectionEase = Ease.InBack;


    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        itemCollider = GetComponent<Collider2D>();
        visualAnimation = GetComponent<TrashVisualAnimation>();
        if (visualAnimation == null) Debug.LogWarning("TrashVisualAnimation script not found on TrashItem prefab!");
    }

    // Chamado em todos os clientes depois que o objeto é spawnado e SyncVars foram inicializadas
    // Se o GameManager define o sprite no servidor ANTES de spawnar,
    // essa configuração visual já deve sincronizar com o NetworkIdentity.
    // Este método pode ser usado para verificar ou adicionar efeitos locais pós-spawn.
    public override void OnStartClient()
    {
        base.OnStartClient();
        // Debug.Log($"TrashItem spawned on client. NetId: {netId}");

        // Não precisa mais de UpdateVisualBasedOnType() lendo SyncVar type
    }


    // --- ClientRpc para Disparar a Animação de Coleta ---
    [ClientRpc]
    public void RpcAnimateCollection()
    {
        // Este RPC é chamado no cliente POUCO ANTES do objeto ser destruído pelo servidor.
        Debug.Log($"Client: Animating collection for trash item (NetId: {netId}).");

        // --- Desabilita Colisão e Pede para Parar a Animação de Flutuação ---
        if (itemCollider != null) itemCollider.enabled = false;
        if (visualAnimation != null)
        {
             visualAnimation.StopMoveAnimation(); // Para a animação de flutuação/rotação
        } else {
             // Fallback: Esconder sprite se não tiver visualAnimation
             if (spriteRenderer != null) spriteRenderer.enabled = false;
        }


        // --- Animação de Coleta (Escalar e Fade Out) ---
        // Criamos e tocamos a sequência de animação localmente.
        Sequence collectionSequence = DOTween.Sequence();

        // Animação de Escala para zero
        collectionSequence.Append(transform.DOScale(Vector3.zero, collectionDuration).SetEase(collectionEase));

        // Animação de Fade Out do Sprite
        if (spriteRenderer != null)
        {
             Color startColor = spriteRenderer.color;
             Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
             collectionSequence.Join(spriteRenderer.DOColor(endColor, collectionDuration).SetEase(Ease.Linear));
        }

        collectionSequence.Play();

        // Não precisamos de OnComplete, o objeto será destruído pelo servidor.
        // A sequência de animação será morta no OnDestroy deste objeto pelo DOTween automaticamente,
        // ou explicitamente pelo OnDestroy do TrashVisualAnimation se ele matar todas as sequências.
    }
}