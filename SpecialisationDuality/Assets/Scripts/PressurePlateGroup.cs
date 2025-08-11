using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PressurePlateGroup : MonoBehaviour
{
    [Header("Door + UI")]
    public Animator doorAnimator;
    public TMP_Text messageText;
    public CanvasGroup messageGroup;

    [Header("Plate Settings")]
    public int totalPlates = 2;

    [Header("UI Timing")]
    public float fadeDuration = 0.5f;
    public float displayDuration = 2f;

    private int activatedPlates = 0;
    private Coroutine fadeRoutine;
    private bool isOpen = false;



    public void Update()
    {
        print(activatedPlates);
    }

    public void PlatePressed()
    {
        activatedPlates++;
        CheckPlates();
    }

    public void PlateReleased()
    {
        activatedPlates--;
        CheckPlates();
    }

    void CheckPlates()
    {
        if (activatedPlates >= totalPlates && !isOpen)
        {
            AudioManager.Instance.PlaySFX("Gate");
            doorAnimator.SetTrigger("Open");
            isOpen = true;

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeMessage("The door has opened!"));
        }
        else if (activatedPlates < totalPlates && isOpen)
        {
            doorAnimator.SetTrigger("Close");
            isOpen = false;
        }
    }


    IEnumerator FadeMessage(string message)
    {
        messageText.text = message;
        yield return StartCoroutine(FadeCanvasGroup(messageGroup, 0f, 1f, fadeDuration));
        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(FadeCanvasGroup(messageGroup, 1f, 0f, fadeDuration));
        messageText.text = "";
        fadeRoutine = null;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            group.alpha = Mathf.Lerp(from, to, elapsed / time);
            elapsed += Time.deltaTime;
            yield return null;
        }
        group.alpha = to;
    }
}
