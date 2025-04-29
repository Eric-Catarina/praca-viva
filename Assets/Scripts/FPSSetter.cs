using UnityEngine;

public class FPSSetter : MonoBehaviour
{
    [Tooltip("Taxa de quadros alvo para a build Android")]
    public int targetFPS = 60;

    void Awake()
    {
        Application.targetFrameRate = targetFPS;

        // Define a taxa de quadros alvo apenas se estiver em uma build
#if !UNITY_EDITOR
         Application.targetFrameRate = targetFPS;
         Debug.Log($"Target Frame Rate set to {targetFPS}");
#else
        // Opcional: Mostrar no editor para referência, mas o Editor tem seu próprio limite
        // Debug.Log($"Editor Frame Rate (not forced): {targetFPS}");
#endif
    }
    /*
    // Opcional: Se DontDestroyOnLoad for usado em algum gerenciador,
    // pode querer mover isso para um Awake em script que não use DontDestroyOnLoad
    // para garantir que seja definido em cada cena se necessário.
    void Start()
    {
        // Repetir em Start pode ser redundante se já fez em Awake
    }
    */
}