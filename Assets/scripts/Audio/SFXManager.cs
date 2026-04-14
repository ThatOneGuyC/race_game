using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SFXManager : MonoBehaviour
{
    //säilyttää KAIKKI äänet, PAITSI ne, joita käytetään interactableiden kanssa
    [SerializeField] private List<AudioSource> soundList;
    [SerializeField] protected List<AudioSource> interactableSounds;
    [SerializeField] private List<GameObject> interactables;
    [SerializeField] private AudioSource gamePaused;
    [SerializeField] private AudioSource pausedTrack;
    [SerializeField] private AudioSource carLightsToggle;
    public AudioSource nextLap;
    public AudioSource raceFinished;

    [ContextMenu("Assign SFX")]
    void FindSounds()
    {
        interactableSounds = GetComponentsInChildren<AudioSource>().Where(a => a.CompareTag("soundFXonClick")).OrderBy(a => a.name).ToList();
    }
    [ContextMenu("Assign interactables")]
    void FindInteractables()
    {
        //HUOM!!! context menun tekemät muutokset EI AIHEUTA muutoksia tiedostoon; laita esim. joku gameobject päälle ja pois ja sitten tallenna nii se tallentaa lmao
        interactables = GameObject.FindGameObjectsWithTag("SFXInteractable").ToList();
    }

    void Awake()
    {
        soundList = GetComponentsInChildren<AudioSource>().Where(a => a.CompareTag("soundFX")).ToList();
        foreach (var i in interactables)
        {
            Selectable tester = GetInteractableComponent(i);
            
            if (tester is Button button) button.onClick.AddListener(() => { interactableSounds[0].Play(); });
            else if (tester is Toggle toggle) toggle.onValueChanged.AddListener((value) => { interactableSounds[1].Play(); });
            else if (tester is Slider slider) slider.onValueChanged.AddListener((value) => { interactableSounds[2].Play(); });
            else if (tester is TMP_Dropdown dropdown)
            {
                DropdownOpenSFX openSFX = dropdown.gameObject.AddComponent<DropdownOpenSFX>();
                openSFX.dropdownOpen = interactableSounds[3];
                //TODO: dropdown SFX jollai muulla event paskalla
            }
        }
        CarColors c = FindFirstObjectByType<CarColors>();
        if (c != null) c.lights = carLightsToggle;
    }

    //TODO: muuttaa hiukan paremmaks, mutta tarpeeks hyvä atm
    //TryGetComponent suoraan objektissa; jos ei löydä, ettii seuraavasta sisäsestä objektista.
    //voi aiheuttaa hieman paskasta toimintaa mutta tbf emme ole idiootteja

    /// <param name="obj"></param>
    /// <returns>Selectable component (e.g. Button, Toggle, Slider or Dropdown)</returns>
    /// <exception cref="NullReferenceException"></exception>
    private Selectable GetInteractableComponent(GameObject obj)
    {
        if (obj.TryGetComponent(out Button button)) return button;
        else if (obj.TryGetComponent(out Toggle toggle)) return toggle;
        else if (obj.TryGetComponent(out Slider slider)) return slider;
        else if (obj.TryGetComponent(out TMP_Dropdown dropdown)) return dropdown;

        List<Transform> objList = new(obj.GetComponentsInChildren<Transform>());

        foreach (var i in objList)
        {
            if (i.gameObject.TryGetComponent(out Button buttonFromObj)) return buttonFromObj;
            else if (i.gameObject.TryGetComponent(out Toggle toggleFromObj)) return toggleFromObj;
            else if (i.gameObject.TryGetComponent(out Slider sliderFromObj)) return sliderFromObj;
            else if (i.gameObject.TryGetComponent(out TMP_Dropdown dropdownFromObj)) return dropdownFromObj;
        }

        throw new NullReferenceException($"no Selectable component found on {obj} or its hierarchy!");
    }

    public void PauseStateHandler()
    {
        bool isPaused = GameManager.instance.isPaused;

        pausedTrack.volume = isPaused ? 0.72f : 0f;
        foreach (AudioSource sound in soundList)
        {
            if (isPaused) sound.Pause();
            else sound.UnPause();
        }
        if (!isPaused) return;
        gamePaused.Play();
    }
}