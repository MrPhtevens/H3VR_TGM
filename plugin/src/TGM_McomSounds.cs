using System;
using System.Collections;
using System.IO;
using BepInEx;
using UnityEngine;

namespace TeamsGameMode;

/// <summary>
/// Optional Mcom sounds supplied by the player, loaded from BepInEx/config/TeamsGameMode/McomSounds.
/// arming = loops while being armed / disarmed, armed = loops until it blows, explosion = plays once.
/// Each can be .wav or .ogg, missing files fall back to the built in sounds.
/// </summary>
public static class TGM_McomSounds
{
    public static AudioClip arming;
    public static AudioClip armed;
    public static AudioClip explosion;

    public static string Folder => Path.Combine(Path.Combine(Paths.ConfigPath, "TeamsGameMode"), "McomSounds");

    public static IEnumerator Load()
    {
        try
        {
            Directory.CreateDirectory(Folder);
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogWarning("Mcom sounds: could not create " + Folder + ": " + e.Message);
            yield break;
        }

        yield return TGMPlugin.instance.StartCoroutine(LoadClip("arming", c => arming = c));
        yield return TGMPlugin.instance.StartCoroutine(LoadClip("armed", c => armed = c));
        yield return TGMPlugin.instance.StartCoroutine(LoadClip("explosion", c => explosion = c));
    }

    static IEnumerator LoadClip(string name, Action<AudioClip> result)
    {
        string path = null;
        AudioType type = AudioType.WAV;
        if (File.Exists(Path.Combine(Folder, name + ".wav")))
            path = Path.Combine(Folder, name + ".wav");
        else if (File.Exists(Path.Combine(Folder, name + ".ogg")))
        {
            path = Path.Combine(Folder, name + ".ogg");
            type = AudioType.OGGVORBIS;
        }

        if (path == null)
        {
            TGMPlugin.Logger.LogMessage("Mcom sounds: no " + name + ".wav/.ogg, using built in sound");
            yield break;
        }

        WWW www = new WWW(new Uri(path).AbsoluteUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            TGMPlugin.Logger.LogWarning("Mcom sounds: failed to load " + path + ": " + www.error);
            yield break;
        }

        AudioClip clip = www.GetAudioClip(true, false, type);
        while (clip != null && clip.loadState == AudioDataLoadState.Loading)
            yield return null;

        if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
        {
            TGMPlugin.Logger.LogWarning("Mcom sounds: " + path + " is not a supported audio file");
            yield break;
        }

        clip.name = "TGM_Mcom_" + name;
        TGMPlugin.Logger.LogMessage("Mcom sounds: loaded " + path);
        result(clip);
    }
}
