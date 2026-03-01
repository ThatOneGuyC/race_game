using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;

public class userDataInput : MonoBehaviour
{
    private TMP_InputField inputField;
    public string userName;
    private TextAsset jsonText;
    private TextAsset jsonRegexText;
    private string[] bannedNamesArray;
    private string[] bannedNamePopups;
    [SerializeField] private Text bannedPopup;
    private Button enter;
    private RacerScript racerscript;
    private HashSet<string> bannedRegexWords;

    void OnEnable()
    {
        inputField = GameObject.Find("userDataInput").GetComponent<TMP_InputField>();
        enter = GameObject.Find("SubmitButton").GetComponent<Button>();

        jsonText = Resources.Load<TextAsset>("bannedNames");
        bannedNamesArray = JsonUtility.FromJson<BannedNames>(jsonText.text).names.ToArray();
        jsonRegexText = Resources.Load<TextAsset>("regexReplacementValues");
        Dictionary<char, string> regexReplacements = JsonConvert.DeserializeObject<Dictionary<char, string>>(jsonRegexText.text);
        bannedNamePopups = new string[]
        {
            "Name cannot be empty!",
            "Invalid name!"
        };
        bannedRegexWords = AddRegexToHashset(bannedNamesArray, regexReplacements);
    }

    void Start()
    {
        racerscript = FindFirstObjectByType<RacerScript>();
    }
    
    [Serializable]
    public class BannedNames
    {
        public string[] names;
    }

    /// <summary>
    /// ota lista kielletyistä sanoista (bannedWords) ja lisää sille maholliset kirjainten korvaukset (replacements).
    /// </summary>
    /// <param name="bannedWords">array tuhmista sanoista >:(</param>
    /// <param name="replacements">dictionary mahollisista korvauksista, esim. "b":"[83]"</param>
    /// <returns>hashset string lol</returns>
    
    // kiitos lamelemon
    private HashSet<string> AddRegexToHashset(string[] bannedWords, Dictionary<char, string> replacements)
    {
        HashSet<string> bannedRegex = new();

        foreach (string word in bannedWords)
        {
            string regexWord = "";
            foreach (char letter in word)
            {
                if (replacements.Keys.Contains(letter))
                {
                    regexWord += replacements[letter];
                }
                else
                {
                    regexWord += letter;
                }
            }
            //lisää pahat sanat pahaan paikkaan
            bannedRegex.Add(regexWord);
        }

        return bannedRegex;
    }

    public void UpdateUserName()
    {
        userName = inputField.text.ToLower();
        Debug.Log($"edited; new name: {userName}");
    }

    public void CheckForInvalidName()
    {
        long startTime = DateTime.Now.Ticks;
        foreach (string bannedName in bannedRegexWords)
        {
            //huom. tää jo toimii
            if (Regex.IsMatch(userName, bannedName))
            {
                Debug.Log("THIS IS A BAD NAME!!!");
                bannedPopup.text = bannedNamePopups[1];
                enter.interactable = false;
                Debug.Log($"Username censor completed in {(DateTime.Now.Ticks - startTime) / 10} microseconds");
                return;
            }
        } 

        if (userName.Length == 0)
        {
            bannedPopup.text = bannedNamePopups[0];
            enter.interactable = false;
        }
        else
        {
            bannedPopup.text = "";
            enter.interactable = true;
        }
    }

    public void SaveDataWithUserName()
    {
        RaceResultCollector.instance.SaveRaceResult(userName);
        racerscript.FinalizeRaceAndSaveData();
    }
}
