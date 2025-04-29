using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using Random = UnityEngine.Random; // Para evitar conflito com System.Random

// A enum TrashType NÃO é mais necessária neste script.

public class GameManager : NetworkBehaviour
{
    // --- Singleton ---
    public static GameManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
             if (isClientOnly) { Debug.LogWarning("GameManager: Another instance found on client. Using the one from the server."); }
        }
    }

     void OnDestroy()
    {
         if (Instance == this)
         {
             Instance = null;
         }
    }
    // -------------------------------------------------------


    [Header("Game Settings")]
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float remainingTime = 60f;

    [SyncVar(hook = nameof(OnTotalTrashChanged))]
    public int totalTrashCount = 0;

    [SyncVar(hook = nameof(OnCollectedTrashChanged))]
    public int collectedTrashCount = 0;

    [Header("Difficulty Settings")]
    public List<float> difficultyTimes = new List<float> { 120f, 90f, 60f };
    public List<int> difficultyTrashCounts = new List<int> { 10, 20, 30 };
    // REMOVIDO: public List<List<TrashType>> difficultyTrashTypes;

     [Header("Trash Spawning")]
    [Tooltip("O Prefab do item de lixo (deve ter TrashItem script e SpriteRenderer)")]
    public GameObject trashItemPrefab; // Um único prefab agora

     [Tooltip("Lista de Sprites para usar aleatoriamente no lixo spawnado")]
     public List<Sprite> trashSprites; // <<< NOVA LISTA DE SPRITES ALEATÓRIOS

     [Header("Random Spawn Area")]
    public float spawnAreaMinX = -10f;
    public float spawnAreaMaxX = 10f;
    public float spawnAreaMinY = -3f;
    public float spawnAreaMaxY = 3f;

     // REMOVIDO: [Header("Trash Type Colors")] public List<Color> trashTypeColors;

     // --- Game State (Servidor) ---
    private bool gameStarted = false;
    private bool gameEnded = false;
    private int currentDifficultyIndex = 0; // Definido antes de StartHost

    // --- Sincroniza o início da partida ---
    [ClientRpc]
    void RpcStartGame()
    {
         Debug.Log("Game Started RPC received on client.");
         // Ativar UI do jogo, esconder lobby, etc.
         // FindObjectOfType<GameUI>()?.ShowGameUI();
    }

    // --- Chamado no SERVIDOR quando o Host inicia ---
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("GameManager Server Started.");

        StartCoroutine(StartGameWithDelay(1.0f));
    }

     IEnumerator StartGameWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
         if (!isServer) yield break;

         Debug.Log($"Starting Game - Difficulty: {currentDifficultyIndex}");

        if (currentDifficultyIndex >= 0 && currentDifficultyIndex < difficultyTimes.Count &&
            currentDifficultyIndex < difficultyTrashCounts.Count) // Verificação simplificada
        {
            remainingTime = difficultyTimes[currentDifficultyIndex];
            totalTrashCount = difficultyTrashCounts[currentDifficultyIndex];
            // REMOVIDO: List<TrashType> typesToSpawn; // Não precisa mais de tipos específicos

            // --- Spawning do Lixo ---
            SpawnTrashItems(totalTrashCount,
                            spawnAreaMinX, spawnAreaMaxX,
                            spawnAreaMinY, spawnAreaMaxY); // Não passa mais lista de tipos

            collectedTrashCount = 0;
            gameStarted = true;
            gameEnded = false;

            RpcStartGame();
        }
         else
        {
             Debug.LogError($"Invalid difficulty index ({currentDifficultyIndex}) or difficulty settings incomplete!");
        }
    }


    // --- Lógica de Spawning (Servidor) ---
    // Não recebe mais lista de tipos
    void SpawnTrashItems(int count,
                         float minX, float maxX, float minY, float maxY)
    {
        if (trashItemPrefab == null)
        {
             Debug.LogError("TrashItemPrefab is not assigned in GameManager!");
             return;
        }
        // Verificação para a lista de sprites
         if (trashSprites == null || trashSprites.Count == 0)
        {
             Debug.LogError("Trash Sprites list is empty or null in GameManager!");
             return;
        }
         if (count > (maxX - minX) * (maxY - minY)) // Verificação simples se a área é grande o suficiente para a quantidade
         {
             Debug.LogWarning("Spawn area may be too small for the number of trash items.");
         }


        for (int i = 0; i < count; i++)
        {
            // REMOVIDO: TrashType randomType; // Não precisa mais de tipo aleatório

            // --- Gera Posição Aleatória ---
            Vector3 spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);
            // -----------------------------

            GameObject trashGO = Instantiate(trashItemPrefab, spawnPos, Quaternion.identity);
            TrashItem trashItem = trashGO.GetComponent<TrashItem>();
            SpriteRenderer trashRenderer = trashGO.GetComponent<SpriteRenderer>();

             if (trashItem != null)
             {
                  // REMOVIDO: trashItem.type = randomType; // Não precisa definir tipo
             } else {
                 Debug.LogError($"Spawned object {trashItemPrefab.name} is missing TrashItem component!");
             }

             // --- Define o Sprite Aleatório no Servidor ANTES de Spawnar ---
             if (trashRenderer != null)
             {
                 int randomSpriteIndex = Random.Range(0, trashSprites.Count);
                 trashRenderer.sprite = trashSprites[randomSpriteIndex];
                 // Debug.Log($"Server: Assigned sprite {trashRenderer.sprite.name} to new trash item.");
             } else {
                  Debug.LogWarning("TrashItem prefab is missing SpriteRenderer component!");
             }
             // ------------------------------------------------------------


            NetworkServer.Spawn(trashGO); // Spawna o objeto na rede
            // Debug.Log($"Spawned trash at {spawnPos}");
        }
    }


    // --- Game Loop (Servidor) ---
    void Update()
    {
        if (!isServer || gameEnded || !gameStarted)
        {
            return;
        }

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            EndGame(false); // Derrota
        }
        else if (collectedTrashCount >= totalTrashCount && totalTrashCount > 0)
        {
            EndGame(true); // Vitória
        }
    }

    // --- Métodos Chamados por Outros Scripts (Servidor) ---

    [Server]
    public void IncrementCollectedCount()
    {
        collectedTrashCount++;
        Debug.Log($"Server: Trash Collected! Count: {collectedTrashCount}/{totalTrashCount}");

         if (collectedTrashCount >= totalTrashCount && totalTrashCount > 0 && !gameEnded)
         {
             EndGame(true);
         }
    }

    // --- Fim do Jogo (Servidor) ---
    [Server]
    void EndGame(bool win)
    {
        if (gameEnded) return;
        gameEnded = true;
        gameStarted = false;

        Debug.Log($"Game Ended. Result: {(win ? "Victory" : "Derrota")}");
        RpcEndGame(win);

        // Opcional: Parar o host/servidor após um delay
        // StartCoroutine(StopHostAfterDelay(5.0f));
    }

    [ClientRpc]
    void RpcEndGame(bool win)
    {
        Debug.Log($"Game Ended RPC received on client. Result: {(win ? "Victory" : "Derrota")}");
        // Mostrar tela de resultado na UI do cliente
        // Ex: FindObjectOfType<GameUI>()?.ShowResultScreen(win);
    }


    // --- Métodos Hook de SyncVar (Chamados em TODOS os clientes e no Host) ---
    void OnTimeChanged(float oldTime, float newTime)
    {
        // Atualizar UI do Timer no cliente
        // Ex: FindObjectOfType<GameUI>()?.UpdateTimerUI(newTime);
    }

    void OnTotalTrashChanged(int oldTotal, int newTotal)
    {
        // Atualizar UI do Total de Lixo no cliente
         Debug.Log($"Total Trash updated: {newTotal}"); // Log para depuração
        // Ex: FindObjectOfType<GameUI>()?.UpdateTotalTrashUI(newTotal);
    }

    void OnCollectedTrashChanged(int oldCollected, int newCollected)
    {
        // Atualizar UI da Contagem Coletada no cliente
         Debug.Log($"Collected Trash updated: {newCollected}"); // Log para depuração
        // Ex: FindObjectOfType<GameUI>()?.UpdateCollectedTrashUI(newCollected);

        // Opcional: Tocar som ou mostrar efeito quando um lixo é coletado (no cliente)
        // SoundManager.Instance?.PlayTrashCollectedSound();
    }


     // --- Método para definir a dificuldade ANTES de StartHost ---
     public void SetDifficultyIndex(int index)
     {
         if (!gameStarted && !gameEnded)
         {
             currentDifficultyIndex = index;
             Debug.Log($"Difficulty set to index: {index} (Applies on next game start)");
             // Opcional: Atualizar UI de dificuldade selecionada no lobby
         } else { Debug.LogWarning("Cannot change difficulty after game started."); }
     }

     // REMOVIDO: GetColorForTrashType pois não usamos mais cores por tipo de lixo
}