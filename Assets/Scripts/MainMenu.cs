using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public TMP_Text bestText;

    void Start()
    {
        Time.timeScale = 1f;
        if (bestText) bestText.text = "Best deadline: " + PlayerPrefs.GetInt("BestDeadline", 0);
    }

    public void Play() => SceneManager.LoadScene("Game");

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
