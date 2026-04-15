using System;
using UnityEngine;
using UnityEngine.UI;

public class TimeCounter : MonoBehaviour
{
    [SerializeField] private Sprite[] numberSprites;
    [SerializeField] private Sprite[] redNumberSprites;
    [SerializeField] private GameObject digitPrefab;
    private const int timerNumberCount = 4;
    private RacerScript racerScript;
    private Image[] digitImages = new Image[timerNumberCount];

    private int TimeSeconds => Mathf.FloorToInt(racerScript.laptime);
    private int TimeTenths => Mathf.FloorToInt((racerScript.laptime - TimeSeconds) * 10f);
    private string timeString;
    private string lastTimeString = "";

    void Start()
    {
        if (GameManager.instance.CurrentCar != null) racerScript = GameManager.instance.CurrentCar.GetComponentInChildren<RacerScript>();
        for (int i = 0; i < timerNumberCount; i++)
        {
            GameObject digitGO = Instantiate(digitPrefab, transform);
            digitImages[i] = digitGO.GetComponent<Image>();
        }
        if (numberSprites == null) throw new NullReferenceException($"numberSprites is null");
        if (redNumberSprites == null) throw new NullReferenceException($"redNumberSprites is null");
    }

    void Update()
    {
        timeString = $"{TimeSeconds}".PadLeft(3, '0') + $"{TimeTenths}";
        if (timeString != lastTimeString) UpdateTimeUI();
        lastTimeString = timeString;
    }

    void UpdateTimeUI()
    {
        for (int i = 0; i < timerNumberCount; i++) digitImages[i].sprite = i == 3 ? redNumberSprites[timeString[i] - '0'] : numberSprites[timeString[i] - '0'];
    }
}
