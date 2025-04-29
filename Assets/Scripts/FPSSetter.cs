using UnityEngine;

public class FPSSetter : MonoBehaviour
{
    [Tooltip("Taxa de quadros alvo para a build Android")]
    public int targetFPS = 60;

    void Awake()
    {
        Application.targetFrameRate = targetFPS;

#if !UNITY_EDITOR
         Application.targetFrameRate = targetFPS;
         Debug.Log($"Target Frame Rate set to {targetFPS}");
#else

#endif
    }

}