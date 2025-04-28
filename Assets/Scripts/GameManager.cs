using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using Random = UnityEngine.Random; // Para evitar conflito com System.Random

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
            // (a NetworkBehaviour spawnada pelo servidor)
             if (!GetComponent<NetworkIdentity>().isClient) // Evita log se for apenas um cliente remoto
             {
                 Debug.LogWarning("GameManager: More than one instance found in the scene! Check setup.");
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
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float remainingTime = 60f;

    [SyncVar(hook = nameof(OnTotalTrashChanged))]
    public int totalTrashCount = 0;

    [SyncVar(hook = nameof(OnCollectedTrashChanged))]
    public int collectedTrashCount = 0;

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

     // --- Para mapear tipo de lixo para cor (Usado no servidor ANTES de spawnar) ---
     [Header("Trash Type Colors")]
     public List<Color> trashTypeColors = new List<Color>(); // Mapeia TrashType (exceto None) para cor

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
         if (!isServer) yield break; // Garante que estamos no servidor

         Debug.Log($"Starting Game - Difficulty: {currentDifficultyIndex}");

        // Configurações baseadas na dificuldade selecionada
        if (currentDifficultyIndex >= 0 && currentDifficultyIndex < difficultyTimes.Count &&
            currentDifficultyIndex < difficultyTrashCounts.Count &&
            currentDifficultyIndex < difficultyTrashTypes.Count)
        {
            remainingTime = difficultyTimes[currentDifficultyIndex];
            totalTrashCount = difficultyTrashCounts[currentDifficultyIndex];
            List<TrashType> typesToSpawn = difficultyTrashTypes[currentDifficultyIndex];

            // --- Spawning do Lixo (AGORA COM POSIÇÕES ALEATÓRIAS) ---
            SpawnTrashItems(totalTrashCount, typesToSpawn,
                            spawnAreaMinX, spawnAreaMaxX,
                            spawnAreaMinY, spawnAreaMaxY); // Passa os limites da área

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
    // Recebe os limites da área para gerar posições aleatórias
    void SpawnTrashItems(int count, List<TrashType> types,
                         float minX, float maxX, float minY, float maxY)
    {
        if (trashItemPrefab == null)
        {
             Debug.LogError("TrashItemPrefab is not assigned in GameManager!");
             return;
        }
         if (trashTypeColors.Count < System.Enum.GetValues(typeof(TrashType)).Length -1) // -1 for None
        {
             Debug.LogWarning("TrashTypeColors list is not fully assigned in GameManager. Trash may spawn without color!");
             // Continua, mas com aviso
        }


        for (int i = 0; i < count; i++)
        {
            // Seleciona o tipo de lixo para spawnar (aleatoriamente entre os tipos permitidos)
            TrashType randomType = types[Random.Range(0, types.Count)];

            // --- Gera Posição Aleatória ---
            Vector3 spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f); // Assume 2D no plano XY
            // -----------------------------

            GameObject trashGO = Instantiate(trashItemPrefab, spawnPos, Quaternion.identity);
            TrashItem trashItem = trashGO.GetComponent<TrashItem>();
            SpriteRenderer trashRenderer = trashGO.GetComponent<SpriteRenderer>(); // Pega o SpriteRenderer

             if (trashItem != null)
             {
                  // Define o tipo do item ANTES de spawnar na rede
                 trashItem.type = randomType;

                 // --- Define a Cor no Servidor ANTES de Spawnar ---
                 // Isso garante que a cor inicial é sincronizada pelo Mirror
                 if (trashRenderer != null)
                 {
                     int typeIndex = (int)randomType - 1; // Assumindo que None é 0, e Paper=1, Plastic=2, etc.
                     if (typeIndex >= 0 && typeIndex < trashTypeColors.Count)
                     {
                         trashRenderer.color = trashTypeColors[typeIndex];
                     } else {
                         Debug.LogWarning($"Color not defined for trash type {randomType}. Using white.");
                         trashRenderer.color = Color.white;
                     }
                 }
                 // --------------------------------------------------
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

        Debug.Log($"Game Ended. Result: {(win ? "Victory" : "Defeat")}");
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
         yield return new WaitForSecondsRealtime(delay); // Use Realtime para não ser afetado pelo timer do jogo
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
     // Se o TrashItem.UpdateSpriteBasedOnType precisar de uma lista de cores no cliente
     // para manter a cor após spawn, pode usar isso.
     // O servidor já define a cor ANTES de spawnar, o que DEVERIA sincronizar,
     // mas as vezes hooks ou OnStartClient podem ser necessários para reforçar.
     /*
     public Color GetColorForTrashType(TrashType type)
     {
         int index = (int)type -1;
          if (index >= 0 && index < trashTypeColors.Count)
         {
             return trashTypeColors[index];
         }
         return Color.white; // Cor padrão
     }
     */

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