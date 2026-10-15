using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TeamsGameMode;

public class TGM_ModLoader
{
    public static TGM_Assets tgmAssets;
    public static AssetBundle tgmBundle;
    public static bool assetsLoading = false;
    public static bool loadedAssets = false;
    public static float timeout = 0;

    //Loaded Teams
    public static List<TGM_PlayerTeam> playerTeams = new List<TGM_PlayerTeam>();
    public static List<TGM_SosigTeam> sosigTeams = new List<TGM_SosigTeam>();


    public static IEnumerator LoadAssets()
    {
        if (assetsLoading || timeout > Time.time)
            yield break;

        assetsLoading = true;

        string path = Paths.PluginPath + "/Packer-Teams_Game_Mode/teamgamemode.tgm";

        if (!File.Exists(path))
            path = Paths.PluginPath + "/Teams_Game_Mode/teamgamemode.tgm";

        Debug.Log(path);

        if (!loadedAssets)
        {
            AssetBundleCreateRequest asyncBundleRequest
                = AssetBundle.LoadFromFileAsync(path);

            yield return asyncBundleRequest;
            AssetBundle localAssetBundle = asyncBundleRequest.assetBundle;

            if (localAssetBundle == null)
            {
                TGMPlugin.Logger.LogMessage($"Failed to load Teams Game Mode AssetBundle");
                yield break;
            }

            tgmBundle = localAssetBundle;
        }

        AssetBundleRequest assetRequest = tgmBundle.LoadAssetWithSubAssetsAsync<TGM_Assets>("TeamsGameMode");
        yield return assetRequest;

        if (assetRequest == null)
        {
            TGMPlugin.Logger.LogMessage($"Missing TGM Assets");
            yield break;
        }

        tgmAssets = assetRequest.asset as TGM_Assets;
        loadedAssets = true;

        //--------------------------------------------------------------------------------------------------------

        //yield return TGM_Scene.instance.StartCoroutine(LoadInAssets());
        assetsLoading = false;

        timeout = Time.time + 10f;
    }

    //--------------------------------------------------------------------------------------------------------
    // Player Teams
    //--------------------------------------------------------------------------------------------------------

    public static void LoadPlayerTeams()
    {
        List<string> directories = Directory.GetFiles(Paths.PluginPath, "*.pttgm", SearchOption.AllDirectories).ToList();

        if (directories.Count == 0)
        {
            TGMPlugin.Logger.LogMessage($"No Player Teams were found!");
            return;
        }

        //Clear old Player Teams
        playerTeams.Clear();

        //Load up each of our categories
        for (int i = 0; i < directories.Count; i++)
        {
            TGM_PlayerTeam team;

            //Load each Category via the Directory
            using (StreamReader streamReader = new StreamReader(directories[i]))
            {
                string json = streamReader.ReadToEnd();

                try
                {
                    team = JsonUtility.FromJson<TGM_PlayerTeam>(json);
                }
                catch (Exception ex)
                {
                    TGMPlugin.Logger.LogMessage(ex.Message);
                    return;
                }


                //Add to our item category pool
                playerTeams.Add(team);
                string newDirectory = directories[i];
                newDirectory = newDirectory.Replace("pttgm", "png");
                team.thumbnail = LoadSprite(newDirectory);

                //Set Defaults

                //Class Icons
                for (int x = 0; x < team.playerClasses.Length; x++)
                {
                    //string nameFixed = team.playerClasses[x].name.Replace(" ", "_");
                    string pathName = Path.GetDirectoryName(newDirectory);
                    team.playerClasses[x].thumbnail = LoadSprite(pathName + "\\" + team.playerClasses[x].spriteName);
                }

                TGMPlugin.Logger.LogMessage($"Loaded Player Team - " + team.name);
            }
        }
    }

    //--------------------------------------------------------------------------------------------------------
    // Sosig Teams
    //--------------------------------------------------------------------------------------------------------

    public static void LoadSosigTeams()
    {
        List<string> directories = Directory.GetFiles(Paths.PluginPath, "*.sttgm", SearchOption.AllDirectories).ToList();

        if (directories.Count == 0)
        {
            TGMPlugin.Logger.LogMessage($"No Sosig Teams were found!");
            return;
        }

        //Clear Old Sosig Teams
        sosigTeams.Clear();

        //Load up each of our categories
        for (int i = 0; i < directories.Count; i++)
        {
            TGM_SosigTeam team;

            //Load each Category via the Directory
            using (StreamReader streamReader = new StreamReader(directories[i]))
            {
                string json = streamReader.ReadToEnd();

                try
                {
                    team = JsonUtility.FromJson<TGM_SosigTeam>(json);
                }
                catch (Exception ex)
                {
                    TGMPlugin.Logger.LogMessage(ex.Message);
                    return;
                }


                //Add to our item category pool
                sosigTeams.Add(team);
                string newDirectory = directories[i];
                newDirectory = newDirectory.Replace("sttgm", "png");
                team.thumbnail = LoadSprite(newDirectory);

                TGMPlugin.Logger.LogMessage($"Loaded Sosig Team - " + team.name);
            }
        }
    }

    //--------------------------------------------------------------------------------------------------------
    // PROFILES
    //--------------------------------------------------------------------------------------------------------

    public static List<TGM_Profile> profiles = new List<TGM_Profile>();

