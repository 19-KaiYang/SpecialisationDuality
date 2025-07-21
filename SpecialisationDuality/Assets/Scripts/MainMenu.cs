using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    private void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void onStartButtonClicked()
    {
        SceneManager.LoadScene("MainGameScene");
    }

    public void onQuitButtonClicked()
    {
        Application.Quit();
    }

    public void onMainMenuClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
