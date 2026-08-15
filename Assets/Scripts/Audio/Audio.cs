using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;

public class Audio : MonoBehaviour
{
    public enum TYPE
    {
        MASTER,
        MUSIC,
        SFX,
        AMBIENCE,
        MAX
    }

    private static Audio instance = null;

    private static float[] volumes = new float[(int)TYPE.MAX];
    private static Bus[] buses = new Bus[(int)TYPE.MAX];
    private static EventInstance[] events = new EventInstance[(int)TYPE.MAX];
    private static EventReference[] currentRef = new EventReference[(int)TYPE.MAX];

    private static string[] busPaths =
    {
        "bus:/",
        //"bus:/Music",
        //"bus:/SFX",
        //"bus:/Ambience",
    };
    private static bool isBusSet = false;

    private static FMODEvents fmodEvents;
    private static bool isMessagingRegistered = false;

    private const string MSG_SERVER_TO_CLIENT_SFX = "Audio_S2C_SFX";
    private const string MSG_CLIENT_TO_SERVER_SFX = "Audio_C2S_SFX";

    private void Awake()
    {
        if (instance != null)
        {
            if (instance != this)
            {
                Destroy(gameObject);
            }
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        clearVariables();
    }

    private void Start()
    {
        fmodEvents = FMODEvents.instance ?? FindAnyObjectByType<FMODEvents>();
        EnsureMessagingRegistered();

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu" && currentScene != "Main Menu")
        {
            StartCoroutine(WaitForBanksAndPlayMusic());
        }
    }

    private IEnumerator WaitForBanksAndPlayMusic()
    {
        float timer = 0f;
        while (!RuntimeManager.IsInitialized && timer < 5f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        playGameMusic();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted += OnNetworkStarted;
            NetworkManager.Singleton.OnServerStarted += OnNetworkStarted;
            NetworkManager.Singleton.OnClientStopped += OnNetworkStopped;
            NetworkManager.Singleton.OnServerStopped += OnNetworkStopped;
        }
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted -= OnNetworkStarted;
            NetworkManager.Singleton.OnServerStarted -= OnNetworkStarted;
            NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
            NetworkManager.Singleton.OnServerStopped -= OnNetworkStopped;
        }
        isMessagingRegistered = false;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" || scene.name == "Main Menu")
        {
            stop(TYPE.MUSIC);
        }
        else if (scene.name == "SampleScene")
        {
            StartCoroutine(WaitForBanksAndPlayMusic());
        }
    }

    private void OnNetworkStarted()
    {
        EnsureMessagingRegistered();
    }

    private void OnNetworkStopped(bool isServer)
    {
        isMessagingRegistered = false;
    }

