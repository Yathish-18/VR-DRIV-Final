// CarSpawner.cs
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public GameObject npcCarPrefab;
    public Transform spawnPoint;
    public Transform[] pathOptions; // Multiple routes

    public float spawnInterval = 5f;

    void Start()
    {
        InvokeRepeating("SpawnCar", 1f, spawnInterval);
    }

    void SpawnCar()
    {
        GameObject car = Instantiate(npcCarPrefab, spawnPoint.position, spawnPoint.rotation);
        NpcCar carScript = car.GetComponent<NpcCar>();

        // Random path from options
        int randomIndex = Random.Range(0, pathOptions.Length);
        Transform selectedPath = pathOptions[randomIndex];
        carScript.path = selectedPath.GetComponentsInChildren<Transform>();
    }
}
