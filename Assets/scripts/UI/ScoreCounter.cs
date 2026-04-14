using System;
using UnityEngine;
using UnityEngine.UI;

public class ScoreCounter : MonoBehaviour
{
    public Sprite[] numberSprites;
    public GameObject digitPrefab;

    private const int scoreNumberCount = 7;
    private Image[] scoreNumberImages = new Image[scoreNumberCount];
    private string ScoreString => Score.ToString().PadLeft(scoreNumberCount, '0');
    private string lastScoreString = "";
    private int Score => ScoreManager.instance.GetScoreInt();

    void Start()
    {
        for (int i = 0; i < scoreNumberCount; i++)
        {
            GameObject number = Instantiate(digitPrefab, transform);
            scoreNumberImages[i] = number.GetComponent<Image>();
            if (scoreNumberImages[i] == null) throw new NullReferenceException($"index of {i} was null in scoreNumberImages");
        }
    }

    void Update()
    {
        if (ScoreString != lastScoreString) UpdateScoreUI(ScoreString, lastScoreString);
        lastScoreString = ScoreString;
    }

    void UpdateScoreUI(string scoreString, string prevScoreString)
    {
        if (numberSprites == null) return;
        for (int i = 0; i < scoreNumberCount; i++) if (prevScoreString.Length != scoreNumberCount || prevScoreString[i] != scoreString[i]) scoreNumberImages[i].sprite = numberSprites[(byte)(scoreString[i] - '0')];
    }
}
