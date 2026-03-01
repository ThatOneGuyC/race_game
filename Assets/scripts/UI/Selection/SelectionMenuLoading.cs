using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

[System.Serializable]
public class specialTextChances
{
    public float error;
    public float allthetexts;
    public float chance;
    public float outoftime;
    public float juud7;
    public float grass;
    public float reallyspecial;
    public float nine_trillion;
}

public class SelectionMenuLoading : MonoBehaviour
{
    [SerializeField] private Text loadText_text;
    private TextAsset loadTexts;
    private Dictionary<string, string[]> textData;
    private int index = -1;

    void OnEnable()
    {
        loadTexts = Resources.Load<TextAsset>("loading/loadTexts");
        textData = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(loadTexts.text);
    }
    void Start()
    {
        // pitäs toimia
        loadingTexts();
        specialLoadingTexts();
    }

    public void loadingTexts()
    {
        UnityEngine.Random.InitState(DateTime.Now.Millisecond);
        int chance = UnityEngine.Random.Range(1, 101);
        string loadTextRarity;
        int randomIndex;

        if (chance <= 2) //2%
        {
            loadTextRarity = "obscure";
        }
        else if (chance <= 8) //6%
        {
            loadTextRarity = "rare";
        }
        else if (chance <= 26) //18%
        {
            loadTextRarity = "uncommon";
        }
        else //74%
        {
            loadTextRarity = "general";
        }

        string[] texts = loadTextRarity switch
        {
            "general" => textData["general"],
            "uncommon" => textData["uncommon"],
            "rare" => textData["rare"],
            "obscure" => textData["obscure"],
            _ => new string[] { "THIS IS NOT SUPPOSED TO SHOW UP" },
        };
        randomIndex = UnityEngine.Random.Range(0, texts.Length);
        loadText_text.text = texts[randomIndex];
    }

    public void specialLoadingTexts()
    {
        TextAsset specialTextChancesFile = Resources.Load<TextAsset>("loading/specialTextChances");
        specialTextChances specialChances = JsonUtility.FromJson<specialTextChances>(specialTextChancesFile.text);

        UnityEngine.Random.InitState(DateTime.Now.Millisecond);

        var fields = typeof(specialTextChances).GetFields();
        foreach (var field in fields)
        {
            index += 1;

            string key = field.Name;
            float value = (float)field.GetValue(specialChances);

            float sChance = UnityEngine.Random.Range(value * 1000, 100000);
            if (sChance <= value * 1000)
            {
                loadText_text.text = textData["special"][index];
                Debug.Log(key);

                loadtextbehaviour loadtextbehaviour = gameObject.GetComponent<loadtextbehaviour>();
                loadtextbehaviour.SetSpecialLoadTextBehaviour(key);

                break;
            }
            else
            {
                Debug.Log("special load text not triggered");
            }
        }
    }
}
