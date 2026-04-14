using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.Linq;

public class SelectionMenuLoadTexts : MonoBehaviour
{
    [SerializeField] private Text loadText;
    private TextAsset loadTexts;
    private TextAsset specialLoadChances;
    private Dictionary<string, string[]> loadTextsData;
    private Dictionary<string, float> specialLoadChancesData;

    void OnEnable()
    {
        loadTexts = Resources.Load<TextAsset>("loading/loadTexts");
        loadTextsData = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(loadTexts.text);
        specialLoadChances = Resources.Load<TextAsset>("loading/specialTextChances");
        specialLoadChancesData = JsonConvert.DeserializeObject<Dictionary<string, float>>(specialLoadChances.text);
    }
    void Start()
    {
        LoadingTexts();
        SpecialLoadingTexts();
    }

    public void LoadingTexts()
    {
        UnityEngine.Random.InitState(DateTime.Now.Millisecond);
        int chance = UnityEngine.Random.Range(1, 101);
        string loadTextRarity;
        int randomIndex;

        if (chance <= 4) loadTextRarity = "obscure";
        else if (chance <= 10) loadTextRarity = "rare";
        else if (chance <= 30) loadTextRarity = "uncommon";
        else loadTextRarity = "general";

        string[] texts = loadTextRarity switch
        {
            "general" => loadTextsData["general"],
            "uncommon" => loadTextsData["uncommon"],
            "rare" => loadTextsData["rare"],
            "obscure" => loadTextsData["obscure"],
            _ => new string[] { "THIS IS NOT SUPPOSED TO SHOW UP" },
        };
        randomIndex = UnityEngine.Random.Range(0, texts.Length);
        loadText.text = texts[randomIndex];
    }

    public void SpecialLoadingTexts()
    {
        UnityEngine.Random.InitState(DateTime.Now.Millisecond);
        foreach (var (entry, index) in specialLoadChancesData.Select((entry, index) => (entry, index)))
        {
            string key = entry.Key;
            float value = entry.Value;
            float chance = UnityEngine.Random.Range(value * 1000, 100000);

            if (chance <= value * 1000)
            {
                loadText.text = loadTextsData["special"][index];
                SpecialLoadTextBehaviour loadtextbehaviour = GetComponent<SpecialLoadTextBehaviour>();
                loadtextbehaviour.SetSpecialLoadTextBehaviour(key);
                
                Debug.Log($"{key} was triggered");
                break;
            }
            else Debug.Log("special load text not triggered");
        }
    }
}
