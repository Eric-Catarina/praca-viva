using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using Random = UnityEngine.Random; // Para evitar conflito com System.Random

// Adicione o using para o arquivo da enumeração se ela estiver em um namespace
// using SeuProjeto.Enums;


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
            // Log de aviso se encontrar outra instância no cliente que não seja a que deve ser controlada
             if (isClientOnly) // Mostra o aviso apenas em clientes remotos
             {
                 Debug.LogWarning("GameManager: Another instance found on client. Using the one from the server.");
             } else if (isServer && isClient) {
                 // Este é o Host. Pode ter duas instâncias no Inspector, mas apenas uma NetworkBehaviour ativa.
                 // A instância 'this' que se torna o Singleton deve ser a NetworkBehaviour.
             }
             else // Se for uma instância local não de rede no Host/Editor antes de iniciar
             {
                  // Debug.Log("GameManager: Local non-networked instance found."); // Pode acontecer no Editor antes de StartHost
             }
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
    [SyncVar(hook = nameof(OnTimeChanged))] // Sincroniza o tempo restante
    public float remainingTime = 60f; // Tempo inicial (será sobrescrito pela dificuldade)

    [SyncVar(hook = nameof(OnTotalTrashChanged))] // Total de lixo na partida
    public int totalTrashCount = 0; // Definido no servidor

    [SyncVar(hook = nameof(OnCollectedTrashChanged))] // Contagem de lixo coletado
    public int collectedTrashCount = 0; // Gerenciado no servidor

    [Header("Difficulty Settings")]
    public List<float> difficultyTimes = new List<float> { 120f, 90f, 60f };
    public List<int> difficultyTrashCounts = new List<int> { 10, 20, 30 };
    public List<List<TrashType>> difficultyTrashTypes = new List<List<TrashType>>
    {
        new List<TrashType> { TrashType.Paper }, // Fácil: Só Papel
        new List<TrashType> { TrashType.Paper, TrashType.Plastic }, // Médio: Papel e Plástico
        new List<TrashType> { TrashType.Paper, TrashType.Plastic, TrashType.Organic } // Difícil: Papel, Plástico e Orgânico
    };

     [Header("Trash Spawning")]
    [Tooltip("O ÚNICO Prefab do item de lixo (deve ter TrashItem script e SpriteRenderer)")]
    public GameObject trashItemPrefab; // Agora é um único prefab

     [Header("Random Spawn Area")]
    [Tooltip("Limite MÍNIMO X para o spawn aleatório")]
    public float spawnAreaMinX = -10f;
    [Tooltip("Limite MÁXIMO X para o spawn aleatório")]
    public float spawnAreaMaxX = 10f;
    [Tooltip("Limite MÍNIMO Y para o spawn aleatório")]
    public float spawnAreaMinY = -3f;
    [Tooltip("Limite MÁXIMO Y para o spawn aleatório")]
    public float spawnAreaMaxY = 3f;

     // --- Para mapear tipo de lixo para cor (Usado no servidor ANTES de spawnar E Cliente no hook) ---
     [Header("Trash Type Colors")]
     public List<Color> trashTypeColors = new List<Color>(); // Mapeia TrashType (exceto None) para cor. Configurar no Inspector.

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

        // Espera um pouco para garantir que os clientes tenham chance de conectar
        StartCoroutine(StartGameWithDelay(1.0f));
    }

     IEnumerator StartGameWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
         if (!isServer) yield break;

         Debug.Log($"Starting Game - Difficulty: {currentDifficultyIndex}");

        // Configurações baseadas na dificuldade selecionada
        if (currentDifficultyIndex >= 0 && currentDifficultyIndex < difficultyTimes.Count &&
            currentDifficultyIndex < difficultyTrashCounts.Count &&
            currentDifficultyIndex < difficultyTrashTypes.Count)
        {
            remainingTime = difficultyTimes[currentDifficultyIndex];
            totalTrashCount = difficultyTrashCounts[currentDifficultyIndex];
            List<TrashType> typesToSpawn = difficultyTrashTypes[currentDifficultyIndex];

            // --- Spawning do Lixo ---
            SpawnTrashItems(totalTrashCount, typesToSpawn,
                            spawnAreaMinX, spawnAreaMaxX,
                            spawnAreaMinY, spawnAreaMaxY);

            collectedTrashCount = 0;
            gameStarted = true;
            gameEnded = false;

            RpcStartGame();
        }
         else
        {
             Debug.LogError($"Invalid difficulty index ({currentDifficultyIndex}) or difficulty settings incomplete!");
             // Talvez parar o host
        }
    }


    // --- Lógica de Spawning (Servidor) ---
    void SpawnTrashItems(int count, List<TrashType> types,
                         float minX, float maxX, float minY, float maxY)
    {
        if (trashItemPrefab == null)
        {
             Debug.LogError("TrashItemPrefab is not assigned in GameManager!");
             return;
        }
         // Verificação para a lista de cores (agora usada em OnStartClient do TrashItem também)
         if (trashTypeColors.Count < System.Enum.GetValues(typeof(TrashType)).Length -1) // -1 for None
        {
             Debug.LogWarning("TrashTypeColors list may not be fully assigned in GameManager. Trash colors may be incorrect.");
        }


        for (int i = 0; i < count; i++)
        {
            TrashType randomType = types[Random.Range(0, types.Count)];
            Vector3 spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);

            GameObject trashGO = Instantiate(trashItemPrefab, spawnPos, Quaternion.identity);
            TrashItem trashItem = trashGO.GetComponent<TrashItem>();
            // SpriteRenderer trashRenderer = trashGO.GetComponent<SpriteRenderer>(); // Não precisamos mais definir a cor aqui no servidor

             if (trashItem != null)
             {
                  // Define o tipo do item ANTES de spawnar na rede
                 trashItem.type = randomType;

                 // A COR DO SPRITE SERÁ DEFINIDA NO CLIENTE EM OnStartClient DO TRASHITEM
                 // LENDO A SyncVar 'type' E USANDO UMA LISTA DE CORES ACESSÍVEL NO CLIENTE
                 // (Pode ser uma cópia da lista aqui ou uma lista separada no TrashItem)

             } else {
                 Debug.LogError($"Spawned object {trashItemPrefab.name} is missing TrashItem component!");
             }

            NetworkServer.Spawn(trashGO); // Spawna o objeto na rede
            // Debug.Log($"Spawned {randomType} at {spawnPos}");
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
        // <<< AQUI VOCÊ MOSTRA A TELA DE VITÓRIA/DERROTA NA UI DO CLIENTE >>>
        // Encontre e ative/configure um painel de resultado na UI
        // Ex: FindObjectOfType<GameUI>()?.ShowResultScreen(win);
    }

     /*
     IEnumerator StopHostAfterDelay(float delay)
     {
         yield return new WaitForSecondsRealtime(delay);
         if (isServer)
         {
             NetworkManager.singleton.StopHost();
         }
     }
     */

    // --- Métodos Hook de SyncVar (Chamados em TODOS os clientes e no Host) ---
    void OnTimeChanged(float oldTime, float newTime)
    {
        // Debug.Log($"Time updated: {newTime}");
        // <<< ATUALIZE A UI DO TIMER AQUI (no cliente) >>>
         // Ex: FindObjectOfType<GameUI>()?.UpdateTimerUI(newTime);
    }

    void OnTotalTrashChanged(int oldTotal, int newTotal)
    {
         Debug.Log($"Total Trash updated: {newTotal}");
         // <<< ATUALIZE A UI DO TOTAL DE LIXO AQUI (no cliente) >>>
         // Ex: FindObjectOfType<GameUI>()?.UpdateTotalTrashUI(newTotal);
    }

    void OnCollectedTrashChanged(int oldCollected, int newCollected)
    {
         Debug.Log($"Collected Trash updated: {newCollected}");
         // <<< ATUALIZE A UI DA CONTAGEM COLETADA AQUI (no cliente) >>>
         // Ex: FindObjectOfType<GameUI>()?.UpdateCollectedTrashUI(newCollected);

         // Opcional: Tocar som ou mostrar efeito quando um lixo é coletado (no cliente)
         // SoundManager.Instance?.PlayTrashCollectedSound();
    }


     // --- Método para definir a dificuldade ANTES de StartHost ---
     // Você chamaria isso do script da UI que tem os botões de dificuldade no lobby
     public void SetDifficultyIndex(int index)
     {
         if (!gameStarted && !gameEnded) // Só permite mudar a dificuldade antes do jogo começar
         {
             currentDifficultyIndex = index;
             Debug.Log($"Difficulty set to index: {index} (Applies on next game start)");
             // Opcional: Atualizar UI de dificuldade selecionada
         } else {
             Debug.LogWarning("Cannot change difficulty after game started.");
         }
     }


     // --- Para mapear tipo de lixo para cor (Cliente) ---
     // Este método é chamado pelo TrashItem.OnStartClient para obter a cor
     // correspondente ao seu tipo sincronizado.
     public Color GetColorForTrashType(TrashType type)
     {
         int index = (int)type -1; // Ajusta índice se None for 0
          if (index >= 0 && index < trashTypeColors.Count)
         {
             return trashTypeColors[index];
         }
         Debug.LogWarning($"Color not found for trash type: {type}. Index {index}. Using white.");
         return Color.white; // Cor padrão se não encontrar
     }

      // --- Para mapear tipo de lixo para Sprite (Cliente) ---
     // Se você tiver sprites diferentes em vez de apenas cores
     /*
     [Header("Client Sprites")]
     public List<Sprite> trashSprites = new List<Sprite>(); // Arraste os sprites aqui na ordem do enum (exceto None)

     public Sprite GetSpriteForTrashType(TrashType type)
     {
         int index = (int)type -1;
         if (index >= 0 && index < trashSprites.Count)
         {
             return trashSprites[index];
         }
         Debug.LogWarning($"Sprite not found for trash type: {type}. Index {index}");
         return null;
     }
     */
}