using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class SelectionMenuUIControls : MonoBehaviour
{
    //ei oo vielä täyellinen mut täl nyt mennää ig
    private readonly Dictionary<string, string> buttonListings = new()
    {
        { "Keyboard", "WASD/Arrows: Move | Enter: Confirm/Next | Esc: Back | Q, E: Change car type" },
        { "Controller", "Left Stick: Move | X/A: Confirm/Next | O/B: Back | LB, RB: Change car type" },
        { "Wheel", "D-Pad: Move | X/A: Confirm/Next | O/B: Back | L3, R3: Change car type" }
    };
    private TMP_Text text;
    void Awake()
    {
        text = GetComponent<TMP_Text>();
        CheckInputDevice();
        InputSystem.onEvent.Call((_) =>
        {
            var device = InputSystem.GetDeviceById(_.deviceId);
            if ((device is Keyboard || device is Mouse) && buttonListings.TryGetValue("Keyboard", out string keyboardString)) text.text = keyboardString;
            else if (device is Gamepad && buttonListings.TryGetValue("Controller", out string controllerString)) text.text = controllerString;
            else if (buttonListings.TryGetValue("Wheel", out string wheelString)) text.text = wheelString;
        });
    }
    private void CheckInputDevice()
    {
        if (Gamepad.current == null)
        {
            buttonListings.TryGetValue("Keyboard", out string keyboardString);
            text.text = keyboardString;
            Debug.Log("no controller");
            return;
        }
        buttonListings.TryGetValue("Controller", out string controllerString);
        text.text = controllerString;
        Debug.Log("controller");
        //lisään wheelin tälle jutulle myöhemmi
    }
}