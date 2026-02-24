using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    public GameObject Optionspanel;
    public GameObject[] pauseMenuObjects;
    public GameObject playButton; // Assign in inspector, or find by name

    private bool optionsOpen => Optionspanel != null && Optionspanel.activeSelf;
    private CarInputActions Controls;
    public RacerScript racerScript;
    public GameObject fullMenu;

    void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        Controls.CarControls.pausemenu.performed += PauseMenuCheck;

        if (pauseMenuObjects == null || pauseMenuObjects.Length == 0)
            Debug.LogWarning("PauseMenuObjects array is not assigned or empty.");
        if (Optionspanel == null)
            Debug.LogWarning("Optionspanel is not assigned.");

        fullMenu = GameObject.Find("menuCanvas");
    }

    private void OnEnable() => Controls.Enable();
    private void OnDisable()
    {
        Controls.CarControls.pausemenu.performed -= PauseMenuCheck;
        Controls.Disable();
    }
    private void OnDestroy() => Controls.Disable();

    void Start()
    {
        fullMenu.SetActive(false);
        Optionspanel.SetActive(false);
        racerScript = FindFirstObjectByType<RacerScript>();
    }

    void PauseMenuCheck(InputAction.CallbackContext context)
    {
        if (!optionsOpen && !racerScript.raceFinished && racerScript.racestarted)
        {
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        if (pauseMenuObjects == null || pauseMenuObjects.Length == 0) return;

        LeanTween.cancel(pauseMenuObjects[0]);
        bool isActive = pauseMenuObjects[0].activeSelf;

        SetPausedState(!isActive);
        foreach (GameObject obj in pauseMenuObjects)
        {
            obj.SetActive(!isActive);
        }
        if (!isActive)
        {
            pauseMenuObjects[0].transform.localPosition = new Vector3(0.0f, 470.0f, 0.0f);
            LeanTween.moveLocalY(pauseMenuObjects[0], 0.0f, 0.4f).setEaseInOutCirc().setIgnoreTimeScale(true);
            
            // Select the Play button for UI navigation
            SelectPlayButton();
        }
    }

    private void SelectPlayButton()
    {
        // If not assigned in inspector, try to find it
        if (playButton == null)
        {
            playButton = FindPlay(pauseMenuObjects[0].transform, "Play")?.gameObject;
        }

        if (playButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(playButton);
        }
    }

    private Transform FindPlay(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform result = FindPlay(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    //ERITTÄIN ronnyinen funktio tääl näi
    //ja nyt se on kaks kertaa ronnyisempi
    public void SetPausedState(bool paused)
    {
        Time.timeScale = paused ? 0 : 1;
        GameManager.instance.isPaused = paused;

        musicControl musicCtrl = FindFirstObjectByType<musicControl>();
        if (musicCtrl != null) musicCtrl.PausedMusicHandler();
        soundFXControl soundFXctrl = FindFirstObjectByType<soundFXControl>();
        if (soundFXctrl != null && racerScript.racestarted) soundFXctrl.PauseStateHandler();
    }

    public void ContinueGame()
    {
        foreach (GameObject obj in pauseMenuObjects)
        {
            obj.SetActive(false);
        }
        SetPausedState(false);
    }
    public void QuitGame()
    {
        SetPausedState(false);
        SceneManager.LoadSceneAsync("MainMenu");
    }
    public void RestartGame()
    {
        SetPausedState(false);
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }
}
