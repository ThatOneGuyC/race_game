using System;
using UnityEngine;
using UnityEngine.UI;

public class TimeSprite : MonoBehaviour
{
    public Sprite[] numberSprites;
    public Sprite[] redNumberSprites;
    public GameObject digitPrefab;
    [Tooltip("How many digits to show (should be 4 for SSSd, e.g. 0123 = 12.3s).")]
    public const int digitCount = 4;
    private RacerScript racerScript;
    private Image[] digitImages = new Image[digitCount];

    private string TimeString => TimeSeconds.ToString().PadLeft(3, '0') + TimeTenths.ToString();
    private string lastTimeString = "";
    private int TimeSeconds => Mathf.FloorToInt(racerScript.laptime);
    private int TimeTenths => Mathf.FloorToInt((racerScript.laptime - TimeSeconds) * 10f);

    void Start()
    {
        if (GameManager.instance.CurrentCar != null) racerScript = GameManager.instance.CurrentCar.GetComponentInChildren<RacerScript>();
        for (int i = 0; i < digitCount; i++)
        {
            GameObject digitGO = Instantiate(digitPrefab, transform);
            digitImages[i] = digitGO.GetComponent<Image>();
        }
        if (numberSprites == null) throw new NullReferenceException($"numberSprites is null");
        if (redNumberSprites == null) throw new NullReferenceException($"redNumberSprites is null");
    }

    void Update()
    {
        if (TimeString != lastTimeString) UpdateTimeUI(TimeString);
        lastTimeString = TimeString;
    }

    void UpdateTimeUI(string timeString)
    {
        for (int i = 0; i < digitCount; i++)
        {
            //i on jokanen erillinen ajastimen numero
            
            int digit = timeString[i] - '0';

            if (i >= timeString.Length || digit < 0 || digit > 9) continue;
            if (i == timeString.Length - 1) digitImages[i].sprite = redNumberSprites[digit];
            else digitImages[i].sprite = numberSprites[digit];
        }
    }
}
