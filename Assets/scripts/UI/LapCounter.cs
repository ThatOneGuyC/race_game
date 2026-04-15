using System;
using UnityEngine;
using UnityEngine.UI;

public class LapCounter : MonoBehaviour
{
    public Sprite[] numberSprites;
    public Sprite[] finalLapNumberSprites;
    public GameObject digitPrefab;

    private Image lapNumberImage;
    private int laps;
    private RacerScript racer;
    private int CurrentLap => racer != null ? racer.currentLap : 0;
    private int previousLap;
    private SFXManager SFXMngr;

    //TODO: if lauseitten poisto ja muutama pieni parannus

    void Start()
    {
        SFXMngr = FindFirstObjectByType<SFXManager>(FindObjectsInactive.Exclude);
        if (GameManager.instance.CurrentCar != null) racer = GameManager.instance.CurrentCar.GetComponentInChildren<RacerScript>();
        laps = PlayerPrefs.GetInt("Laps");
        if (laps == 1) numberSprites = finalLapNumberSprites;

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
        if (CurrentLap == laps) numberSprites = finalLapNumberSprites;
        if (CurrentLap >= 0 && CurrentLap <= 9 && numberSprites.Length > CurrentLap)
        {
            lapNumberImage.sprite = numberSprites[CurrentLap];
            if (CurrentLap != 1) SFXMngr.nextLap.Play();
        }
    }
}
