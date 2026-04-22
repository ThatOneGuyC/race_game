using System.Linq;
using UnityEditor;
using UnityEngine;

public class musicControl : MonoBehaviour
{
    //ois pitäny olla private jo alusta alkaen...
    private GameObject[] musicObjects;
    private AudioSource[] musicTracks;
    
    private enum CarMusicState {Main, Drift, Turbo};
    private CarMusicState CurrentMusState = CarMusicState.Main;
    private CarMusicState LatestMusState = CarMusicState.Main;
    private int[] activeTweenIDs;

    //uniikkeja yksittäisiä biisejä, siksi en laita näille tageja tai arrayta.
    //vectoraman demossa se tulee valittemaan kolmen eri biisin välillä
    //ja se vaatii sit enemmän paskaa
    public AudioSource resultsTrack;
    public AudioSource finalLapTrack;

    private PlayerCarController carController;
    CarInputActions Controls;

    void Awake()
    {
        Controls = new CarInputActions();
        carController = FindAnyObjectByType<PlayerCarController>();

        Controls.CarControls.Drift.started += ctx => DriftCall();
        Controls.CarControls.Drift.canceled += ctx => DriftCanceled();
        Controls.CarControls.turbo.started += ctx => TurboCall();
        Controls.CarControls.turbo.canceled += ctx => TurboCanceled();
    }
    private void OnEnable() => Controls.Enable();
    private void OnDisable() => DisableEventsAndControls();
    private void OnDestroy() => DisableEventsAndControls();
    private void DisableEventsAndControls()
    {
        LeanTween.cancelAll();
        Controls.CarControls.Drift.started -= ctx => DriftCall();
        Controls.CarControls.Drift.canceled -= ctx => DriftCanceled();
        Controls.CarControls.turbo.started -= ctx => TurboCall();
        Controls.CarControls.turbo.canceled -= ctx => TurboCanceled();
        Controls.Disable();
    }

    void Start()
    {
        //Ouchies! Double ouchies! Triple? Yes!
        musicObjects = GameObject.FindGameObjectsWithTag("thisisasound");
        musicObjects = musicObjects.OrderBy(go => go.name).ToArray();
        musicTracks = musicObjects.Select(go => go.GetComponent<AudioSource>()).ToArray();
        //the death of TrackedTween
        activeTweenIDs = new int[musicTracks.Length];
    }

    //kaikki tarpeelline on täs
    void DriftCall()
    {
        CurrentMusState = carController.isTurboActive ? CarMusicState.Turbo : CarMusicState.Drift;
        FadeTracks();
    }
    void DriftCanceled()
    {
        CurrentMusState = carController.isTurboActive ? CarMusicState.Turbo : CarMusicState.Main;
        FadeTracks();
    }
    void TurboCall()
    {
        CurrentMusState = CarMusicState.Turbo;
        FadeTracks();
    }
    void TurboCanceled()
    {
        CurrentMusState = carController.IsDrifting ? CarMusicState.Drift : CarMusicState.Main;
        FadeTracks();
    }

    private void FadeTracks()
    {
        //tarkistaa staten ku funktio alkaa, ei tarvi muualla
        if (CurrentMusState == LatestMusState) return;

        int stateIndex = (int)CurrentMusState; //current on oikeesti se viimeisin lol
        int previousStateIndex = (int)LatestMusState;
        AudioSource NextTrack = musicTracks[stateIndex];
        AudioSource PreviousTrack = musicTracks[previousStateIndex];

        LeanTween.cancel(activeTweenIDs[stateIndex]);
        LeanTween.cancel(activeTweenIDs[previousStateIndex]);
        activeTweenIDs[stateIndex] = LeanTween.value(NextTrack.volume, 0.3f, 0.7f).setOnUpdate(val => NextTrack.volume = val).id;
        activeTweenIDs[previousStateIndex] = LeanTween.value(PreviousTrack.volume, 0.0f, 0.7f).setOnUpdate(val => PreviousTrack.volume = val).id;
        LatestMusState = CurrentMusState;

        //ihan VITUN PASKANEN hackki, joka tarkistaa että onko lopullinen music state ees oikea
        //jostain syystä IsTurboActive ei halua toimia samalla framella music staten kanssa, toisin kuin drift...
        if (CurrentMusState == CarMusicState.Turbo && !Controls.CarControls.turbo.IsPressed())
        {
            if (carController.IsDrifting) CurrentMusState = CarMusicState.Drift;
            else CurrentMusState = CarMusicState.Main;
            FadeTracks();
        }
    }

    public void StartMusicTracks()
    {
        foreach (AudioSource track in musicTracks) track.Play();
    }
    //when
    public void StartFinalLapTrack()
    {
        Debug.Log("jos näitä logeja on enemmän ku yks jokin on paskana.");
        Controls.CarControls.Drift.started -= ctx => DriftCall();
        Controls.CarControls.Drift.canceled -= ctx => DriftCanceled();
        Controls.CarControls.turbo.started -= ctx => TurboCall();
        Controls.CarControls.turbo.canceled -= ctx => TurboCanceled();
        StopMusicTracks();
        finalLapTrack.Play();
    }
    //when 2
    public void StopMusicTracks(bool endRaceEvent = false, bool stopFinalLap = false)
    {
        foreach (AudioSource track in musicTracks) track.Stop();

        if (endRaceEvent || stopFinalLap && finalLapTrack != null) finalLapTrack.Stop();
    }

    public void PausedMusicHandler()
    {
        bool isPaused = GameManager.instance.isPaused;
        foreach (AudioSource track in musicTracks)
        {
            if (isPaused) track.Pause();
            else track.UnPause();
        }
    }
}