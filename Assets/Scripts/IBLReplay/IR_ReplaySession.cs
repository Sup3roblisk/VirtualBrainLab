using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

/// <summary>
/// This class stores the data loaded for a single IBL experiment
/// </summary>
public class IR_ReplaySession
{
    IR_IBLReplayManager replayManager;
    Utils util;

    string eid;

    // SESSION DATA
    private static string[] dataTypes = { "spikes.times", "spikes.clusters", "wheel.position",
        "wheel.timestamps", "goCue_times", "feedback_times", "feedbackType",
        "contrastLeft","contrastRight","licks.times"};
    TaskCompletionSource<bool> taskLoadedSource;

    private List<string> waitingForData;
    private Dictionary<string, string> URIs;
    private Dictionary<string, Array> data;
    private List<string> pids;
    private Dictionary<int, List<Vector3>> coords;
    private Dictionary<string, float> videoTimes;
    private Dictionary<int, Vector3[]> trajectories;

    private Vector3[] paw3DL;
    private Vector3[] paw3DR;

    // TIME DATA
    private float[] quantiles = { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f };
    private Dictionary<int, int[]> quantileIndexes;
    private Dictionary<int, float[]> quantileTimes;
    private Dictionary<int, float> maxTime;

    public IR_ReplaySession(string eid, IR_IBLReplayManager replayManager, Utils util)
    {
        Debug.Log("New session data object created");
        this.eid = eid;
        this.replayManager = replayManager;
        this.util = util;

        waitingForData = new List<string>();
        URIs = new Dictionary<string, string>();
        data = new Dictionary<string, Array>();
        pids = new List<string>();
        coords = new Dictionary<int, List<Vector3>>();
        videoTimes = new Dictionary<string, float>();

        quantileIndexes = new Dictionary<int, int[]>();
        quantileTimes = new Dictionary<int, float[]>();
        maxTime = new Dictionary<int, float>();

        trajectories = new Dictionary<int, Vector3[]>();
    }

    public string GetEID()
    {
        return eid;
    }

    public List<string> GetPIDs()
    {
        return pids;
    }

    /// <summary>
    /// Load all assets for the session, this includes:
    /// data files that need to be downloaded from flatiron 
    /// cluster mlapdv coordinate data
    /// videos
    /// </summary>
    /// <returns></returns>
    public async Task<bool> LoadAssets()
    {
        Debug.Log("Asset load started for session");
        replayManager.SetLoading(true);

        // You have to load the file URLs first, since we need to have information about the probe count before we go on
        await LoadFileURLs();
        // Then you can simul-load all the cluster/traj/video data
        await Task.WhenAll(new Task[] { LoadClusters(), LoadVideos(), LoadTrajectoryData(), Load3DPaw() });

        replayManager.SetLoading(false);
        return true;
    }

    /// <summary>
    /// Get the stored data. Can be:
    /// spikes.times0/1
    /// spikes.clusters0/1
    /// wheel.position
    /// wheel.timestamps
    /// goCue_times
    /// feedback_times
    /// feedbackType
    /// contrastLeft
    /// contrastRight
    /// lick.times
    /// </summary>
    /// <param name="dataType"></param>
    /// <returns></returns>
    public Array GetData(string dataType)
    {
        return data[dataType];
    }

    /// <summary>
    /// Get the coordinates of all the clusters on a probe
    /// </summary>
    /// <param name="probe">[0/1]</param>
    /// <returns></returns>
    public List<Vector3> GetMLAPDVCoords(int probe)
    {
        return coords[probe];
    }

    /// <summary>
    /// Get the start time offset in (ms? s?) for the video
    /// </summary>
    /// <param name="videoOpt">left/right/body</param>
    /// <returns></returns>
    public float GetVideoStartTime(string videoOpt)
    {
        return videoTimes[videoOpt];
    }

    /// <summary>
    /// Get the probe trajectory information for the current probe
    /// </summary>
    /// <param name="probe"></param>
    /// <returns></returns>
    public Vector3[] GetProbeTrajectory(int probe)
    {
        return trajectories[probe];
    }

