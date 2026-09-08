using UnityEngine;
using TMPro;
using System.Text;

public class Version : MonoBehaviour
{
    [SerializeField] private string changes;
    private void OnValidate()
    {
        ChangeText();
    }
    private void Awake()
    {
        ChangeText();
    }
    private void ChangeText()
    {
        StringBuilder sb = new();
        sb.Append($"BETA | DEMO V{Application.version}");
        sb.Append($"\n{changes}");
        GetComponent<TMP_Text>().text = sb.ToString();
    }
}