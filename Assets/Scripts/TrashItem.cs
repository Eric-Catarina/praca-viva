using UnityEngine;
using Mirror;

public class TrashItem : NetworkBehaviour
{
    // Usa a enum que você criou
    [SyncVar] // O tipo de lixo deste item, sincronizado pelo servidor
    public TrashType type = TrashType.Paper; // Tipo padrão

    // [SyncVar(hook = nameof(OnCollectedStatusChanged))] // Opcional: se precisar mudar visualmente após coletar
    // public bool isCollected = false; // Não precisamos sincronizar isso se vamos destruir o objeto

    // Referência ao Sprite Renderer para mudar o sprite/cor (se aplicável)
    private SpriteRenderer spriteRenderer;

    // Método chamado no servidor ANTES de Spawnar
    public void SetType(TrashType newType, Sprite sprite)
    {
        type = newType;
        // No servidor, se tiver spriteRenderer, pode definir aqui
        // if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        // if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;

        // Alternativa: Definir o sprite/cor no hook ou OnStartClient,
        // lendo o 'type' sincronizado.
    }

    void Awake()
    {
         spriteRenderer = GetComponent<SpriteRenderer>();
    }


     // Hook opcional para mudar o visual quando coletado (se não destruir)
     // void OnCollectedStatusChanged(bool oldStatus, bool newStatus)
     // {
     //     if (newStatus) {
     //         // Ex: Esconder o sprite ou desativar o GameObject localmente
     //         if (spriteRenderer != null) spriteRenderer.enabled = false;
     //         // Ou gameObject.SetActive(false); // Isso desativaria para TODOS
     //     }
     // }

    // Método chamado em todos os clientes depois que o objeto é spawnado
    // e SyncVars foram inicializadas
    public override void OnStartClient()
    {
        base.OnStartClient();
         // Atualiza o sprite/cor baseado no tipo sincronizado
         // Isso requer ter Sprites diferentes configurados em algum lugar
         // (ex: em um GameManager ou um array no próprio TrashItem)
         UpdateSpriteBasedOnType();
    }

    // Método para atualizar o visual (você precisará gerenciar os Sprites)
    void UpdateSpriteBasedOnType()
    {
        if (spriteRenderer == null) return;

        // <<< VOCÊ PRECISA DE UMA FORMA DE MAPEAR TrashType PARA Sprite AQUI >>>
        // Exemplo SIMPLES (não ideal, mas funciona):
         if (type == TrashType.Paper) spriteRenderer.color = Color.yellow;
         else if (type == TrashType.Plastic) spriteRenderer.color = Color.blue;
         else if (type == TrashType.Organic) spriteRenderer.color = Color.green;
         else spriteRenderer.color = Color.white; // Padrão ou erro

         // Exemplo MELHOR: Usar uma lista de Sprites configurada em outro lugar
         // spriteRenderer.sprite = GameManager.Instance?.GetSpriteForTrashType(type); // Assumindo um GameManager Singleton
        // ----------------------------------------------------------------------
    }

}