    private async Task<bool> LoadTrajectoryData()
    {
        // Load the probe trajectory CSV
        string filename = replayManager.GetAssetPrefix() + "probe_trajectories.csv";
        AsyncOperationHandle<TextAsset> trajLoader = Addressables.LoadAssetAsync<TextAsset>(filename);
        await trajLoader.Task;

        List<Dictionary<string, object>> trajectoryCSVdata = CSVReader.ParseText(trajLoader.Result.text);

        for (int i = 0; i < trajectoryCSVdata.Count; i++)
        {
            Dictionary<string, object> row = trajectoryCSVdata[i];

            string cEid = (string)row["eid"];

            if (eid == cEid)
            {
                int probe = (int)char.GetNumericValue(((string)row["probe"])[6]);

                float ml = Convert.ToSingle(row["ml"]);
                float ap = Convert.ToSingle(row["ap"]);
                float dv = Convert.ToSingle(row["dv"]);
                float depth = Convert.ToSingle(row["depth"]);
                float theta = Convert.ToSingle(row["theta"]);
                float phi = Convert.ToSingle(row["phi"]);

                Vector3 mlapdv = new Vector3(ml, ap, dv);
                Vector3 dtp = new Vector3(depth, theta, phi);

                trajectories.Add(probe, new Vector3[] { mlapdv, dtp });
            }
        }
        return true;
    }

    private Task LoadFileURLs()
    {
        taskLoadedSource = new TaskCompletionSource<bool>();
        Task filesLoadedTask = taskLoadedSource.Task;

        LoadFileURLs_helper();

        return filesLoadedTask;
    }

    private async void LoadFileURLs_helper()
    {
        string[] probeOpts = { "probe00", "probe01" };
        foreach (string probeOpt in probeOpts)
        {
            try
            {
                string filename = replayManager.GetAssetPrefix() + "Files/file_urls_" + eid + "_" + probeOpt + ".txt";
                AsyncOperationHandle<TextAsset> sessionLoader = Addressables.LoadAssetAsync<TextAsset>(filename);
                await sessionLoader.Task;

                ParseFileURLs(sessionLoader.Result);
            }
            catch
            {
                Debug.Log("No probe00 for EID: " + eid);
            }
        }

        Debug.Log("Load got called for: " + eid);

        foreach (string dataType in URIs.Keys)
        {
            Debug.Log("Started coroutine to load: " + dataType);
            util.LoadFlatIronData(dataType, URIs[dataType], AddSessionData);
            waitingForData.Add(dataType);
        }
    }

    private void AddSessionData(string type, Array receivedData)
    {
        Debug.Log("Receiving data: " + type + " with data type " + receivedData.GetType());
        data[type] = receivedData;

        if (type.Contains("spikes.times0"))
            ProcessSpikeData(data[type], 0);
        if (type.Contains("spikes.times1"))
            ProcessSpikeData(data[type], 1);

        waitingForData.Remove(type);
        if (waitingForData.Count == 0)
        {
            // All data acquired, flag that task can start replaying
            Debug.Log("All data loaded");
            taskLoadedSource.SetResult(true);
        }

    }

    /// <summary>
    /// Run through the spike times array and save out timestamp of the 10% quantiles. Also save the max value
    /// This saves time when the user changes what time point they are in time
    /// </summary>
    private void ProcessSpikeData(Array stData, int probe)
    {
        double[] st = new double[stData.Length];
        stData.CopyTo(st, 0);

        maxTime[probe] = (float)st.Max();

        int[] quantIdxs = new int[quantiles.Length];
        float[] quantTimes = new float[quantiles.Length];

        int prevIndex = 0;
        for (int i = 0; i < quantiles.Length; i++)
        {
            float quantile = quantiles[i];
            quantTimes[i] = maxTime[probe] * quantile;

            while ((prevIndex < st.Length) && (st[prevIndex] < quantTimes[i]))
                prevIndex++;
            quantIdxs[i] = prevIndex;
        }

        quantileIndexes[probe] = quantIdxs;
        quantileTimes[probe] = quantTimes;
    }

    private void ParseFileURLs(TextAsset fileURLsAsset)
    {
        string[] uriTargets = fileURLsAsset.text.Split('\n');
        int probeNum = int.Parse(uriTargets[1]);
        string pid = uriTargets[2];
        pid = new string(pid.Where(c => !char.IsControl(c)).ToArray());
        pids.Add(pid);

        for (int i = 3; i < uriTargets.Length; i++)
        {
            string uriTarget = uriTargets[i];
            // Check for the various data types, then save this data accordingly

            foreach (string dataType in dataTypes)
            {
                if (uriTarget.Contains(dataType))
                {
                    // If the data type includes spikes then we need to separate by probe0/probe1
                    string dataTypeP = dataType;
                    if (dataType.Contains("spikes"))
                    {
                        dataTypeP += probeNum;
                    }
                    if (!URIs.ContainsKey(dataTypeP))
                    {
                        URIs[dataTypeP] = uriTarget;
                    }
                }
            }
        }
    }

