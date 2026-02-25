using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using UnityEditor.SearchService;
using UnityEngine.SceneManagement;

public class RacerScript : MonoBehaviour
{
    public GameObject winMenu; 
    public GameObject Minimap;
    private GameObject respawnfade;
    private bool FadeState;

    CarInputActions Controls;

    public float laptime;
    public float besttime;
    public bool racestarted = false; // <-- Add this
    private bool startTimer = false;

    // Other variables
    public Transform startFinishLine;
    public List<Transform> checkpoints;
    public int CurrentLap => currentLap;

    private bool[] checkpointStates;

    private int currentLap = 1;
    private int totalLaps;
    public bool raceFinished = false;

    private Transform respawnPoint;
    private musicControl musicControl;
    private soundFXControl soundControl;

    public GameObject[] endButtons;
    public GameObject[] otherStuff;
    private GameObject finalLapImg;

    private PlayerCarController carController;

    void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        musicControl = FindAnyObjectByType<musicControl>();
        soundControl = FindAnyObjectByType<soundFXControl>();
        carController = GetComponent<PlayerCarController>();
    }

    private void OnEnable()
    {
        Controls.Enable();
        Controls.CarControls.respawn.performed += ctx => FadeGameViewAndRespawn();

        startFinishLine = GameObject.FindGameObjectWithTag("StartFinishLine").transform;
        checkpoints = GameObject.FindGameObjectsWithTag("checkpointTag").Select(a => a.transform).ToList();
        if (SceneManager.GetActiveScene().name != "tutorial") SetupRacingShit();
        if (GameManager.instance.CarUI != null) respawnfade = GameManager.instance.CarUI.transform.Find("respawnfade").gameObject;
        totalLaps = PlayerPrefs.GetInt("Laps");
    }

    private void OnDisable()
    {
        Controls.Disable();
        Controls.CarControls.respawn.performed -= ctx => FadeGameViewAndRespawn();
    }

    private void OnDestroy()
    {
        Controls.Disable();
        Controls.CarControls.respawn.performed -= ctx => FadeGameViewAndRespawn();
        Controls.Dispose();
    }

    void Start()
    {
        respawnPoint = startFinishLine;
        checkpointStates = new bool[checkpoints.Count];
    }

    void Update()
    {
        if (!racestarted || raceFinished) return;
        HandleReset();
    }
    void FixedUpdate()
    {
        if (!racestarted || raceFinished) return;
        laptime += Time.fixedDeltaTime;
    }

    void OnTriggerEnter(Collider trigger)
    {
        if (trigger.gameObject.CompareTag("StartFinishLine")) HandleStart();
        else if (trigger.gameObject.CompareTag("RespawnTrigger")) FadeGameViewAndRespawn(0.8f);
        else HandleCheck(trigger);
    }

    private void SetupRacingShit()
    {
        winMenu = GameObject.Find("WinMenu").GetComponentInChildren<Canvas>(true).gameObject;
        if (GameManager.instance.CarUI != null) finalLapImg = GameManager.instance.CarUI.transform.Find("finalLap").gameObject;
        if (PlayerPrefs.GetInt("Reverse") == 1)
        {
            foreach (Transform checkpoint in checkpoints) checkpoint.eulerAngles = new(checkpoint.eulerAngles.x, checkpoint.eulerAngles.y + 180.0f, checkpoint.eulerAngles.z);
            startFinishLine.eulerAngles = new(startFinishLine.eulerAngles.x, startFinishLine.eulerAngles.y + 180.0f, startFinishLine.eulerAngles.z);
        }
    }

    //helper method fadeaamiselle
    private void FadeGameViewAndRespawn(float length = 0.25f)
    {
        if (GameManager.instance.isPaused || !racestarted || raceFinished || FadeState) return;

        FadeState = true;
        LeanTween.value(respawnfade.GetComponent<RawImage>().color.a, 1f, length).setOnUpdate((float val) =>
        {
            var img = respawnfade.GetComponent<RawImage>();
            Color c = img.color;
            c.a = val;
            img.color = c;
        })
        .setOnComplete(() =>
        {
            RespawnAtLastCheckpoint();
            LeanTween.value(respawnfade.GetComponent<RawImage>().color.a, 0f, 0.25f).setOnUpdate((float val) =>
            {
                var img = respawnfade.GetComponent<RawImage>();
                Color c = img.color;
                c.a = val;
                img.color = c;
            })
            .setOnComplete(() => FadeState = false);
        });
    }

    public void RespawnAtLastCheckpoint()
    {
        Debug.Log("Respawning at the last checkpoint...");
        transform.SetPositionAndRotation(respawnPoint != null ? respawnPoint.position : startFinishLine.position,
        respawnPoint != null ? respawnPoint.rotation : startFinishLine.rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        carController.ClearWheelTrails();
    }

    void HandleReset()
    {
        if (transform.position.y < -1)
        {
            RespawnAtLastCheckpoint();
        }
    }

    void HandleStart()
    {
        if (!startTimer)
        {
            StartNewLap();
        }

        bool allCheckpointsPassed = true;
        for (int i = 0; i < checkpointStates.Length; i++)
        {
            if (!checkpointStates[i])
            {
                allCheckpointsPassed = false;
                break;
            }
        }

        if (allCheckpointsPassed)
        {
            currentLap++;

            //FINAL LAP CHECK
            if (currentLap == totalLaps)
            {
                //musicControl.StartFinalLapTrack();
                LeanTween.value(finalLapImg,
                finalLapImg.GetComponent<RectTransform>().anchoredPosition.x, 0.0f, 0.6f)
                .setOnUpdate((float val) =>
                {
                    finalLapImg.GetComponent<RectTransform>().anchoredPosition = new Vector2(val, finalLapImg.GetComponent<RectTransform>().anchoredPosition.y);
                })
                .setEaseInOutCirc()
                .setOnComplete(() =>
                    LeanTween.value(finalLapImg,
                    finalLapImg.GetComponent<RectTransform>().anchoredPosition.x, -530.0f, 2.4f)
                    .setOnUpdate((float val) =>
                    {
                        finalLapImg.GetComponent<RectTransform>().anchoredPosition = new Vector2(val, finalLapImg.GetComponent<RectTransform>().anchoredPosition.y);
                    })
                    .setEaseInExpo()
                );
            }

            if (currentLap > totalLaps)
            {
                raceFinished = true;
                startTimer = false;

                if (besttime == 0 || laptime < besttime)
                {
                    besttime = laptime;
                }

                EndRace();
            }
            else
            {
                for (int i = 0; i < checkpointStates.Length; i++)
                {
                    checkpointStates[i] = false;
                }
                respawnPoint = startFinishLine;
            }
        }
    }

    void HandleCheck(Collider trigger)
    {
        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (trigger.transform == checkpoints[i])
            {
                checkpointStates[i] = true;
                respawnPoint = checkpoints[i];
                break;
            }
        }
    }

    void StartNewLap()
    {
        startTimer = true;
        laptime = 0;
        for (int i = 0; i < checkpointStates.Length; i++)
        {
            checkpointStates[i] = false;
        }
        respawnPoint = startFinishLine;
    }

    //KUTSU TÄÄ FUNKTIO AINOASTAA SILLO KU KISA LOPPUU!!!
    public void EndRace()
    {
        respawnPoint = startFinishLine;

        for (int i = 0; i < checkpointStates.Length; i++)
        {
            checkpointStates[i] = false;
        }

        musicControl.StopMusicTracks(true);

        if (GameManager.instance.CarUI != null)
            GameManager.instance.CarUI.SetActive(false);
        //nää pitää korjata
        if (startFinishLine != null)
            startFinishLine.gameObject.SetActive(false);
        if (Minimap != null)
            Minimap.SetActive(false);
        winMenu.SetActive(true);
        raceFinished = true;
        startTimer = false;


        carController.StopDrifting();
        carController.CanDrift = false;
        endButtons = GameObject.FindGameObjectsWithTag("winmenubuttons")
            .OrderBy(r => r.name)
            .ToArray();
        otherStuff = GameObject.FindGameObjectsWithTag("winmenuother")
            .OrderBy(r => r.name)
            .ToArray();
        TMP_InputField playerInput = otherStuff[1].GetComponent<TMP_InputField>();
        playerInput.Select();
    }

    //pitää kutsua sillo ku haluaa tallentaa
    public void FinalizeRaceAndSaveData()
    {
        if (winMenu != null)
        {
            Button returnButton = endButtons[0].GetComponent<Button>();

            foreach (GameObject go in endButtons)
            {
                LeanTween.value(go, go.GetComponent<RectTransform>().anchoredPosition.x, -20.0f, 1.2f)
                .setOnUpdate((float val) =>
                {
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(val, go.GetComponent<RectTransform>().anchoredPosition.y);
                })
                .setEaseOutBack();
            }
            foreach (GameObject go in otherStuff)
            {
                LeanTween.value(go, go.GetComponent<RectTransform>().anchoredPosition.y, -110.0f, 0.4f)
                .setOnUpdate((float val) =>
                {
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(go.GetComponent<RectTransform>().anchoredPosition.x, val);
                })
                .setEaseInOutQuart();
            }

            GameObject finishedImg, resultsImg;
            finishedImg = winMenu.transform.Find("Race Finished").gameObject;
            resultsImg = winMenu.transform.Find("Race Results").gameObject;

            LeanTween.value(finishedImg, finishedImg.GetComponent<RectTransform>().anchoredPosition.y, 150.0f, 0.6f)
            .setOnUpdate((float val) =>
            {
                    finishedImg.GetComponent<RectTransform>().anchoredPosition
                = new Vector2(finishedImg.GetComponent<RectTransform>().anchoredPosition.x, val);
            })
            .setEaseInOutQuart();
            //eri kesto tän siirtymiselle, jotta ne ei vaikuta overlappaavan
            LeanTween.value(resultsImg, resultsImg.GetComponent<RectTransform>().anchoredPosition.y, 0.0f, 0.9f)
            .setOnUpdate((float val) =>
            {
                resultsImg.GetComponent<RectTransform>().anchoredPosition
                = new Vector2(resultsImg.GetComponent<RectTransform>().anchoredPosition.x, val);
            })
            .setEaseInOutQuart();

            GameObject leaderboard = GameObject.Find("leaderboardholder");
            LeanTween.value(leaderboard, leaderboard.GetComponent<RectTransform>().anchoredPosition.y, 0.0f, 2f)
            .setOnUpdate((float val) =>
            {
                leaderboard.GetComponent<RectTransform>().anchoredPosition
                = new Vector2(leaderboard.GetComponent<RectTransform>().anchoredPosition.x, val);
            })
            .setEaseInOutQuart();

            returnButton.Select();
        }
    }

    public void StartRace() // <-- Call this from Waitbeforestart
    {
        racestarted = true;
        if (GameManager.instance.sceneSelected != "tutorial")
            musicControl.StartMusicTracks();
        startTimer = true;
    }
}