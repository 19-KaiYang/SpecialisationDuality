using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [System.Serializable]
    public class SoundEntry
    {
        public string name;
        public AudioClip clip;
    }

    public List<SoundEntry> sfxClips;

    private Dictionary<string, AudioClip> sfxDict = new();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolume();
            InitDictionaries();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void LoadVolume()
    {
        bgmSource.volume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        sfxSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    void InitDictionaries()
    {
        foreach (var sfx in sfxClips)
            sfxDict[sfx.name] = sfx.clip;
    }

    public void SetBGMVolume(float vol)
    {
        bgmSource.volume = vol;
        PlayerPrefs.SetFloat("BGMVolume", vol);
    }

    public void SetSFXVolume(float vol)
    {
        sfxSource.volume = vol;
        PlayerPrefs.SetFloat("SFXVolume", vol);
    }

    public void PlaySFX(string name)
    {
        if (sfxDict.TryGetValue(name, out var clip))
            sfxSource.PlayOneShot(clip);
    }

 
}