    private static void EnsureMessagingRegistered()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            if (!isMessagingRegistered)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_SERVER_TO_CLIENT_SFX, OnReceiveSFXClient);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_CLIENT_TO_SERVER_SFX, OnReceiveSFXServer);
                isMessagingRegistered = true;
            }
        }
    }

    public static void PlayNetworkedSFX(EventReference eventRef, Vector3 pos, float volume = 1f)
    {
        if (eventRef.IsNull) return;

        EnsureMessagingRegistered();

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.CustomMessagingManager == null)
        {
            playSFXInternal(eventRef, pos, volume);
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            BroadcastSFXToClients(eventRef, pos, volume);
        }
        else
        {
            SendSFXToServer(eventRef, pos, volume);
        }
    }

    public static string GetEventPath(EventReference eventRef)
    {
#if UNITY_EDITOR
        return eventRef.Path;
#else
        return null;
#endif
    }

    public static EventReference CreateEventRef(FMOD.GUID guid, string path)
    {
#if UNITY_EDITOR
        return new EventReference { Guid = guid, Path = path };
#else
        return new EventReference { Guid = guid };
#endif
    }

    private static void BroadcastSFXToClients(EventReference eventRef, Vector3 pos, float volume = 1f)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

        FMOD.GUID guid = eventRef.Guid;
        string path = GetEventPath(eventRef) ?? "";

        if (string.IsNullOrEmpty(path) && !guid.IsNull && RuntimeManager.IsInitialized && RuntimeManager.HaveMasterBanksLoaded)
        {
            try
            {
                var desc = RuntimeManager.GetEventDescription(guid);
                if (desc.isValid())
                {
                    desc.getPath(out path);
                }
            }
            catch { }
        }

        using FastBufferWriter writer = new FastBufferWriter(256 + (path.Length * 2), Allocator.Temp);
        writer.WriteValueSafe(guid.Data1);
        writer.WriteValueSafe(guid.Data2);
        writer.WriteValueSafe(guid.Data3);
        writer.WriteValueSafe(guid.Data4);
        writer.WriteValueSafe(path);
        writer.WriteValueSafe(pos);
        writer.WriteValueSafe(volume);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MSG_SERVER_TO_CLIENT_SFX, writer);

        // Always play locally on server/host
        playSFXInternal(eventRef, pos, volume);
    }

    private static void SendSFXToServer(EventReference eventRef, Vector3 pos, float volume = 1f)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

        FMOD.GUID guid = eventRef.Guid;
        string path = GetEventPath(eventRef) ?? "";

        if (string.IsNullOrEmpty(path) && !guid.IsNull && RuntimeManager.IsInitialized && RuntimeManager.HaveMasterBanksLoaded)
        {
            try
            {
                var desc = RuntimeManager.GetEventDescription(guid);
                if (desc.isValid())
                {
                    desc.getPath(out path);
                }
            }
            catch { }
        }

        using FastBufferWriter writer = new FastBufferWriter(256 + (path.Length * 2), Allocator.Temp);
        writer.WriteValueSafe(guid.Data1);
        writer.WriteValueSafe(guid.Data2);
        writer.WriteValueSafe(guid.Data3);
        writer.WriteValueSafe(guid.Data4);
        writer.WriteValueSafe(path);
        writer.WriteValueSafe(pos);
        writer.WriteValueSafe(volume);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MSG_CLIENT_TO_SERVER_SFX, NetworkManager.ServerClientId, writer);
    }

    private static void OnReceiveSFXClient(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int d1);
        reader.ReadValueSafe(out int d2);
        reader.ReadValueSafe(out int d3);
        reader.ReadValueSafe(out int d4);
        reader.ReadValueSafe(out string path);
        reader.ReadValueSafe(out Vector3 pos);
        reader.ReadValueSafe(out float volume);

        FMOD.GUID guid = new FMOD.GUID { Data1 = d1, Data2 = d2, Data3 = d3, Data4 = d4 };
        
        EventReference eventRef = default;
        if (!string.IsNullOrEmpty(path))
        {
            eventRef = RuntimeManager.PathToEventReference(path);
        }
        if (eventRef.IsNull)
        {
            eventRef = CreateEventRef(guid, path);
        }

        // Client (non-host) plays sound locally upon receiving broadcast
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            playSFXInternal(eventRef, pos, volume);
        }
    }

    private static void OnReceiveSFXServer(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int d1);
        reader.ReadValueSafe(out int d2);
        reader.ReadValueSafe(out int d3);
        reader.ReadValueSafe(out int d4);
        reader.ReadValueSafe(out string path);
        reader.ReadValueSafe(out Vector3 pos);
        reader.ReadValueSafe(out float volume);

        FMOD.GUID guid = new FMOD.GUID { Data1 = d1, Data2 = d2, Data3 = d3, Data4 = d4 };
        
        EventReference eventRef = default;
        if (!string.IsNullOrEmpty(path))
        {
            eventRef = RuntimeManager.PathToEventReference(path);
        }
        if (eventRef.IsNull)
        {
            eventRef = CreateEventRef(guid, path);
        }

        // Server broadcasts to all clients (including local playback on host)
        BroadcastSFXToClients(eventRef, pos, volume);
    }

    private static void playSFXInternal(EventReference eventRef, Vector3 pos, float volume = 1f)
    {
        string eventPath = GetEventPath(eventRef);
        if (eventRef.IsNull && string.IsNullOrEmpty(eventPath)) return;

        try
        {
            if (!RuntimeManager.IsInitialized) return;

            // 1. Try StudioSystem getEvent directly using string Path (works on virtual clients)
            if (!string.IsNullOrEmpty(eventPath))
            {
                try
                {
                    var res = RuntimeManager.StudioSystem.getEvent(eventPath, out var desc);
                    if (res == FMOD.RESULT.OK && desc.isValid())
                    {
                        desc.createInstance(out var inst);
                        if (inst.isValid())
                        {
                            inst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
                            inst.setVolume(volume);
                            inst.start();
                            inst.release();
                            return;
                        }
                    }
                }
                catch { }
            }

            // 2. Try StudioSystem getEventByID directly using GUID
            if (!eventRef.Guid.IsNull)
            {
                try
                {
                    var res = RuntimeManager.StudioSystem.getEventByID(eventRef.Guid, out var desc);
                    if (res == FMOD.RESULT.OK && desc.isValid())
                    {
                        desc.createInstance(out var inst);
                        if (inst.isValid())
                        {
                            inst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
                            inst.setVolume(volume);
                            inst.start();
                            inst.release();
                            return;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void playSFXInternal(FMOD.GUID guid, Vector3 pos)
    {
        if (guid.IsNull) return;

        try
        {
            if (!RuntimeManager.IsInitialized || !RuntimeManager.HaveMasterBanksLoaded) return;

            var eventDesc = RuntimeManager.GetEventDescription(guid);
            if (!eventDesc.isValid()) return;

            RuntimeManager.PlayOneShot(guid, pos);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Audio] Could not play SFX GUID '{guid}': {ex.Message}");
        }
    }

    private static void playSFXInternal(string eventIdentifier, Vector3 pos)
    {
        if (string.IsNullOrEmpty(eventIdentifier)) return;

        try
        {
            RuntimeManager.PlayOneShot(eventIdentifier, pos);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Audio] Could not play SFX '{eventIdentifier}': {ex.Message}");
        }
    }

    private static void clearVariables()
    {
        isBusSet = false;
        for (int i = 0; i < (int)TYPE.MAX; i++)
        {
            events[i] = default;
            currentRef[i] = default;
        }
    }

    private static void setBuses()
    {
        if (isBusSet) { return; }
        isBusSet = true;

        for (int i = 0; i < (int)TYPE.MAX; i++)
        {
            buses[i] = RuntimeManager.GetBus(busPaths[i]);
        }
    }

    public static void playGameMusic()
    {
        if (fmodEvents == null)
        {
            fmodEvents = FMODEvents.instance ?? FindAnyObjectByType<FMODEvents>();
        }

        if (fmodEvents == null) return;

        EventReference sound = fmodEvents.sombieStyle;
        if (!sound.IsNull)
        {
            play(TYPE.MUSIC, sound);
        }
    }

    public static void playTimelineSFX(EventReference eventReference, Vector3 pos = default)
    {
        play(TYPE.MUSIC, eventReference, pos);
    }

    public static float volume(TYPE type, float value = -1)
    {
        if (value < 0)
        {
            return volumes[(int)type];
        }
        setBuses();
        volumes[(int)type] = Mathf.Clamp01(value);
        buses[(int)type].setVolume(volumes[(int)type]);
        return value;
    }

    private static void play(TYPE type, EventReference eventRef, Vector3 pos = default, float volume = 1f)
    {
        string eventPath = GetEventPath(eventRef);
        if (eventRef.IsNull && string.IsNullOrEmpty(eventPath)) return;

        if (type == TYPE.SFX)
        {
            playSFXInternal(eventRef, pos, volume);
            return;
        }
        if (currentRef[(int)type].Guid == eventRef.Guid && !currentRef[(int)type].Guid.IsNull)
        {
            return; // Don't restart music/ambience
        }
        stop(type);

        EventInstance eventInst = CreateSFXInstance(eventRef);
        if (eventInst.isValid())
        {
            eventInst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
            eventInst.setVolume(volume);
            eventInst.start();
            events[(int)type] = eventInst;
            currentRef[(int)type] = eventRef;
        }
    }

    public static void playSFX(EventReference eventRef, Vector3 pos = default, float volume = 1f)
    {
        play(TYPE.SFX, eventRef, pos, volume);
    }

    public static EventInstance CreateSFXInstance(EventReference eventRef)
    {
        if (eventRef.IsNull) return default;

        try
        {
            if (!RuntimeManager.IsInitialized) return default;

            try
            {
                EventInstance inst = RuntimeManager.CreateInstance(eventRef);
                if (inst.isValid()) return inst;
            }
            catch { }

            string eventPath = GetEventPath(eventRef);
            // 1. Try StudioSystem getEvent directly using string Path
            if (!string.IsNullOrEmpty(eventPath))
            {
                try
                {
                    var res = RuntimeManager.StudioSystem.getEvent(eventPath, out var desc);
                    if (res == FMOD.RESULT.OK && desc.isValid())
                    {
                        desc.createInstance(out var inst);
                        if (inst.isValid()) return inst;
                    }
                }
                catch { }
            }

            // 2. Try StudioSystem getEventByID directly using GUID
            if (!eventRef.Guid.IsNull)
            {
                try
                {
                    var res = RuntimeManager.StudioSystem.getEventByID(eventRef.Guid, out var desc);
                    if (res == FMOD.RESULT.OK && desc.isValid())
                    {
                        desc.createInstance(out var inst);
                        if (inst.isValid()) return inst;
                    }
                }
                catch { }
            }
        }
        catch { }

        return default;
    }

    public static EventInstance playSFXInstance(EventReference eventRef, Vector3 pos = default)
    {
        EventInstance eventInst = CreateSFXInstance(eventRef);
        if (eventInst.isValid())
        {
            eventInst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
            eventInst.start();
        }
        return eventInst;
    }

    private static void stop(TYPE type)
    {
        EventInstance eventInst = events[(int)type];
        if (!eventInst.isValid()) { return; }

        eventInst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        eventInst.release();
        events[(int)type] = default;
    }
}
