using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

public class ReplaySession
{
    IR_IBLReplayManager replayManager;
    Utils util;

    string eid;

    // SESSION DATA
    private static string[] dataTypes = { "spikes.times", "spikes.clusters", "wheel.position",
        "wheel.timestamps", "goCue_times", "feedback_times", "feedbackType",
        "contrastLeft","contrastRight","lick.times"};
    TaskCompletionSource<bool> taskLoadedSource;

    private List<string> waitingForData;
    private Dictionary<string, string> URIs;
    private Dictionary<string, Array> data;
    private List<string> pids;
    private Dictionary<string, List<Vector3>> coords;
    private Dictionary<string, float> videoTimes;
    private Dictionary<int, Vector3[]> trajectories;


    public ReplaySession(string eid, IR_IBLReplayManager replayManager, Utils util, Dictionary<int, Vector3[]> trajectories)
    {
        this.eid = eid;
        this.replayManager = replayManager;
        this.util = util;
        this.trajectories = trajectories;

        waitingForData = new List<string>();
        URIs = new Dictionary<string, string>();
        data = new Dictionary<string, Array>();
        pids = new List<string>();
        coords = new Dictionary<string, List<Vector3>>();
        videoTimes = new Dictionary<string, float>();
    }

    public async Task<bool> LoadAssets()
    {
        await LoadFileURLs();
        await LoadClusters();
        await LoadVideos();

        return true;
    }

    private async Task<bool> LoadFileURLs()
    {
        taskLoadedSource = new TaskCompletionSource<bool>();

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

        return true;
    }

    private void AddSessionData(string type, Array receivedData)
    {
        Debug.Log("Receiving data: " + type + " with data type " + receivedData.GetType());
        data[type] = receivedData;

        waitingForData.Remove(type);
        if (waitingForData.Count == 0)
        {
            // All data acquired, flag that task can start replaying
            taskLoadedSource.SetResult(true);
        }

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
            string pid = pids[i];

            string filename = replayManager.GetAssetPrefix() + "Clusters/" + pid + ".csv";
            AsyncOperationHandle<TextAsset> clusterLoader = Addressables.LoadAssetAsync<TextAsset>(filename);
            clusterLoader.Completed += handle => { ParseClusterCoordinates(pid, handle.Result); };

            loaders[i] = clusterLoader.Task;
        }

        await Task.WhenAll(loaders);

        return true;
    }

    private void ParseClusterCoordinates(string pid, TextAsset coordsAsset)
    {
        List<Dictionary<string, object>> data = CSVReader.ParseText(coordsAsset.text);

        List<Vector3> pidCoords = new List<Vector3>();
        for (int i = 0; i < data.Count; i++)
        {
            Dictionary<string, object> row = data[i];
            pidCoords.Add(new Vector3((float)row["ml"], (float)row["ap"], (float)row["dv"]));
        }
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

}
