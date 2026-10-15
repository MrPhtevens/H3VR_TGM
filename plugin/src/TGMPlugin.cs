using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Atlas;
using Atlas.Loaders;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamsGameMode
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    //[BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    [BepInDependency("VIP.TommySoucy.H3MP", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(AtlasConstants.Guid, AtlasConstants.Version)]
    public partial class TGMPlugin : BaseUnityPlugin
    {
        public static TGMPlugin instance;
        public static GameObject mapSelector;

        private void Awake()
        {
            instance = this;
            Logger = base.Logger;

            AtlasPlugin.Loaders["teamsgamemode"] = new SandboxLoader();


            //Logger.LogMessage($"Hello, world! Sent from {Id} {Name} {Version}");

            //print("Auto scene load in 8 seconds ");
            //Invoke(nameof(DebugSceneLoad), 8);

            SceneManager.activeSceneChanged += ChangedActiveScene;
        }


        public static IEnumerator CreateMapMenu(Vector3 position, Vector3 rotation, bool hideTnH = false)
        {
            //Load our Assets
            yield return instance.StartCoroutine(TGM_ModLoader.LoadAssets());

            //Didn't load assets, don't try to spawn them
            if (TGM_ModLoader.tgmAssets == null || TGM_ModLoader.tgmAssets.mapSelector == null)
                yield break;

            mapSelector = Instantiate(TGM_ModLoader.tgmAssets.mapSelector.gameObject, position, Quaternion.Euler(rotation));
            mapSelector.GetComponent<TGM_MapSelector>().tnhButton.SetActive(!hideTnH);
        }
        private void ChangedActiveScene(Scene current, Scene next)
        {
            if (next == null)
                return;

            if (next.name.Contains("MainMenu3"))
            {
                //Spawn our map selector
                StartCoroutine(CreateMapMenu(new Vector3(1.3f, 1.5f, 0f), new Vector3(35f, 90f, 0f), true));
                mapSelector = null;	//Unassign once we're done with it

            }
        }

        void DebugSceneLoad()
        {

            string sceneName = "TeamsGamemode_Example";

            print("Attempting to load scene: " + sceneName);
            CustomSceneInfo? info = AtlasPlugin.GetCustomScene(sceneName);
            if (info != null)
                AtlasPlugin.LoadCustomScene(sceneName);
            else
                SteamVR_LoadLevel.Begin(sceneName, false, 0.5f, 0f, 0f, 0f, 1f);

        }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
