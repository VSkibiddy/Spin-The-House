using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// Pause menu with Resume / How to Play / Quit to menu.
/// Opens with Esc or the on-screen pause button. Shows How to Play automatically on the very first game.
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public GameObject helpPanel;       // the How to Play screen
    public GameObject gameOverPanel;
    public bool showHelpOnFirstPlay = true;

    bool paused;

    void Start()
    {
        pausePanel.SetActive(false);
        if (helpPanel) helpPanel.SetActive(false);

        // First game ever: open the rules once
        if (showHelpOnFirstPlay && helpPanel && PlayerPrefs.GetInt("SeenHelp", 0) == 0)
        {
            PlayerPrefs.SetInt("SeenHelp", 1);
            PlayerPrefs.Save();
            Pause();
            ShowHelp();
        }
    }

    void Update()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (helpPanel && helpPanel.activeSelf) HideHelp();   // Esc on the rules goes back to the pause menu
        else if (paused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;
        paused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        paused = false;
        if (helpPanel) helpPanel.SetActive(false);
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ShowHelp()
    {
        if (!helpPanel) return;
        helpPanel.SetActive(true);
        pausePanel.SetActive(false);
    }

    public void HideHelp()
    {
        if (!helpPanel) return;
        helpPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void QuitToMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
}