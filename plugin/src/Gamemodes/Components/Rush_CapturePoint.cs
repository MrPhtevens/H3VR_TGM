using UnityEngine;
using UnityEngine.UI;
using FistVR;
using H3MP.Networking;
using System.Collections.Generic;

namespace TeamsGameMode;

/// <summary>
/// Rush Mcom: attackers arm it, it alarms for fuseTime seconds and then explodes unless defenders disarm it.
/// The host (or single player) runs the state, clients report their hands and display what the host sends.
/// </summary>
public class Rush_CapturePoint : MonoBehaviour
{
    public enum McomState { Idle = 0, Arming = 1, Armed = 2, Disarming = 3, Destroyed = 4 }

    public int detectIFF = 0;   //Detect RED by default (attackers)
    [HideInInspector]
    public bool canCapture = false;

    public float captureTotalTime = 5;  //Seconds to arm
    public Image captureCircle;
    public GameObject capturedPrefab;
    [HideInInspector]
    public GameObject spawnedPrefab;

    public AudioSource audioSource;
    public Vector2 audioPitchRange = new Vector2(0.75f, 1.25f);

    //Set by TGM_Rush when spawned
    [HideInInspector] public int pointIndex = -1;

    public const float fuseTime = 25;       //Seconds from armed to explosion
    public const float disarmTotalTime = 5; //Seconds defenders need to disarm
    const float sendInterval = 0.2f;        //Host state sync rate
    const float presenceTimeout = 2.5f;     //Remote hands expire if not refreshed

    //Synced state
    public McomState state = McomState.Idle;
    float progress = 0;     //Arming or disarming seconds
    float fuseRemaining = 0;

    //Local presence
    int handCount = 0;
    readonly List<Sosig> sosigsInside = new List<Sosig>();

    //Host: remote client hands, clientID -> (iff, last refresh time)
    readonly Dictionary<int, KeyValuePair<int, float>> remotePresence = new Dictionary<int, KeyValuePair<int, float>>();

    //Client: last presence we told the host about
    int sentPresenceIFF = -1;
    float nextPresenceSend = 0;
    float nextStateSend = 0;

    AudioSource alarmSource;
    static AudioClip alarmClip;
    static AudioClip armingClip;

    static bool IsAuthority => !Tools.ServerRunning() || Tools.IsHost();

    void Awake()
    {
        SetupAudio();
    }

    void OnEnable()
    {
        //Defaults on Enabled
        state = McomState.Idle;
        progress = 0;
        fuseRemaining = 0;
        captureCircle.gameObject.SetActive(false);
        handCount = 0;
        sosigsInside.Clear();
        remotePresence.Clear();

        if (spawnedPrefab != null)
            Destroy(spawnedPrefab);
    }

    void SetupAudio()
    {
        //Arming sound, louder and audible from further away
        if (audioSource != null)
        {
            //The mod's "capturing" clip is an announcer voice line, not a loop, so use a generated beep instead
            if (armingClip == null)
                armingClip = CreateArmingClip();
            audioSource.clip = TGM_McomSounds.arming != null ? TGM_McomSounds.arming : armingClip;
            audioSource.loop = true;
            audioSource.spatialBlend = 1;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 8;
            audioSource.maxDistance = 60;
            audioSource.volume = 0;
            audioSource.Stop();
        }

        //Alarm while armed
        if (alarmClip == null)
            alarmClip = CreateAlarmClip();
        alarmSource = gameObject.AddComponent<AudioSource>();
        alarmSource.clip = TGM_McomSounds.armed != null ? TGM_McomSounds.armed : alarmClip;
        alarmSource.loop = true;
        alarmSource.playOnAwake = false;
        alarmSource.spatialBlend = 1;
        alarmSource.rolloffMode = AudioRolloffMode.Linear;
        alarmSource.minDistance = 15;
        alarmSource.maxDistance = 120;
        alarmSource.volume = 1;
    }

