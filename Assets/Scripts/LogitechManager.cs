using UnityEngine;

public class LogitechManager : MonoBehaviour
{
    void Start()
    {
        if (!LogitechGSDK.LogiSteeringInitialize(false))
        {
            Debug.LogError("Logitech Steering Wheel NOT detected!");
        }
        else
        {
            Debug.Log("Logitech Steering Wheel initialized.");
        }
    }

    void OnApplicationQuit()
    {
        LogitechGSDK.LogiSteeringShutdown();
    }
}
