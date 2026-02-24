using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class RaceResultCollector : MonoBehaviour
{
    public static RaceResultCollector instance;

    [SerializeField] private string fileName = "race_result.json";

    private RaceResultHandler resultHandler;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //lol
        if (SceneManager.GetActiveScene().name == "tutorial") resultHandler = new RaceResultHandler(Application.persistentDataPath, "tutorialMapResult.json");
        else
        {
            resultHandler = new RaceResultHandler(Application.persistentDataPath, fileName);
            Debug.Log($"Race results will be saved to: {Path.Combine(Application.persistentDataPath, fileName)}");
        }
    }

    /// <summary>
    /// Call this method when the player wins the race.
    /// Collects score, time, and map data and saves it to a file.
    /// </summary>
    public void SaveRaceResult(string racerName = "Unknown")
    {
        int score = GetScore();
        float time = GetTime();
        string map = GetMap();
        string carName = GetCarName();

        RaceResultData resultData = new(score, time, map, racerName, carName);
        resultHandler.Save(resultData);

        Debug.Log($"Race Result Saved - Racer: {racerName}, Score: {score}, Time: {time:F2}, Map: {map}, Car: {carName}");
    }

    private int GetScore()
    {
        if (ScoreManager.instance != null)
        {
            return ScoreManager.instance.GetScoreInt();
        }

        return 0;
    }

    private float GetTime()
    {
        RacerScript racer = FindFirstObjectByType<RacerScript>();
        if (racer != null)
        {
            // Round to 2 decimal places
            return Mathf.Round(racer.laptime * 100f) / 100f;
        }

        return 0f;
    }

    private string GetMap()
    {
        string tester = SceneManager.GetActiveScene().name;
        return tester switch
        {
            "haukipudas" => "Shoreline Day",
            "haukipudas_night" => "Shoreline Night",
            "ai_haukipudas" => "Shoreline Day [AI]",
            "ai_haukipudas_night" => "Shoreline Night [AI]",
            "canyon" => "Canyon Day",
            "canyon_night" => "Canyon Night",
            "ai_canyon" => "Canyon Day [AI]",
            "ai_canyon_night" => "Canyon Night [AI]",
            "tutorial" => "Tutorial",
            _ => "Unknown"
        };
    }

    private string GetCarName()
    {
        string car = GameManager.instance.CurrentCar.name;
        int endIndex = car.IndexOf("(");
        string result = car.Substring(0, endIndex);
        if (GameManager.instance != null && GameManager.instance.CurrentCar != null)
        {
            return result;
        }
        
        return "Unknown";
    }

    /// <summary>
    /// Load all saved race results.
    /// </summary>
    public RaceResultCollection LoadAllRaceResults()
    {
        return resultHandler.Load();
    }
}