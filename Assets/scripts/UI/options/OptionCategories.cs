using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptionCategories : MonoBehaviour
{
    private List<Transform> CategoryContents;
    private List<Button> CategoryButtonList;
    private GameObject currentlySelected => EventSystem.current.currentSelectedGameObject;
    private int index = 0;
    private Transform currentCategory => CategoryContents[index];
    CarInputActions Controls;

    void Awake()
    {
        //TODO: joku parempi tapa tälle sillä tää on täynnä conditioneita ja paskaa
        CategoryContents = GetComponentsInChildren<Transform>().OrderBy(a => a.name[^1]).Where(i => char.IsDigit(i.name[^1])).ToList();
        CategoryButtonList = GetComponentsInChildren<Button>().OrderBy(a => a.name[^1]).ToList();
        foreach (var a in CategoryContents) if (a != currentCategory) a.gameObject.SetActive(false);
        Controls = new CarInputActions();
    }
    void OnEnable()
    {
        Controls.Enable();
        Controls.CarControls.carskinright.performed += ctx => ChangeCategoryManual(true);
        Controls.CarControls.carskinleft.performed += ctx => ChangeCategoryManual(false);
    }
    void OnDestroy()
    {
        Controls.Disable();
        Controls.CarControls.carskinright.performed -= ctx => ChangeCategoryManual(true);
        Controls.CarControls.carskinleft.performed -= ctx => ChangeCategoryManual(false);
    }
    //TODO: nvm sitä ei tarvi korjaa mut tästä voi silti tehä paremman
    private void ChangeCategoryManual(bool change)
    {
        if (index > CategoryButtonList.Count - 1 || index < 0) return;
        try
        {
            if (change) CategoryButtonList[index + 1].Select();
            else CategoryButtonList[index - 1].Select();
        }
        catch
        {
            Debug.Log("You prehistoric pancake, you pigeon pistachio, you absconded acolyte, you corrupted crisis. You think you can index me, you don't know that I invented indexing");
        }
    }

    public void ChangeCategory()
    {
        int previousButtonIndex = index;
        int currentButtonIndex = CategoryButtonList.IndexOf(currentlySelected.GetComponent<Button>());
        if (previousButtonIndex == currentButtonIndex) return;
        index = previousButtonIndex > currentButtonIndex ? index -= 1 : index += 1 ;
        //Debug.Log($"index: {index}, prev: {previousButtonIndex}, cur: {currentButtonIndex}");

        CategoryContents[previousButtonIndex].gameObject.SetActive(false);
        CategoryContents[currentButtonIndex].gameObject.SetActive(true);
    }

    //TODO: parempi tapa ylös liikkumisen tarkistamiseen (OnMove callback todennäkösesti)
    public void SelectNearestOption()
    {
        if (Controls.CarControls.Move.ReadValue<Vector2>().y <= 0f) return;

        Selectable nearestOption = currentCategory.GetChild(currentCategory.childCount - 1).GetComponentInChildren<Selectable>();
        nearestOption.Select();
    }
}