    private async Task<bool> LoadClusters()
    {
        Task<TextAsset>[] loaders = new Task<TextAsset>[pids.Count];
        for (int i = 0; i < pids.Count; i++)
        {
            int pi = i;
            string pid = pids[i];
            Debug.Log("Loading cluster data for " + pid);

            string filename = replayManager.GetAssetPrefix() + "Clusters/" + pid + ".csv";
            AsyncOperationHandle<TextAsset> clusterLoader = Addressables.LoadAssetAsync<TextAsset>(filename);
            clusterLoader.Completed += handle => { ParseClusterCoordinates(pi, handle.Result); };

            loaders[i] = clusterLoader.Task;
        }

        await Task.WhenAll(loaders);

        return true;
    }

    private void ParseClusterCoordinates(int pid, TextAsset coordsAsset)
    {
        List<Dictionary<string, object>> data = CSVReader.ParseText(coordsAsset.text);

        List<Vector3> pidCoords = new List<Vector3>();
        for (int i = 0; i < data.Count; i++)
        {
            Dictionary<string, object> row = data[i];
            pidCoords.Add(new Vector3((float)row["ml"]/1000f, (float)row["ap"] / 1000f, (float)row["dv"] / 1000f));
        }
        Debug.Log("Loaded: " + pidCoords.Count + " neurons for probe: " + pid);
        coords.Add(pid, pidCoords);
    }

    private async Task<bool> LoadVideos()
    {
        string[] videoOpts = { "left", "body", "right" };
        Dictionary<string, VideoClip> videos = new Dictionary<string, VideoClip>();

        foreach (string videoOpt in videoOpts)
        {
            string videoFilename = replayManager.GetAssetPrefix() + "Videos/" + eid + "_" + videoOpt + "_scaled.mp4";
            AsyncOperationHandle<VideoClip> videoLoader = Addressables.LoadAssetAsync<VideoClip>(videoFilename);

            string timeFilename = replayManager.GetAssetPrefix() + "Videos/" + eid + "_" + videoOpt + "_times.txt";
            AsyncOperationHandle<TextAsset> timeLoader = Addressables.LoadAssetAsync<TextAsset>(timeFilename);

            await Task.WhenAll(new Task[] { videoLoader.Task, timeLoader.Task });

            // when finished, parse this data
            videos.Add(videoOpt, videoLoader.Result);
            videoTimes.Add(videoOpt, float.Parse(timeLoader.Result.text));
        }

        replayManager.SetVideoData(videos["left"], videos["body"], videos["right"]);

        return true;
    }

    private async Task<bool> Load3DPaw()
    {
        string fname_l = replayManager.GetAssetPrefix() + "Videos/points3d_l.csv";
        Debug.Log("loading: " + fname_l);
        AsyncOperationHandle<TextAsset> leftPawLoader = Addressables.LoadAssetAsync<TextAsset>(fname_l);
        leftPawLoader.Completed += handle => { ParsePawCoordinates(true, handle.Result); };

        string fname_r = replayManager.GetAssetPrefix() + "Videos/points3d_r.csv";
        Debug.Log("loading: " + fname_r);
        AsyncOperationHandle<TextAsset> rightPawLoader = Addressables.LoadAssetAsync<TextAsset>(fname_r);
        rightPawLoader.Completed += handle => { ParsePawCoordinates(false, handle.Result); };

        await Task.WhenAll(new Task[] { leftPawLoader.Task, rightPawLoader.Task });

        return true;
    }

    private void ParsePawCoordinates(bool left, TextAsset text)
    {
        List<Dictionary<string, object>> pawData = CSVReader.ParseText(text.text);
        if (left)
            paw3DL = new Vector3[pawData.Count];
        else
            paw3DR = new Vector3[pawData.Count];

        for (int i = 0; i < pawData.Count; i++)
        {
            Dictionary<string, object> row = pawData[i];

            float x = (float)row["x"];
            float y = (float)row["y"];
            float z = (float)row["z"];

            if (left)
                paw3DL[i] = new Vector3(x, y, z);
            else
                paw3DR[i] = new Vector3(x, y, z);
        }

        if (left)
            Debug.Log("Loaded " + paw3DL.Length + " points for left");
        else
            Debug.Log("Loaded " + paw3DR.Length + " points for right");
    }

    public Vector3 GetPaw(int index, bool left)
    {
        if (index < 0)
            return Vector3.zero;
        if (left)
            return paw3DL[index];
        else
            return paw3DR[index];
    }
}