    /// <summary>
    /// Generates a quick electronic tick, looped it sounds like a device being armed (pitch rises with progress)
    /// </summary>
    static AudioClip CreateArmingClip()
    {
        const int rate = 44100;
        const float length = 0.25f;
        float[] data = new float[(int)(rate * length)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            if (t < 0.05f)
            {
                float fade = 1 - (t / 0.05f);   //Short decay so it clicks rather than buzzes
                data[i] = Mathf.Sin(2 * Mathf.PI * 1600 * t) * 0.4f * fade;
            }
        }
        AudioClip clip = AudioClip.Create("TGM_McomArming", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Generates a short two tone beep with a gap, looped it makes an alarm
    /// </summary>
    static AudioClip CreateAlarmClip()
    {
        const int rate = 44100;
        const float length = 0.5f;
        float[] data = new float[(int)(rate * length)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            if (t < 0.12f)
                data[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 1100 * t)) * 0.35f;
            else if (t > 0.15f && t < 0.27f)
                data[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 850 * t)) * 0.35f;
        }
        AudioClip clip = AudioClip.Create("TGM_McomAlarm", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Update()
    {
        if (IsAuthority)
            AuthorityUpdate();
        else
            ClientUpdate();

        UpdateVisuals();
    }

    //------------------------------------------------------------------------------
    // Host / Single player
    //------------------------------------------------------------------------------

    void AuthorityUpdate()
    {
        if (state == McomState.Destroyed)
            return;

        //Not the active objective (or not in gameplay), reset back to idle
        if (!canCapture || TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
        {
            if (state != McomState.Idle || progress != 0)
                SetState(McomState.Idle, 0, 0);
            return;
        }

        int defendIFF = Global.GetEnemyIFF(detectIFF);
        bool attackers = IsPresent(detectIFF);
        bool defenders = IsPresent(defendIFF);
        float dt = Time.deltaTime;

        switch (state)
        {
            case McomState.Idle:
            case McomState.Arming:
                if (attackers)
                {
                    progress = Mathf.Min(progress + dt, captureTotalTime);
                    if (progress >= captureTotalTime)
                    {
                        SetState(McomState.Armed, 0, fuseTime);
                        return;
                    }
                    state = McomState.Arming;
                }
                else
                {
                    progress = Mathf.Max(progress - (dt * 2), 0);
                    state = progress > 0 ? McomState.Arming : McomState.Idle;
                }
                break;

            case McomState.Armed:
            case McomState.Disarming:
                fuseRemaining -= dt;
                if (fuseRemaining <= 0)
                {
                    Detonate();
                    return;
                }

                //Defenders disarm, attackers on the point stop it
                if (defenders && !attackers)
                {
                    progress = Mathf.Min(progress + dt, disarmTotalTime);
                    if (progress >= disarmTotalTime)
                    {
                        SetState(McomState.Idle, 0, 0);
                        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Point);
                        return;
                    }
                    state = McomState.Disarming;
                }
                else
                {
                    progress = Mathf.Max(progress - (dt * 2), 0);
                    state = progress > 0 ? McomState.Disarming : McomState.Armed;
                }
                break;
        }

        //Sync to clients
        if (Time.time >= nextStateSend)
            SendState();
    }

    void SetState(McomState newState, float newProgress, float newFuse)
    {
        state = newState;
        progress = newProgress;
        fuseRemaining = newFuse;
        SendState();
    }

    void SendState()
    {
        nextStateSend = Time.time + sendInterval;
        TGM_Rush rush = TGM_Manager.instance.gamemode as TGM_Rush;
        if (rush != null)
            rush.McomState_ToClients(pointIndex, (int)state, progress, fuseRemaining);
    }

    void Detonate()
    {
        SetState(McomState.Destroyed, 0, 0);
        Explode();

        //Score the destroyed Mcom, this moves the objective on
        TGM_Manager.instance.gamemode.AdjustTeamScore(detectIFF, 1);
    }

    bool IsPresent(int iff)
    {
        //Local player
        if (handCount > 0
            && GM.CurrentPlayerBody.GetPlayerIFF() == iff
            && GM.CurrentPlayerBody.Health > 0)
            return true;

        //Sosigs (host owns them)
        for (int i = sosigsInside.Count - 1; i >= 0; i--)
        {
            if (sosigsInside[i] == null || sosigsInside[i].BodyState == Sosig.SosigBodyState.Dead)
            {
                sosigsInside.RemoveAt(i);
                continue;
            }
            if (sosigsInside[i].GetIFF() == iff)
                return true;
        }

        //Remote players
        foreach (var presence in remotePresence.Values)
        {
            if (presence.Key == iff && Time.time - presence.Value < presenceTimeout)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Host: a client told us whether their hands are on this Mcom
    /// </summary>
    public void ReceivePresence(int clientID, int iff)
    {
        if (iff < 0)
            remotePresence.Remove(clientID);
        else
            remotePresence[clientID] = new KeyValuePair<int, float>(iff, Time.time);
    }

    //------------------------------------------------------------------------------
    // Client
    //------------------------------------------------------------------------------

    void ClientUpdate()
    {
        //Tell the host when our hands are on the Mcom, refresh while they stay there
        int presenceIFF = -1;
        if (canCapture && handCount > 0 && GM.CurrentPlayerBody.Health > 0)
            presenceIFF = GM.CurrentPlayerBody.GetPlayerIFF();

        if (presenceIFF != sentPresenceIFF || (presenceIFF >= 0 && Time.time >= nextPresenceSend))
        {
            sentPresenceIFF = presenceIFF;
            nextPresenceSend = Time.time + 1;
            TGM_Rush rush = TGM_Manager.instance.gamemode as TGM_Rush;
            if (rush != null)
                rush.McomPresence_ToServer(pointIndex, presenceIFF);
        }

        //Count down locally between host updates so the timer looks smooth
        if (state == McomState.Armed || state == McomState.Disarming)
            fuseRemaining = Mathf.Max(fuseRemaining - Time.deltaTime, 0);
    }

    /// <summary>
    /// Client: state received from the host
    /// </summary>
    public void ReceiveState(McomState newState, float newProgress, float newFuse)
    {
        if (state == McomState.Destroyed)
            return;

        //Defenders disarmed it
        bool wasArmed = state == McomState.Armed || state == McomState.Disarming;
        if (wasArmed && newState == McomState.Idle)
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Point);

        state = newState;
        progress = newProgress;
        fuseRemaining = newFuse;

        if (newState == McomState.Destroyed)
            Explode();
    }

    //------------------------------------------------------------------------------
    // Shared
    //------------------------------------------------------------------------------

    void UpdateVisuals()
    {
        bool showCircle = canCapture && progress > 0
            && (state == McomState.Arming || state == McomState.Disarming);

        if (showCircle)
        {
            float total = state == McomState.Arming ? captureTotalTime : disarmTotalTime;
            captureCircle.gameObject.SetActive(true);
            captureCircle.transform.parent.LookAt(GM.CurrentPlayerBody.Head.position);
            captureCircle.fillAmount = Mathf.InverseLerp(0, total, progress);

            if (audioSource != null)
            {
                if (!audioSource.isPlaying)
                    audioSource.Play();
                //Custom sounds play as recorded, the built in one ramps up
                bool custom = TGM_McomSounds.arming != null;
                audioSource.volume = custom ? 1 : Mathf.Lerp(0.5f, 1, captureCircle.fillAmount);
                audioSource.pitch = custom ? 1 : Mathf.Lerp(audioPitchRange.x, audioPitchRange.y, captureCircle.fillAmount);
            }
        }
        else
        {
            if (captureCircle.gameObject.activeSelf)
                captureCircle.gameObject.SetActive(false);
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        //Alarm speeds up as the fuse runs out
        bool armed = state == McomState.Armed || state == McomState.Disarming;
        if (armed)
        {
            if (!alarmSource.isPlaying)
                alarmSource.Play();
            alarmSource.pitch = TGM_McomSounds.armed != null ? 1 : Mathf.Lerp(1.6f, 1f, Mathf.InverseLerp(0, fuseTime, fuseRemaining));
        }
        else if (alarmSource.isPlaying)
            alarmSource.Stop();
    }

    void Explode()
    {
        if (alarmSource != null)
            alarmSource.Stop();
        if (audioSource != null)
            audioSource.Stop();

        SpawnExplosionEffect(transform.position);
        CapturePoint();
    }

    /// <summary>
    /// Spawns the M26 grenade explosion effect, keeping only particles, lights and sound so nothing can be hurt
    /// </summary>
    static void SpawnExplosionEffect(Vector3 position)
    {
        bool customSound = TGM_McomSounds.explosion != null;
        if (customSound)
            PlayExplosionSound(position);

        FVRObject grenadeObject;
        if (!IM.OD.TryGetValue("PinnedGrenadeM26", out grenadeObject) || grenadeObject.GetGameObject() == null)
        {
            TGMPlugin.Logger.LogWarning("Mcom explosion: grenade prefab not found");
            return;
        }

        PinnedGrenade grenade = grenadeObject.GetGameObject().GetComponent<PinnedGrenade>();
        if (grenade == null || grenade.SpawnOnSplode == null)
            return;

        //Spawn under an inactive holder so we can strip damage before anything runs
        GameObject holder = new GameObject("TGM_McomExplosionHolder");
        holder.SetActive(false);

        for (int i = 0; i < grenade.SpawnOnSplode.Count; i++)
        {
            if (grenade.SpawnOnSplode[i] == null)
                continue;

            GameObject fx = Instantiate(grenade.SpawnOnSplode[i], position + Vector3.up, Quaternion.identity, holder.transform);

            //Remove every script (damage, shrapnel, secondary spawns), keep the game's explosion sound unless we have our own
            foreach (MonoBehaviour script in fx.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!customSound && script is ExplosionSound)
                    continue;
                DestroyImmediate(script);
            }
            foreach (Collider collider in fx.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(collider);

            fx.transform.SetParent(null, true);
            fx.SetActive(true);
            Destroy(fx, 10);    //Its own cleanup script was removed above
        }
        Destroy(holder);
    }

    static void PlayExplosionSound(Vector3 position)
    {
        GameObject soundObject = new GameObject("TGM_McomExplosionSound");
        soundObject.transform.position = position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = TGM_McomSounds.explosion;
        source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 20;
        source.maxDistance = 300;
        source.volume = 1;
        source.Play();
        Destroy(soundObject, TGM_McomSounds.explosion.length + 0.5f);
    }

    public void CapturePoint()
    {
        //Already set
        if (spawnedPrefab != null)
            return;

        state = McomState.Destroyed;

        //Called on all clients
        spawnedPrefab = Instantiate(capturedPrefab, transform.position, transform.rotation);
        spawnedPrefab.SetActive(true);

        //Disable capturing
        canCapture = false;

        //Disable
        gameObject.SetActive(false);

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Point);
    }

    void OnTriggerEnter(Collider other)
    {
        //Local player's hands
        if (other.name.Contains("Controller ("))
        {
            handCount++;
            return;
        }

        //One entry per body part inside, removed again per part on exit
        SosigLink sosig = other.gameObject.GetComponent<SosigLink>();
        if (sosig != null && sosig.S != null)
            sosigsInside.Add(sosig.S);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("Controller ("))
        {
            handCount = Mathf.Max(handCount - 1, 0);
            return;
        }

        SosigLink sosig = other.gameObject.GetComponent<SosigLink>();
        if (sosig != null)
            sosigsInside.Remove(sosig.S);
    }

    void OnValidate()
    {
        //gameObject.layer = LayerMask.NameToLayer("Interactable");
    }
}
