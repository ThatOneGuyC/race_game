using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections;

public class RacerScript : MonoBehaviour
{
    private GameObject respawnfade;
    private Image finishedImg;
    private bool FadeState;

    CarInputActions Controls;

    [Header("race state")]
    public float laptime;
    public bool racestarted = false;
    private bool startTimer = false;
    public bool raceFinished = false;

    [Header("start/checkpoints")]
    public Transform startFinishLine;
    public List<Transform> checkpoints;
    private bool[] checkpointStates;

    [Header("laps")]
    public int currentLap = 1;
    private int totalLaps;

    private Transform respawnPoint;
    private musicControl musicControl;
    private SFXManager sfxmngr;

    private GameObject finalLapImg;

    private PlayerCarController carController;
    private winmenu winmenu;

    void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        musicControl = FindAnyObjectByType<musicControl>();
        sfxmngr = FindAnyObjectByType<SFXManager>();
        carController = GetComponent<PlayerCarController>();
        winmenu = FindAnyObjectByType<winmenu>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        Controls.Enable();
        Controls.CarControls.respawn.performed += ctx => FadeGameViewAndRespawn();

        startFinishLine = GameObject.FindGameObjectWithTag("StartFinishLine").transform;
        checkpoints = GameObject.FindGameObjectsWithTag("checkpointTag").Select(a => a.transform).ToList();
        SetupRacingShit();
        if (GameManager.instance.CarUI != null) respawnfade = GameManager.instance.CarUI.transform.Find("respawnfade").gameObject;
        finishedImg = GameManager.instance.CarUI.transform.Find("Race Finished").GetComponent<Image>();
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
        laptime += Time.deltaTime;
    }

    void OnTriggerEnter(Collider trigger)
    {
        if (trigger.gameObject.CompareTag("StartFinishLine")) HandleStart();
        else if (trigger.gameObject.CompareTag("RespawnTrigger")) FadeGameViewAndRespawn(0.8f);
        else HandleCheck(trigger);
    }

    private void SetupRacingShit()
    {
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
        //Debug.Log("Respawning at the last checkpoint...");
        transform.SetPositionAndRotation(respawnPoint != null ? respawnPoint.position : startFinishLine.position,
        respawnPoint != null ? respawnPoint.rotation : startFinishLine.rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        carController.ClearWheelTrails();
    }

    void HandleReset()
    {
        if (transform.position.y < -1) RespawnAtLastCheckpoint();
    }

    public void StartRace()
    {
        racestarted = true;
        startTimer = true;
        musicControl.StartMusicTracks();
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
                LeanTween.value(finalLapImg, finalLapImg.GetComponent<RectTransform>().anchoredPosition.x, 0.0f, 0.6f).setOnUpdate((float val) => { finalLapImg.GetComponent<RectTransform>().anchoredPosition = new Vector2(val, finalLapImg.GetComponent<RectTransform>().anchoredPosition.y); }).setEaseInOutCirc()
                .setOnComplete(() => LeanTween.value(finalLapImg, finalLapImg.GetComponent<RectTransform>().anchoredPosition.x, -530.0f, 2.4f).setOnUpdate((float val) => { finalLapImg.GetComponent<RectTransform>().anchoredPosition = new Vector2(val, finalLapImg.GetComponent<RectTransform>().anchoredPosition.y); }) .setEaseInExpo());
            }

            if (currentLap > totalLaps)
            {
                raceFinished = true;
                startTimer = false;
                StartCoroutine(EndRace());
            }
            else
            {
                for (int i = 0; i < checkpointStates.Length; i++) checkpointStates[i] = false;
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
        for (int i = 0; i < checkpointStates.Length; i++) checkpointStates[i] = false;
        respawnPoint = startFinishLine;
    }

    //KUTSU TÄÄ AINOASTAA SILLO KU KISA LOPPUU!!!
    public IEnumerator EndRace()
    {
        respawnPoint = startFinishLine;
        for (int i = 0; i < checkpointStates.Length; i++) checkpointStates[i] = false;
        if (startFinishLine != null) startFinishLine.gameObject.SetActive(false);

        musicControl.StopMusicTracks(true);
        sfxmngr.raceFinished.Play();
        finishedImg.color = new(1f, 1f, 1f, 1f);
        //TODO: erittäin paska tapa ottaa nämä...
        GameManager.instance.CarUI.GetComponentInChildren<SpeedMeter>().gameObject.SetActive(false);
        GameManager.instance.CarUI.transform.Find("TurbeDisplay").gameObject.SetActive(false);
        LeanTween.value(finishedImg.color.a, 0.0f, 2.5f)
        .setOnUpdate((float alpha) =>
        {
            Color c = finishedImg.color;
            c.a = alpha;
            finishedImg.color = c;
        }).setIgnoreTimeScale(true).setEaseInCirc();
        StartCoroutine(StopCarSmooth());
        carController.Controls.Disable();
        raceFinished = true;
        startTimer = false;
        carController.StopDrifting();
        carController.CanDrift = false; 
        carController.CanUseTurbo = false;
        yield return new WaitForSecondsRealtime(2.5f);

        if (GameManager.instance.CarUI != null) GameManager.instance.CarUI.SetActive(false);
        musicControl.resultsTrack.Play();
        winmenu.OnRaceEnd();
        Destroy(FindFirstObjectByType<OptionCategories>(FindObjectsInactive.Include));
    }
    Rigidbody rb => GetComponent<Rigidbody>();
    IEnumerator StopCarSmooth()
    {
        while (rb.linearVelocity.magnitude > 0.05f || rb.angularVelocity.magnitude > 0.05f)
        {
            float linearSpeed = Mathf.MoveTowards(rb.linearVelocity.magnitude, 0f, 35f * Time.deltaTime);
            float angularSpeed = Mathf.MoveTowards(rb.angularVelocity.magnitude, 0f, 140f * Time.deltaTime);

            rb.linearVelocity = rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized * linearSpeed : Vector3.zero;
            rb.angularVelocity = rb.angularVelocity.sqrMagnitude > 0.0001f ? rb.angularVelocity.normalized * angularSpeed : Vector3.zero;
            yield return null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

}