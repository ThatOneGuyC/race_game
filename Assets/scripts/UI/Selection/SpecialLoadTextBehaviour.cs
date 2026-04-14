using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SelectionMenuLoadTexts))]
public class SpecialLoadTextBehaviour : MonoBehaviour
{
    [SerializeField] private Image loadingImage;
    [SerializeField] private Sprite specialLoadingScreen;

    public void SetSpecialLoadTextBehaviour(string key)
    {
        switch (key)
        {
            case "error":
                loadingImage.color = new(255, 0, 0);
                break;
            case "reallyspecial":
                loadingImage.sprite = specialLoadingScreen;
                break;
            default:
                break;
        }
    }
}
