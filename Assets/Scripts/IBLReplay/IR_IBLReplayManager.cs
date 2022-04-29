using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

public class IR_IBLReplayManager : MonoBehaviour
{
    [SerializeField] Networking networking;
    [SerializeField] Utils util;
    [SerializeField] IR_ReplayControls replayControls;

    // UI Elemetns
    [SerializeField] TMP_Dropdown sessionDropdown;
    [SerializeField] VideoPlayer leftPlayer;
    [SerializeField] VideoPlayer bodyPlayer;
    [SerializeField] VideoPlayer rightPlayer;

    // Addressable Assets
    [SerializeField] string assetPrefix;
    [SerializeField] AssetReference sessionAsset;

    // Data
    private Dictionary<string, Dictionary<int, Vector3[]>> trajectories;

    // Sessions
    string[] sessions;
    Dictionary<string, ReplaySession> loadedSessions;
    ReplaySession activeSession;

    private void Awake()
    {
        LoadTrajectories();
        loadedSessions = new Dictionary<string, ReplaySession>();
        LoadSessionInfo();
    }

    public string GetAssetPrefix()
    {
        return assetPrefix;
    }

    public async void LoadSessionInfo()
    {
        AsyncOperationHandle<TextAsset> sessionLoader = Addressables.LoadAssetAsync<TextAsset>(sessionAsset);
        await sessionLoader.Task;

        TextAsset sessionData = sessionLoader.Result;
        sessions = sessionData.text.Split('\n');

        // Populate the dropdown menu with the sessions
        sessionDropdown.AddOptions(new List<string> { "" }); // add a blank option
        sessionDropdown.AddOptions(new List<string>(sessions));

    }

    public async void LoadTrajectories()
    {
        string filename = GetAssetPrefix() + "probe_trajectories.csv";
        AsyncOperationHandle<TextAsset> trajLoader = Addressables.LoadAssetAsync<TextAsset>(filename);

        await trajLoader.Task;

        List<Dictionary<string, object>> trajData = CSVReader.ParseText(trajLoader.Result.text);

        trajectories = new Dictionary<string, Dictionary<int, Vector3[]>>();

        for (int i = 0; i < trajData.Count; i++)
        {
            Dictionary<string, object> row = trajData[i];

            string eid = (string)row["eid"];
            int probe = (int)char.GetNumericValue(((string)row["probe"])[6]);

            float ml = (float)row["ml"];
            float ap = (float)row["ap"];
            float dv = (float)row["dv"];
            float depth = (float)row["depth"];
            float theta = (float)row["theta"];
            float phi = (float)row["phi"];

            Vector3 mlapdv = new Vector3(ml, ap, dv);
            Vector3 dtp = new Vector3(depth, theta, phi);

            if (!trajectories.ContainsKey(eid))
                trajectories[eid] = new Dictionary<int, Vector3[]>();

            trajectories[eid].Add(probe, new Vector3[] { mlapdv, dtp });
        }
    }

    public void ChangeSession(int newSessionID)
    {
        string eid = sessions[newSessionID - 1];
        Debug.Log("(RManager) Changing session to: " + eid);
        LoadSession(eid);
    }

    // Start is called before the first frame update
    void Start()
    {
        networking.startHost();

    }

    public async void LoadSession(string eid)
    {
        if (eid.Length == 0)
            return;

        if (loadedSessions.ContainsKey(eid))
            activeSession = loadedSessions[eid];
        else
        {
            ReplaySession session = new ReplaySession(eid, this, util, trajectories[eid]);
            loadedSessions.Add(eid, session);

            replayControls.SetControlInteraction(false);
            await session.LoadAssets();

            activeSession = session;
            replayControls.SetControlInteraction(true);
        }
    }

    public void SetVideoData(VideoClip left, VideoClip body, VideoClip right)
    {
        leftPlayer.clip = left;
        bodyPlayer.clip = body;
        rightPlayer.clip = right;
    }
}
