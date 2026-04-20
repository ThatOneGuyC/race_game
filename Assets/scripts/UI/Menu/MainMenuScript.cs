using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject fullMenu;
    [SerializeField] private AudioSource menuMusic;
    private int musictweenIDstart = -1;
    private int musictweenIDend = -1;

    [SerializeField] private GameObject playConfirmPanel;

    void Awake()
    {
        Time.timeScale = 1;
    }
    void Start()
    {
        if (playConfirmPanel != null) playConfirmPanel.SetActive(false);

        LeanTween.moveLocalY(fullMenu, 0.0f, 1.5f).setEase(LeanTweenType.easeOutBounce).setOnStart(() => { menuMusic.Play(); });
    }

    public void MainMenuMusic(bool active)
    {
        //jotta ei oo pelkästää truena tai falsena olemassa
        switch (active)
        {
            case true:
                if (musictweenIDend != -1) LeanTween.cancel(musictweenIDend);

                musictweenIDstart = LeanTween.value(menuMusic.volume, 0.27f, 0.9f).setOnUpdate(val => menuMusic.volume = val).id;
                break;
            case false:
                if (musictweenIDstart != -1) LeanTween.cancel(musictweenIDstart);

                musictweenIDend = LeanTween.value(menuMusic.volume, 0.0f, 0.9f).setOnUpdate(val => menuMusic.volume = val).id;
                break;
        }
    }

    public void Playgame()
    {
        SceneManager.LoadSceneAsync("SelectionMenu");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}