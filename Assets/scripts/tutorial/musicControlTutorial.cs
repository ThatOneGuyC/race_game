using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class musicControlTutorial : MonoBehaviour
{
    CarInputActions Controls;
    public List<AudioSource> musicListSources;
    //and here we see the reality of the situation
    //that i have dragged myself into :)
    public List<AudioSource> musicListCopies;
    public AudioSource mainTrack;
    public AudioSource driftTrack;
    public AudioSource turboTrack;
    public AudioSource[] variants;

    private enum CarMusicState {Main, Drift, Turbo};
    private CarMusicState CurrentMusState = CarMusicState.Main;
    private CarMusicState LatestMusState = CarMusicState.Main;
    public int[] activeTweenIDs;

    private PlayerCarController carController;

    void OnEnable()
    {
        Controls = new CarInputActions();
        Controls.Enable();

        carController = FindAnyObjectByType<PlayerCarController>();
    }
    private void OnDisable()
    {
        LeanTween.cancelAll();
        Controls.CarControls.Drift.performed -= DriftCall;
        Controls.CarControls.Drift.canceled -= DriftCanceled;
        Controls.CarControls.turbo.performed -= TurboCall;
        Controls.CarControls.turbo.canceled -= TurboCanceled;
        Controls.Disable();
    }
    private void OnDestroy() => Controls.Disable();

    public void EnableDriftFunctions()
    {
        Controls.CarControls.Drift.performed += DriftCall;
        Controls.CarControls.Drift.canceled += DriftCanceled;
    }
    public void EnableTurboFunctions()
    {
        Controls.CarControls.turbo.performed += TurboCall;
        Controls.CarControls.turbo.canceled += TurboCanceled;
    }

    //kaikki tarpeelline on täs
    void DriftCall(InputAction.CallbackContext context)
    {
        CurrentMusState = carController.IsTurboActive ? CarMusicState.Turbo : CarMusicState.Drift;
        FadeLayerTracks();
    }
    void DriftCanceled(InputAction.CallbackContext context)
    {
        CurrentMusState = carController.IsTurboActive ? CarMusicState.Turbo : CarMusicState.Main;
        FadeLayerTracks();
    }
    void TurboCall(InputAction.CallbackContext context)
    {
        CurrentMusState = CarMusicState.Turbo;
        FadeLayerTracks();
    }
    void TurboCanceled(InputAction.CallbackContext context)
    {
        CurrentMusState = carController.IsDrifting ? CarMusicState.Drift : CarMusicState.Main;
        FadeLayerTracks();
    }



    void Start()
    {
        musicListSources = gameObject.GetComponentsInChildren<AudioSource>()
        .OrderBy(a => a.name)
        .ToList();
        musicListCopies = new List<AudioSource>(musicListSources);
        foreach(AudioSource track in musicListSources)
            Debug.Log(track);
        SetTrackVariants();

        //debug
        //carController.canDrift = true;
        //carController.canUseTurbo = true;
        //StartNonIntroTracks(); //begin all non-intro tracks?
        //MusicSections("2_FINAL_TUTORIAL_main"); //set track to something else?
        //EnableDriftFunctions(); //check for layers on drift?
        //EnableTurboFunctions(); //check for layers on turbo?
        //end debug
        
        //the TRUE and EVEN MORE PAINFUL death of TrackedTween
        activeTweenIDs = new int[musicListCopies.Count];
    }

    public void StartNonIntroTracks()
    {
        foreach (AudioSource musicTrack in musicListSources)
        {
            if (!musicTrack.isPlaying)
            {
                musicTrack.Play();
            }
        }
    }
    public void StopNonIntroTracks()
    {
        foreach (AudioSource musicTrack in musicListSources)
        {
            musicTrack.Stop();
        }
    }
    void SetTrackVariants()
    {
        string clipName = mainTrack.clip.name;

        // Get the prefix (e.g. first two characters)
        string prefix = clipName.Substring(0, 1);

        //updated the fucker jotta se käyttää suoraan soossia eikä gameobjectei
        variants = musicListSources
            .Select(go => go)
            .Where(a => a.name.StartsWith(prefix))
            .OrderBy(a => a.name)
            .ToArray();

        mainTrack = variants.Length > 0 ? variants[0] : null;
        driftTrack = variants.Length > 1 ? variants[1] : null;
        turboTrack = variants.Length > 2 ? variants[2] : null;
    }

    void ChangeTrack(string selectedAudio)
    {
        mainTrack = GameObject.Find(selectedAudio).GetComponent<AudioSource>();
        //ottaa main trackin mukaan, eli ei tartte laittaa mukaan variablee
        //ei myöskään alota mitään biisiä itse, sitä varten on MusicSections()
        SetTrackVariants();
    }

    /// <summary>
    /// vaihtaa musiikkiraidat trackNamen mukaan
    /// </summary>
    /// <param name="trackName">koko tiedostonimi, ilman .wav päätettä</param>
    public void MusicSections(string trackName, string mode = "instant") //lisään myöhemmi oikeet fade outit ja transitionit
    {
        float volSet = 0.28f;

        switch (mode)
        {
            case "instant":
                mainTrack.Stop();
                if (driftTrack != null)
                    driftTrack.Stop();
                if (turboTrack != null)
                    turboTrack.Stop();

                ChangeTrack(trackName);
                mainTrack.volume = volSet;

                mainTrack.Play();
                if (driftTrack != null)
                    driftTrack.Play();
                if (turboTrack != null)
                    turboTrack.Play();
                break;
            
            case "fade":
                FadeSections(trackName);
                break;
        }
    }

    private void FadeLayerTracks()
    {
        if (CurrentMusState == LatestMusState) return;

        int stateIndex = (int)CurrentMusState;
        int previousStateIndex = (int)LatestMusState;
        AudioSource NextTrack = (stateIndex < variants.Length) ? variants[stateIndex] : null;
        AudioSource PreviousTrack = (previousStateIndex < variants.Length) ? variants[previousStateIndex] : null;

        if (NextTrack != null)
        {
            int nextIdx = musicListCopies.IndexOf(NextTrack);
            int prevIdx = musicListCopies.IndexOf(PreviousTrack);

            // Cancel any existing tweens on these tracks
            if (nextIdx != -1 && activeTweenIDs[nextIdx] != 0)
                LeanTween.cancel(activeTweenIDs[nextIdx]);
            if (prevIdx != -1 && activeTweenIDs[prevIdx] != 0)
                LeanTween.cancel(activeTweenIDs[prevIdx]);

            // Start new tweens and store their IDs
            if (nextIdx != -1)
                activeTweenIDs[nextIdx] =
                    LeanTween.value(NextTrack.volume, 0.28f, 1.0f)
                        .setOnUpdate(val => NextTrack.volume = val)
                        .id;
            if (prevIdx != -1)
                activeTweenIDs[prevIdx] =
                    LeanTween.value(PreviousTrack.volume, 0.0f, 1.0f)
                        .setOnUpdate(val => PreviousTrack.volume = val)
                        .id;

            LatestMusState = CurrentMusState;
        }
        else
        {
            Debug.LogError("yeah so there's not more tracks for this album buddy");
        }
    }

    private void RemovePreviousTrack(AudioSource previousTrack)
    {
        if (previousTrack != null)
        {
            int trackIndex = musicListCopies.IndexOf(previousTrack);
            if (trackIndex != -1)
            {
                if (activeTweenIDs[trackIndex] != 0)
                    LeanTween.cancel(activeTweenIDs[trackIndex]);

                // Tween volume to 0
                LeanTween.value(previousTrack.volume, 0.0f, 0.6f)
                .setOnUpdate(val => previousTrack.volume = val);

                // Remove from list to prevent future tweens
                musicListSources.Remove(previousTrack);
                // Optionally, also remove the corresponding tween ID if you want to keep arrays in sync:
                // activeTweenIDs = musicListSources.Select((_, i) => i < activeTweenIDs.Length ? activeTweenIDs[i] : -1).ToArray();
            }
        }
    }

    private void FadeSections(string newTrackName)
    {
        //ota nykyset, assignaa ne vanhoiksi

        AudioSource previousMain = mainTrack;
        AudioSource previousDrift = driftTrack;
        AudioSource previousTurbo = turboTrack;

        RemovePreviousTrack(mainTrack);
        RemovePreviousTrack(driftTrack);
        RemovePreviousTrack(turboTrack);
        
        //vaiha uuet nykyset tilalle
        ChangeTrack(newTrackName);

        LeanTween.value(mainTrack.volume, 0.28f, 0.6f)
            .setOnUpdate(val => mainTrack.volume = val);
    }

    public void PausedMusicHandler()
    {
        bool isPaused = GameManager.instance.isPaused;
        foreach (AudioSource musicTrack in musicListSources)
        {
            if (isPaused)
                musicTrack.Pause();
            else
                musicTrack.UnPause();
        }
    }

    //jotta instructionCheck.cs ei callaa tätä scriptii 5 kertaa
    public void BeginDriftSection()
    {
        mainTrack.volume = 0f;
        StopNonIntroTracks();
        ChangeTrack("6_FINAL_TUTORIAL_1main");
        driftTrack.volume = 0.28f;
        StartNonIntroTracks();
    }
}