using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Duplicate GameManager found on {gameObject.name}. Destroying this instance.");
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Header("Game Settings")]
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float remainingTime = 60f;

    [SyncVar(hook = nameof(OnTotalTrashChanged))]
    public int totalTrashCount = 0;

    [SyncVar(hook = nameof(OnCollectedTrashChanged))]
    public int collectedTrashCount = 0;

    [SyncVar(hook = nameof(OnGameTotalDurationChanged))]
    public float gameTotalDuration = 60f;

    [Header("Difficulty Settings")]
    [Tooltip("Tempo limite para cada dificuldade (Índice 0=Fácil, 1=Médio, 2=Difícil)")]
    public List<float> difficultyTimes = new List<float> { 120f, 90f, 60f };
    [Tooltip("Número total de itens de lixo para cada dificuldade")]
    public List<int> difficultyTrashCounts = new List<int> { 10, 20, 30 };

    [Header("Trash Spawning")]
    [Tooltip("O Prefab do item de lixo (deve ter TrashItem script, SpriteRenderer, NetworkIdentity)")]
    public GameObject trashItemPrefab;
    [Tooltip("Lista de Sprites para usar aleatoriamente no lixo spawnado (Usada no Servidor para escolher o índice)")]
    public List<Sprite> trashSpritesServer;

    [Header("Random Spawn Area")]
    public float spawnAreaMinX = -10f;
    public float spawnAreaMaxX = 10f;
    public float spawnAreaMinY = -3f;
    public float spawnAreaMaxY = 3f;

    private bool gameStarted = false;
    private bool gameEnded = false;
    private int currentDifficultyIndex = 0;

    [ClientRpc]
    void RpcStartGame()
    {
        Debug.Log("GameManager Client: RpcStartGame received.");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("GameManager Server Started.");

        LobbyManager lobby = FindObjectOfType<LobbyManager>();
        int selectedDifficultyIndex = 0;

        if (lobby != null)
        {
            selectedDifficultyIndex = lobby.currentSelectedDifficulty;
            Debug.Log($"GameManager Server: Reading difficulty index {selectedDifficultyIndex} from LobbyManager.");
        } else {
             Debug.LogWarning("GameManager Server: LobbyManager not found! Using default difficulty index 0.");
        }

        ConfigureAndStartGame(selectedDifficultyIndex);
    }

    [Server]
    void ConfigureAndStartGame(int difficultyIndex)
    {
        if (!isServer) return;

        Debug.Log($"GameManager Server: Configuring game for difficulty index {difficultyIndex}.");

        if (difficultyIndex >= 0 && difficultyIndex < difficultyTimes.Count &&
            difficultyIndex < difficultyTrashCounts.Count)
        {
            gameTotalDuration = difficultyTimes[difficultyIndex];
            remainingTime = gameTotalDuration;
            totalTrashCount = difficultyTrashCounts[difficultyIndex];

            SpawnTrashItems(totalTrashCount, spawnAreaMinX, spawnAreaMaxX, spawnAreaMinY, spawnAreaMaxY);

            collectedTrashCount = 0;
            gameStarted = true;
            gameEnded = false;

            RpcStartGame();
        }
        else
        {
             Debug.LogError($"GameManager Server: Invalid difficulty index {difficultyIndex}! Cannot configure game.");
        }
    }

    [Server]
    void SpawnTrashItems(int count, float minX, float maxX, float minY, float maxY)
    {
        if (trashItemPrefab == null) { Debug.LogError("GameManager: TrashItemPrefab is not assigned!"); return; }
        if (trashSpritesServer == null || trashSpritesServer.Count == 0)
        {
             Debug.LogError("GameManager: Trash Sprites Server list is empty or null!");
             return;
        }
        Debug.Log($"GameManager Server: Spawning {count} trash items...");

         if (count > (maxX - minX) * (maxY - minY) * 2)
         {
             Debug.LogWarning($"GameManager: Spawn area ({maxX-minX}x{maxY-minY}) might be crowded for {count} items.");
         }

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);

            GameObject trashGO = Instantiate(trashItemPrefab, spawnPos, Quaternion.identity);
            TrashItem trashItem = trashGO.GetComponent<TrashItem>();

             if (trashItem != null)
             {
                 int randomSpriteIndex = Random.Range(0, trashSpritesServer.Count);
                 trashItem.spriteIndex = randomSpriteIndex;

             } else { Debug.LogError($"GameManager: Spawned object {trashItemPrefab.name} is missing TrashItem component!"); }

            NetworkServer.Spawn(trashGO);
        }
         Debug.Log($"GameManager Server: Finished spawning {count} trash items.");
    }

    void Update()
    {
        if (!isServer || gameEnded || !gameStarted) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            EndGame(false);
        }
    }

    [Server]
    public void IncrementCollectedCount()
    {
        if (gameEnded || !gameStarted) return;

        collectedTrashCount++;
        Debug.Log($"GameManager Server: Trash Collected! Count: {collectedTrashCount}/{totalTrashCount}");

         if (collectedTrashCount >= totalTrashCount && totalTrashCount > 0)
         {
             EndGame(true);
         }
    }

    [Server]
    void EndGame(bool win)
    {
        if (gameEnded) return;
        gameEnded = true;
        gameStarted = false;

        Debug.Log($"GameManager Server: Game Ended. Result: {(win ? "Victory" : "Derrota")}");

        RpcEndGame(win);
    }

    [ClientRpc]
    void RpcEndGame(bool win)
    {
        Debug.Log($"GameManager Client: RpcEndGame received. Result: {(win ? "Victory" : "Derrota")}");
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.ShowResultScreen(win);
        } else { Debug.LogWarning("GameManager Client: GameUI script not found to show result screen!"); }
    }

    void OnTimeChanged(float oldTime, float newTime)
    {
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.UpdateTimerUI(newTime, gameTotalDuration);
        }
    }

    void OnGameTotalDurationChanged(float oldDur, float newDur)
    {
        Debug.Log($"GameManager Client: Game Total Duration updated: {newDur}");
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.UpdateTimerUI(remainingTime, newDur);
        }
    }

    void OnTotalTrashChanged(int oldTotal, int newTotal)
    {
        Debug.Log($"GameManager Client: Total Trash updated: {newTotal}");
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.UpdateCollectedTrashUI(collectedTrashCount, newTotal);
        }
    }

    void OnCollectedTrashChanged(int oldCollected, int newCollected)
    {
        Debug.Log($"GameManager Client: Collected Trash updated: {newCollected}");
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.UpdateCollectedTrashUI(newCollected, totalTrashCount);
        }
    }

    public void SetDifficultyIndex(int index)
    {
        if (!gameStarted && !gameEnded)
        {
            if (index >= 0 && index < difficultyTimes.Count)
            {
                currentDifficultyIndex = index;
                Debug.Log($"GameManager: Stored difficulty index for next game: {index}");
            } else {
                Debug.LogWarning($"GameManager: Invalid difficulty index {index} received. Ignoring.");
            }
        } else { Debug.LogWarning("GameManager: Cannot change difficulty after game started."); }
    }

    public int GetCurrentSelectedDifficulty()
    {
        return currentDifficultyIndex;
    }
}