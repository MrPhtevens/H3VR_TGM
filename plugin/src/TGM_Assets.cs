using UnityEngine;


namespace TeamsGameMode
{
    [CreateAssetMenu(fileName = "TGM Assets", menuName = "Teams Game Mode/TGM Assets", order = 1)]
    public class TGM_Assets : ScriptableObject
    {
        public TGM_Manager manager;

        public TGM_MainMenu mainMenu;
        public TGM_TeamSetup teamSetup;
        public TGM_ProfileMenu profileMenu;

        public TGM_ClassMenu classMenu;

        public TGM_Spectator spectator;

        public TGM_Compass compass;

        public TGM_EndScreen endScreen;

        public GameObject iffPrefab;

        public TGM_MapSelector mapSelector;
    }
}
