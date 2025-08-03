using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    public GameObject settingsPanel;
    private bool isPaused = false;

    private void Update()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "MainGameScene")
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                isPaused = !isPaused;
                settingsPanel.SetActive(isPaused);

                if (isPaused)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    Time.timeScale = 0f;
                }
                else
                {
                    StartCoroutine(RehideCursorNextFrame());
                    Time.timeScale = 1f;
                }
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

    }

    public void onStartButtonClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainGameScene");
    }

    public void onQuitButtonClicked()
    {
        Application.Quit();
    }

    public void onMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OnCloseBtnClicked()
    {
        settingsPanel.SetActive(false);
        isPaused = false;
     
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }


    public void OnSettingBtnClicked()
    {
        settingsPanel.SetActive(true);
        isPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }
    IEnumerator RehideCursorNextFrame()
    {
        yield return null; 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

}
