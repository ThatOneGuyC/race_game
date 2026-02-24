using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.EditorTools;
using UnityEngine;

// Add AiSpawnPosition prefabs as children 
// to this manager to set spawn positions for AI cars

[RequireComponent(typeof(BezierBaker))]
public class AiCarManager : MonoBehaviour
{
    [Header("AI Car Settings")]
    [Tooltip("Number of AI cars to spawn. 0 = no AI cars.")]
    [Range(0, 100)]
    [SerializeField] private byte spawnedAiCarCount = 0;
    [SerializeField] private AiCarController[] AiCarPrefabs;
    public List<AiCarController> AiCars;
    private AIDifficulty difficulty;
    public Vector3[] Waypoints { get; private set; }
    private GameManager gm;
    
    public enum AIDifficulty { Beginner, Intermediate, Hard } 
 
    public struct DifficultyStats
    {
        public float minSpeed, maxSpeed, minAccel, maxAccel, avoidance;
        public DifficultyStats(float minS, float maxS, float minA, float maxA, float avoidanceMultiplier)
        {
            minSpeed = minS; 
            maxSpeed = maxS; 
            minAccel = minA; 
            maxAccel = maxA;
            avoidance = avoidanceMultiplier;
        }
    }

    private readonly Dictionary<AIDifficulty, DifficultyStats> difficultyRanges = new()
    {
        { AIDifficulty.Beginner,     new DifficultyStats(105f, 115f, 240f, 290f, 1f) },
        { AIDifficulty.Intermediate, new DifficultyStats(120f, 130f, 270f, 290f, 0.7f) },
        { AIDifficulty.Hard,         new DifficultyStats(130f, 140f, 280f, 300f, 0.3f) }
    };

    void Start()
    {
        BezierBaker bezierBaker = GetComponent<BezierBaker>();
        Waypoints = bezierBaker.GetCachedPoints();
        spawnedAiCarCount = (byte)PlayerPrefs.GetInt("AIAmount");
        difficulty = (AIDifficulty)PlayerPrefs.GetInt("AILevel");

        gm = GameManager.instance;
        if (gm == null || gm.CurrentCar == null) return;

        // Spawn AI Cars at spawn points
        if (spawnedAiCarCount > 0)
        {
            // Find Spawn points in children
            Transform[] spawnPoints = GetComponentsInChildren<Transform>().Where(t => t != transform).ToArray();
            
            // Iterate through spawn points
            for (int i = 0; i < spawnedAiCarCount; i++)
            {
                // Get a random prefab from the list
                AiCarController prefab = AiCarPrefabs[UnityEngine.Random.Range(0, AiCarPrefabs.Length)];
                
                // Spawn the AI car
                GameObject newAI = Instantiate(prefab.gameObject, spawnPoints[i % spawnPoints.Length].position, transform.rotation);

                AiCarController controller = newAI.GetComponent<AiCarController>();
                controller.Initialize(this, gm.CurrentCar.GetComponentInChildren<Collider>(), difficultyRanges[difficulty]);
                
                AiCars.Add(controller);
            }
        }
    }
}
