using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsScript : MonoBehaviour
{
    public Slider bgmSlider;
    public Slider sfxSlider;

    void Start()
    {
        if (bgmSlider != null)
        {
            float bgm = PlayerPrefs.GetFloat("BGMVolume", 1f);
            bgmSlider.value = bgm;
            AudioManager.Instance.SetBGMVolume(bgm);
            bgmSlider.onValueChanged.AddListener(val => AudioManager.Instance.SetBGMVolume(val));
        }

        if (sfxSlider != null)
        {
            float sfx = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxSlider.value = sfx;
            AudioManager.Instance.SetSFXVolume(sfx);
            sfxSlider.onValueChanged.AddListener(val => AudioManager.Instance.SetSFXVolume(val));
        }
    }
}
