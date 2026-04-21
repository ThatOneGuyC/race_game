using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Collections.Generic;



public class GameManager : MonoBehaviour
{
    public CarInputActions Controls;
    public static GameManager instance;
    public static RacerScript racerscript;
    public GameObject CarUI;

    public bool isPaused => Time.timeScale == 0;

    public GameObject CurrentCar { get; private set; }
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform reverse_playerSpawn;
    [SerializeField] private GameObject[] cars;
    [NonSerialized] public HashSet<BaseCarController> spawnedCars = new();

    public string sceneSelected => SceneManager.GetActiveScene().name;
    private readonly string[] maps = new string[]
    {
        "shoreline",
        "shoreline_night",
        "canyon",
        "canyon_night"
    };

    void Awake()
    {
        instance = this;
        Controls = new();
        Controls.Enable();

        if (maps.Contains(sceneSelected) && cars.Length > 0)
        {
            GameObject selectedCar = cars.FirstOrDefault(c => c.name == PlayerPrefs.GetString("SelectedCar"));
            if (selectedCar == null) selectedCar = cars[0];
            Transform spawn = PlayerPrefs.GetInt("Reverse") == 1 ? reverse_playerSpawn : playerSpawn;
            CurrentCar = Instantiate(selectedCar, spawn.position, spawn.rotation);
            spawnedCars.Add(CurrentCar.GetComponentInChildren<BaseCarController>());
            #if UNITY_EDITOR
                Controls.CarControls.Debug_Win.performed += context => ManualRaceEnd();
            #endif
        }
    }

    void OnEnable()
    {
        racerscript = FindAnyObjectByType<RacerScript>();
    }
    #if UNITY_EDITOR
        void OnDisable()
        {
            Controls.Disable();
            Controls.CarControls.Debug_Win.performed -= context => ManualRaceEnd();
        }
        void OnDestroy()
        {
            Controls.Disable();
            Controls.CarControls.Debug_Win.performed -= context => ManualRaceEnd();
        }
    #endif

    public void ManualRaceEnd()
    {
        if (racerscript.raceFinished) return;
        StartCoroutine(racerscript.EndRace());
    }
}