using UnityEngine;
using Mirror;
using DG.Tweening;
using System.Collections.Generic; // Para List

// A enum TrashType NÃO é mais necessária neste script.


public class TrashItem : NetworkBehaviour
{
    // REMOVIDO: [SyncVar] TrashType type;

    // --- Sincroniza o índice do sprite escolhido no servidor ---
    [SyncVar(hook = nameof(OnSpriteIndexChanged))]
    public int spriteIndex = -1; // Índice na lista de sprites local do Prefab.
    // -----------------------------------------------------------------

    [Header("Visuals")]
    [Tooltip("Lista de Sprites para usar no TrashItem (Configurar no Prefab)")]
    public List<Sprite> trashSpritesLocal; // <<< LISTA NO PRÓPRIO PREFAB

    // Referência ao Sprite Renderer
    private SpriteRenderer spriteRenderer;
    // Referência ao Collider 2D
    private Collider2D itemCollider;

    private TrashVisualAnimation visualAnimation;

    [Header("Collection Animation")]
    public float collectionDuration = 0.3f;
    public Ease collectionEase = Ease.InBack;


    // Dentro de TrashItem.cs, no Awake()
    void Awake()
    {
        // Se o SpriteRenderer está no GameObject raiz:
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Se o SpriteRenderer está em um GameObject FILHO chamado "Visual":
        // Transform visualChild = transform.Find("Visual"); // Encontra o filho pelo nome
        // if (visualChild != null) {
        //     spriteRenderer = visualChild.GetComponent<SpriteRenderer>();
        // } else {
        //     Debug.LogError("TrashItem: Visual child with SpriteRenderer not found!");
        // }

        // Ou se é o primeiro/único SpriteRenderer em qualquer filho:
        // spriteRenderer = GetComponentInChildren<SpriteRenderer>(); // Procura em filhos também

        itemCollider = GetComponent<Collider2D>();
        visualAnimation = GetComponent<TrashVisualAnimation>(); // Se visualAnimation está no raiz
        // Ou visualAnimation = GetComponentInChildren<TrashVisualAnimation>(); // Se visualAnimation está no filho

        // Adicionar uma verificação
        if (spriteRenderer == null) Debug.LogError("TrashItem: SpriteRenderer not found in Awake!");
        if (itemCollider == null) Debug.LogError("TrashItem: Collider2D not found in Awake!");
        if (visualAnimation == null) Debug.LogWarning("TrashItem: TrashVisualAnimation not found in Awake!"); // Warning, não Error, pois pode só não ter animação
    }

    // Chamado em todos os clientes depois que o objeto é spawnado e SyncVars foram inicializadas
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"TrashItem spawned on client. NetId: {netId}, SyncVar spriteIndex: {spriteIndex}");

        // Define o sprite inicial baseado no valor sincronizado assim que o objeto aparece no cliente
        // O hook OnSpriteIndexChanged também chama isso. Chamamos aqui para garantir que o visual é definido
        // mesmo que o valor inicial da SyncVar seja o padrão (-1) e mude para um índice real logo em seguida.
         UpdateSpriteBasedOnIndex(spriteIndex); // Chama o método de atualização visual
    }


    // --- Hook da SyncVar `spriteIndex` ---
    // Chamado em todos os clientes quando spriteIndex muda no servidor.
    void OnSpriteIndexChanged(int oldIndex, int newIndex)
    {
        Debug.Log($"TrashItem {netId}: spriteIndex changed from {oldIndex} to {newIndex}. Updating sprite.");
        UpdateSpriteBasedOnIndex(newIndex);
    }

    // --- Método para atualizar o Sprite ---
    // Obtém o sprite da lista LOCAL `trashSpritesLocal` no Prefab usando o índice sincronizado.
    void UpdateSpriteBasedOnIndex(int index)
    {
        if (spriteRenderer == null)
        {
             Debug.LogWarning($"TrashItem {netId}: SpriteRenderer not found to update sprite.");
             return;
        }

        if (trashSpritesLocal != null && index >= 0 && index < trashSpritesLocal.Count)
        {
             spriteRenderer.sprite = trashSpritesLocal[index];
             // Debug.Log($"TrashItem {netId}: Sprite updated to {spriteRenderer.sprite.name} using local list.");
        } else {
            Debug.LogWarning($"TrashItem {netId}: Invalid sprite index {index} for local list size {trashSpritesLocal?.Count ?? 0}. Cannot update sprite.");
            spriteRenderer.sprite = null; // Ou um sprite de erro padrão
        }
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
        // Se o objeto for destruído antes do fim da animação, o DOTween cuidará disso.
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

        // O objeto será destruído pelo servidor logo após este RPC terminar em todos os clientes.
    }
}