    public static bool SaveProfile(string saveName)
    {
        if (TGM_Profile.profile == null)
            return false;

        //Error Check
        TGM_Profile.profile.name = CleanFileName(saveName);
        if (TGM_Profile.profile.name == "Profile")
            return false;


        //---------------------------------------------------------------------------------------------------------------------
        // SAVE SETTINGS
        //---------------------------------------------------------------------------------------------------------------------

        TGM_Profile.profile.gamemode = TGM_Manager.instance.gamemode.name;

        //Save Teams
        for (int x = 0; x < TGM_Manager.instance.team.Length; x++)
        {
            TGM_Profile.profile.sosigTeams[x] = sosigTeams[TGM_Manager.instance.team[x].sosigTeam].name;
            TGM_Profile.profile.playerTeams[x] = playerTeams[TGM_Manager.instance.team[x].playerTeam].name;
            TGM_Profile.profile.sosigLimit[x] = TGM_Manager.instance.team[x].sosigLimit;
            TGM_Profile.profile.scoreGoal[x] = TGM_Manager.instance.team[x].scoreGoal;
        }

        //Save Game Settings
        TGM_Profile.profile.gameSettings = new List<int>();
        for (int x = 0; x < TGM_Settings.gameSettings.Count; x++)
        {
            TGM_Profile.profile.gameSettings.Add(TGM_Settings.gameSettings[x].value);
        }

        //Save Gamemode Settings
        TGM_Profile.profile.gamemodeSettings = new List<int>();
        for (int x = 0; x < TGM_Settings.gamemodeSettings.Count; x++)
        {
            TGM_Profile.profile.gamemodeSettings.Add(TGM_Settings.gamemodeSettings[x].value);
        }

        //---------------------------------------------------------------------------------------------------------------------

        bool status = false;
        string path = Paths.PluginPath + "\\Packer-Teams_Game_Mode\\";
        string fileName = path + saveName + ".protgm";

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception ex)
        {
            FistVR.SM.PlayGlobalUISound(FistVR.SM.GlobalUISound.Error, FistVR.GM.CurrentPlayerBody.transform.position);
            TGMPlugin.Logger.LogMessage($"Failed Saving Profile - " + ex.Message);
            return false;
        }

        try
        {
            if (!File.Exists(fileName))
            {
                FileStream newFile = File.Create(fileName);
                //File.SetAttributes(fileName, FileAttributes.Normal);
                newFile.Close();
            }

            TGMPlugin.Logger.LogMessage($"Writing to " + fileName);
            using (StreamWriter writer = new StreamWriter(fileName, false))
            {
                string json = JsonUtility.ToJson(TGM_Profile.profile, true);
                writer.Write(json);
                writer.Close();

            }
            status = true;
        }
        catch (Exception ex)
        {
            FistVR.SM.PlayGlobalUISound(FistVR.SM.GlobalUISound.Error, FistVR.GM.CurrentPlayerBody.transform.position);
            TGMPlugin.Logger.LogError($"Failed Saving Profile - " + ex.Message);
            status = false;
        }

        return status;
    }

    private static string CleanFileName(string fileName)
    {
        return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
    }

    public static List<TGM_Profile> LoadProfiles()
    {
        //Clear old Profiles incase
        profiles.Clear();

        List<string> directories = Directory.GetFiles(Paths.PluginPath, "*.protgm", SearchOption.AllDirectories).ToList();

        if (directories.Count == 0)
        {
            TGMPlugin.Logger.LogMessage($"No profiles found");
            return null;
        }

        //Load up each of our categories
        for (int i = 0; i < directories.Count; i++)
        {
            try
            {
                TGM_Profile newProfile;
                //Load each Category via the Directory
                using (StreamReader streamReader = new StreamReader(directories[i]))
                {
                    string json = streamReader.ReadToEnd();

                    newProfile = JsonUtility.FromJson<TGM_Profile>(json);

                    //Add to our item category pool
                    if (newProfile != null)
                    {
                        profiles.Add(newProfile);
                        TGMPlugin.Logger.LogMessage($"Loaded External Profile - " + newProfile.name);
                    }
                    else
                        TGMPlugin.Logger.LogMessage($"Failed to Load External Profile, is it a valid json? - " + i);

                }
            }
            catch (Exception ex)
            {
                FistVR.SM.PlayGlobalUISound(FistVR.SM.GlobalUISound.Error, FistVR.GM.CurrentPlayerBody.transform.position);
                TGMPlugin.Logger.LogMessage(ex.Message);
                return null;
            }
        }

        TGM_ProfileMenu.instance.PopulateProfiles();

        return profiles;
    }

    public static int GetGamemodeIndex(string gamemodeName)
    {
        for (int i = 0; i < TGM_Manager.instance.gamemodes.Count; i++)
        {
            if (TGM_Manager.instance.gamemodes[i].name == gamemodeName)
                return i;
        }
        return 0;
    }

    public static int GetSosigTeamIndex(string teamName)
    {
        for (int i = 0; i < sosigTeams.Count; i++)
        {
            if (sosigTeams[i].name == teamName)
                return i;
            
        }

        //Return first index if missing team
        return 0;
    }

    public static int GetPlayerTeamIndex(string teamName)
    {
        for (int i = 0; i < playerTeams.Count; i++)
        {
            if (playerTeams[i].name == teamName)
                return i;

        }

        //Return first index if missing team
        return 0;
    }

    //--------------------------------------------------------------------------------------------------------
    // Sprite Textures
    //--------------------------------------------------------------------------------------------------------

    public static Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

    public static Sprite LoadSprite(string path)
    {
        Texture2D tex = null;

        loadedTextures.TryGetValue(path, out tex);

        if (tex == null)
        {
            byte[] fileData;

            if (File.Exists(path) && tex == null)
            {
                fileData = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2);
                tex.LoadImage(fileData);
                tex.name = path;
            }

            if (tex == null)
            {
                TGMPlugin.Logger.LogError($"Texture Not Found: " + path);
                return null;
            }
            else
            {
                loadedTextures.Add(path, tex);
            }
        }

        Sprite NewSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0), 100.0f);

        return NewSprite;
    }

}
