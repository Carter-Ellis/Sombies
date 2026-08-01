using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using Unity.Netcode;

public class Audio : NetworkBehaviour
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
        fmodEvents = FindAnyObjectByType<FMODEvents>();

        clearVariables();
        playGameMusic();
        //setBuses();

        /*SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);*/

    }

    public static void PlayNetworkedSFX(EventReference eventRef, Vector3 pos)
    {
        if (instance != null && !eventRef.IsNull)
        {
            if (instance.IsServer)
            {
                instance.PlaySoundClientRpc(
                    eventRef.Guid.Data1,
                    eventRef.Guid.Data2,
                    eventRef.Guid.Data3,
                    eventRef.Guid.Data4,
                    pos
                );
            }
            else
            {
                instance.RequestPlaySoundServerRpc(
                    eventRef.Guid.Data1,
                    eventRef.Guid.Data2,
                    eventRef.Guid.Data3,
                    eventRef.Guid.Data4,
                    pos
                );
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlaySoundServerRpc(int d1, int d2, int d3, int d4, Vector3 position)
    {
        PlaySoundClientRpc(d1, d2, d3, d4, position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(int d1, int d2, int d3, int d4, Vector3 position)
    {
        FMOD.GUID networkGuid = new FMOD.GUID { Data1 = d1, Data2 = d2, Data3 = d3, Data4 = d4 };

        EventInstance eventInst = RuntimeManager.CreateInstance(networkGuid);
        eventInst.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        eventInst.start();
        eventInst.release();
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

    /*public static void playMainMusic()
    {
        play(TYPE.MUSIC, fmodEvents.mainMusic);
    }*/
    
    public static void playGameMusic()
    {
        EventReference sound = fmodEvents.sombieStyle;
        
        play(TYPE.MUSIC, sound);
    }
    /*
    public static void playShopMusic()
    {
        play(TYPE.MUSIC, fmodEvents.shopMusic);
    }

    public static void playAmbienceMusic()
    {
        EventReference sound = fmodEvents.ambience;
        if (Map.current == Map.TYPE.BEACH)
        {
            sound = fmodEvents.beachAmbience;
        }
        play(TYPE.AMBIENCE, sound);
    }*/

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

    private static void play(TYPE type, EventReference eventRef, Vector3 pos = default)
    {
        if (type == TYPE.SFX)
        {
            RuntimeManager.PlayOneShot(eventRef, pos);
            return;
        }
        if (currentRef[(int)type].Guid == eventRef.Guid)
        {
            return; //Don't restart music/ambience
        }
        stop(type);

        EventInstance eventInst = RuntimeManager.CreateInstance(eventRef);
        eventInst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
        eventInst.start();
        events[(int)type] = eventInst;
        currentRef[(int)type] = eventRef;
        print("playing: " + eventRef.Path + " at " + pos);
    }

    public static void playSFX(EventReference eventRef, Vector3 pos = default)
    {
        play(TYPE.SFX, eventRef, pos);
    }

    public static EventInstance playSFXInstance(EventReference eventRef, Vector3 pos = default)
    {
        EventInstance eventInst = RuntimeManager.CreateInstance(eventRef);
        eventInst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
        eventInst.start();
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

    /*private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu")
        {
            playMainMusic();
        }
        else
        {
            playGameMusic();
        }

        playAmbienceMusic();

    }*/

}
