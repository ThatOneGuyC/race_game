using System;
using UnityEngine;
using UnityEngine.UI;

public class LapCounter : MonoBehaviour
{
    [SerializeField] private Sprite[] numberSprites;
    [SerializeField] private Sprite[] finalLapNumberSprites;
    [SerializeField] private GameObject digitPrefab;

    private Image lapNumberImage;
    private int laps;
    private RacerScript racer;
    private int CurrentLap => racer != null ? racer.currentLap : 0;
    private int previousLap;
    private SFXManager SFXMngr;

    void Start()
    {
        SFXMngr = FindFirstObjectByType<SFXManager>(FindObjectsInactive.Exclude);
        if (GameManager.instance.CurrentCar != null) racer = GameManager.instance.CurrentCar.GetComponentInChildren<RacerScript>();
        laps = PlayerPrefs.GetInt("Laps");
        numberSprites = laps == 1 ? finalLapNumberSprites : numberSprites;

        GameObject digitGO = Instantiate(digitPrefab, transform);
        lapNumberImage = digitGO.GetComponent<Image>();
        if (lapNumberImage == null) throw new NullReferenceException($"lapNumberImage is null");
        if (numberSprites == null) throw new NullReferenceException($"numberSprites is null");
    }

    void Update()
    {
        if (CurrentLap != previousLap) UpdateLapUI();
        previousLap = CurrentLap;
    }

    void UpdateLapUI()
    {
        numberSprites = CurrentLap == laps ? finalLapNumberSprites : numberSprites;
        if (CurrentLap >= 0 && CurrentLap <= 9 && numberSprites.Length > CurrentLap)
        {
            lapNumberImage.sprite = numberSprites[CurrentLap];
            if (CurrentLap != 1) SFXMngr.nextLap.Play();
        }
    }
}
