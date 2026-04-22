using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil.Cil;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[RequireComponent(typeof(PlayerCarController))]
public class InputRecorder : MonoBehaviour
{
    private InputEventTrace inputTrace;
    [SerializeField] private HashSet<InputAction> recordedInputs = new();
    private string dataDirPath;

    void Awake()
    {
        dataDirPath = Application.persistentDataPath + "/Replays";
        enabled = false;
    }

    void OnEnable()
    {
        inputTrace = new()
        {
            onFilterEvent = (ptr, device) => FilterInput(ptr, device)
        };
        inputTrace.Enable();
        Debug.Log("data path: " + dataDirPath);
    }

    void OnDisable()
    {
        inputTrace.Disable();
        inputTrace.Dispose();
    }

    void OnDestroy()
    {
        inputTrace.Disable();
        inputTrace.Dispose();
    }

    bool FilterInput(InputEventPtr inputEventPtr, InputDevice inputDevice)
    {
        return true;//inputDevice is not Mouse;
    }

    [ContextMenu("Get types")]
    void GetTypes()
    {
        AbstractAction.GetTypes();
    }

    [ContextMenu("Toggle trace")]
    void ToggleTrace()
    {
        if (inputTrace.enabled) inputTrace.Disable();
        else inputTrace.Enable();
        Debug.Log("trace " + (inputTrace.enabled ? "enabled" : "disabled"));
    }

    [ContextMenu("replay trace")]
    void Replay()
    {
        inputTrace.Replay().PlayAllEventsAccordingToTimestamps();
    }

    [ContextMenu("Log trace")]
    void LogTrace()
    {
        foreach (var a in inputTrace)
        {
            Debug.Log(a.ToString());
        }
    }

    [ContextMenu("Write Trace")]
    void WriteTrace()
    {
        inputTrace.WriteTo(dataDirPath + "/replay.txt");
    }

    [ContextMenu("Read trace")] 
    void ReadTrace()
    {
        InputEventTrace trace = new();
        trace.ReadFrom(dataDirPath + "/replay.txt");
        trace.Replay().PlayAllEventsAccordingToTimestamps();
    }
}