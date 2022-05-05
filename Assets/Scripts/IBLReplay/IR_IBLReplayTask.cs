using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Video;
using Unity.Transforms;

/// <summary>
/// IBL REPLAY TASK
/// 
/// This class represents the user interface and functionality to replay a session of an IBL experiment
/// all of the data is loaded and contained in an IR_ReplaySession object, while this class handles the
/// UI and visuals.
/// 
/// </summary>
public class IR_IBLReplayTask : Experiment
{
    private Utils util;
    private Transform wheelTransform;
    private AudioManager audmanager;
    private LickBehavior lickBehavior;
    private VisualStimulusManager vsmanager;
    private NeuronEntityManager nemanager;
    private IR_IBLReplayManager replayManager;

    private GameObject _uiPanel;
    private TMP_Dropdown uiDropdown;

    private bool dataLoaded = false;

    // NEURONS
    private int[] probeEntCount;
    private Dictionary<int,List<Entity>> neuronEntities;

    // PROBES
    private List<Transform> tips;

    // STIMULUS
    private GameObject stimL;
    private GameObject stimR;
    private float stimAz = 20;
    private Vector2 stimAzMinMax = new Vector2(0, 40);
    private bool stimFrozen;
    private float rad2deg = 180 / Mathf.PI;
    private float rad2mm2deg = (196 / 2) / Mathf.PI * 4; // wheel circumference in mm * 4 deg / mm
    private Vector2 stimPosDeg; // .x = left, .y = right

    // PAW
    private Transform pawL;
    private Transform pawR;
    private float pawFPS = 60.12f;

    private float taskTime;

    // Local accessors
    private List<int> probes;
    private int[] spikeIdx; // spike/cluster index
    private Vector2 wheelTime;
    private Vector2 wheelPos; // current wheel pos and next wheel pos
    private int wi; // wheel index
    private int gi; // go cue index
    private int fi; // feedback index
    private int li; // lick index
    private int frameIdx; // video frame index
    private int pawIdx; // paw index

    // VIDEO
    private bool videoPlaying;
    VideoPlayer[] videos;
    private float videoFPS = 30.15f;

    // DATA
    IR_ReplaySession replaySessionData;

    // UI
    GameObject replayText;


    // OTHER
    const float scale = 1000;
    private SpikingComponent spikedComponent;
    private Scale scaledComponent;

    public IR_IBLReplayTask(Utils util, Transform wheelTransform, 
        AudioManager audmanager, LickBehavior lickBehavior, 
        VisualStimulusManager vsmanager, NeuronEntityManager nemanager,
        List<Transform> probeTips) : base("replay")
    {
        this.util = util;
        this.wheelTransform = wheelTransform;
        this.audmanager = audmanager;
        this.lickBehavior = lickBehavior;
        this.vsmanager = vsmanager;
        this.nemanager = nemanager;
        this.tips = probeTips;

        // Setup variables
        spikedComponent = new SpikingComponent { spiking = 1f };
        scaledComponent = new Scale { Value = 0.4f };

        replayManager = GameObject.Find("main").GetComponent<IR_IBLReplayManager>();

        // UI
        replayText = GameObject.Find("Replay_Time");

        neuronEntities = new Dictionary<int, List<Entity>>();

        (pawL, pawR) = replayManager.GetPawTransforms();
    }

    public void SetSession(IR_ReplaySession newSessionData)
    {
        replaySessionData = newSessionData;
        SetupTask();
        LoadNeurons();
    }

    public void UpdateTimeText()
    {
        float seconds = TaskTime();

        float displayMilliseconds = (seconds % 1) * 1000;
        float displaySeconds = seconds % 60;
        float displayMinutes = (seconds / 60) % 60;
        float displayHours = (seconds / 3600) % 24;

        replayText.GetComponent<TextMeshProUGUI>().text = string.Format("{0:00}h:{1:00}m:{2:00}.{3:000}", displayHours, displayMinutes, displaySeconds, displayMilliseconds);
    }

    private void SetupTask()
    {
        // reset indexes
        taskTime = 0f;
        spikeIdx = new int[] { 0, 0 };
        wi = 0;
        gi = 0;
        fi = 0;
        li = 0;
        pawIdx = 0;
        videoPlaying = false;

        probes = new List<int>();

        List<string> pids = replaySessionData.GetPIDs();
        for (int pi = 0; pi < pids.Count; pi++)
        {
            probes.Add(pi);
            Vector3[] probeData = replaySessionData.GetProbeTrajectory(pi);
            AddVisualProbe(pi, probeData[0], probeData[1]);
        }
        Debug.Log("Found " + probes.Count + " probes in this EID");

        // [TODO] handle videos?
        //foreach (VideoPlayer video in videos)
        //{
        //    video.Prepare();
        //}
    }

    /// <summary>
    /// Move the probe GameObjects to match the position information in the current session
    /// </summary>
    /// <param name="pi">Probe int id</param>
    /// <param name="mlapdv">Vector3 coordinate in mlapdv (from IBL, unscaled)</param>
    /// <param name="angles">Vector3 angle information (phi/theta/spin)</param>
    private void AddVisualProbe(int pi, Vector3 mlapdv, Vector3 angles)
    {
        mlapdv = mlapdv / scale;
        tips[pi].localPosition = new Vector3(-mlapdv.x, -mlapdv.z, mlapdv.y);
        tips[pi].localRotation = Quaternion.Euler(new Vector3(0f,angles.z,angles.y));
        // depth
        tips[pi].Translate(Vector3.down * angles.x / scale);
        tips[pi].gameObject.SetActive(true);
    }

    private void ClearVisualProbes()
    {
        foreach (Transform t in tips)
        {
            t.gameObject.SetActive(false);
        }
    }

    public void LoadNeurons()
    {
        List<float3> positions = new List<float3>();
        List<Color> replayComp = new List<Color>();
        Color[] colors = { new Color(0.42f, 0.93f, 1f, 0.4f), new Color(1f, 0.78f, 0.32f, 0.4f) };


        // Get the MLAPDV data 
        foreach (int probe in probes)
        {
            List<Vector3> coords = replaySessionData.GetMLAPDVCoords(probe);

            for (int i = 0; i < coords.Count; i++)
            {
                positions.Add(new float3(coords[i].x, coords[i].y, coords[i].z));
                replayComp.Add(colors[probe]);
            }

            neuronEntities[probe] = nemanager.AddNeurons(positions, replayComp);
        }


        dataLoaded = true;
    }

    public override float TaskTime()
    {
        return taskTime;
    }

    public override void RunTask()
    {
        SetTaskRunning(true);
    }
    public override void PauseTask()
    {
        Debug.LogWarning("Pause not implemented, currently stops task");
        StopTask();
    }

    public override void StopTask()
    {
        ClearVisualProbes();
        SetTaskRunning(false);
    }

    public override void TaskUpdate()
    {
        // Update time
        taskTime += Time.deltaTime;
        UpdateTimeText();

        // Set the frame index and update the videos
        frameIdx = (int) ((taskTime - replaySessionData.GetVideoStartTime("left")) * videoFPS);
        replayManager.SetVideoFrame(frameIdx);

        // Play the current spikes
        int spikesThisFrame = 0;
        foreach (int probe in probes)
        {
            spikesThisFrame += PlaySpikes(probe);
        }

        // TODO: setting a max of 100 is bad for areas that have high spike rates
        // also this creates sound issues if your framerate is low
        if (UnityEngine.Random.value < (spikesThisFrame / 100))
        {
            Debug.LogWarning("Spiking but emanager has no queue for spikes anymore");
            //emanager.QueueSpike();
        }

        // Increment the wheel index if time has passed the previous value
        while (taskTime >= wheelTime.y)
        {
            wi++;
            wheelTime = new Vector2((float)(double)replaySessionData.GetData("wheel.timestamps").GetValue(wi), (float)(double)replaySessionData.GetData("wheel.timestamps").GetValue(wi+1));
            wheelPos = new Vector2((float)(double)replaySessionData.GetData("wheel.position").GetValue(wi), (float)(double)replaySessionData.GetData("wheel.position").GetValue(wi+1));
            float dwheel = (wheelPos.y - wheelPos.x) * -rad2mm2deg;
            stimPosDeg += new Vector2(dwheel, dwheel);

            // Move stimuli
            // Freeze stimuli if they go past zero, or off the screen
            if (stimL != null && !stimFrozen)
            {
                vsmanager.SetStimPositionDegrees(stimL, new Vector2(stimPosDeg.x, 0));
                if (stimPosDeg.x > stimAzMinMax.x || stimPosDeg.x < -stimAzMinMax.y) { stimFrozen = true; }
            }
            if (stimR != null && !stimFrozen)
            {
                vsmanager.SetStimPositionDegrees(stimR, new Vector2(stimPosDeg.y, 0));
                if (stimPosDeg.y < stimAzMinMax.x || stimPosDeg.y > stimAzMinMax.y) { stimFrozen = true; }
            }
        }
        float partialTime = (taskTime - wheelTime.x) / (wheelTime.y - wheelTime.x);
        // note the negative, because for some reason the rotations are counter-clockwise
        wheelTransform.localRotation = Quaternion.Euler(-rad2deg * Mathf.Lerp(wheelPos.x, wheelPos.y, partialTime), 0, 0);

        // Check if go cue time was passed
        if (taskTime >= (double)replaySessionData.GetData("goCue_times").GetValue(gi))
        {
            audmanager.PlayGoTone();
            // Stimulus shown

            // Check left or right contrast
            float conL = (float)(double)replaySessionData.GetData("contrastLeft").GetValue(gi);
            float conR = (float)(double)replaySessionData.GetData("contrastRight").GetValue(gi);
            stimPosDeg = new Vector2(-1 * stimAz, stimAz);

            stimFrozen = false;
            // We'll do generic stimulus checks, even though the task is detection so that
            // if later someone does 2-AFC we are ready
            if (conL > 0)
            {
                Debug.Log("Adding left stimulus");
                stimL = vsmanager.AddNewStimulus("gabor");
                stimL.GetComponent<VisualStimulus>().SetScale(5);
                // Set the position properly
                vsmanager.SetStimPositionDegrees(stimL, new Vector2(stimPosDeg.x, 0));
                vsmanager.SetContrast(stimL, conL);
            }

            if (conR > 0)
            {
                Debug.Log("Adding right stimulus");
                stimR = vsmanager.AddNewStimulus("gabor");
                stimR.GetComponent<VisualStimulus>().SetScale(5);
                // Set the position properly
                vsmanager.SetStimPositionDegrees(stimR, new Vector2(stimPosDeg.y, 0));
                vsmanager.SetContrast(stimR, conR);
            }
            gi++;
        }

        // Check if feedback time was passed
        if (taskTime >= (double)replaySessionData.GetData("feedback_times").GetValue(fi))
        {
            // Check type of feedback
            if ((long)replaySessionData.GetData("feedbackType").GetValue(fi) == 1)
            {
                // Reward + lick
                lickBehavior.Drop();
            }
            else
            {
                // Play white noise
                audmanager.PlayWhiteNoise();
            }
            stimFrozen = true;

            if (stimL != null) { vsmanager.DelayedDestroy(stimL, 1); }
            if (stimR != null) { vsmanager.DelayedDestroy(stimR, 1); }
            fi++;
        }

        //Check if lick time was passed
        if (taskTime >= (double)replaySessionData.GetData("licks.times").GetValue(li))
        {
            lickBehavior.Lick();

            li++;
        }


        // Handle paw positions
        pawIdx = (int) ((taskTime - replaySessionData.GetVideoStartTime("left")) * pawFPS);
        pawL.localPosition = replayManager.ConvertPawToWorld(replaySessionData.GetPaw(pawIdx, true));
        pawR.localPosition = replayManager.ConvertPawToWorld(replaySessionData.GetPaw(pawIdx, false));

        UpdateRawData();
    }

    private int PlaySpikes(int probe)
    {
        int spikesThisFrame = 0;
        string ststr = "";
        string scstr = "";
        if (probe==0)
        {
            ststr = "spikes.times0";
            scstr = "spikes.clusters0";
        } else if (probe==1)
        {
            ststr = "spikes.times1";
            scstr = "spikes.clusters1";
        } else
        {
            Debug.LogError("Probe *should* not exist!! Got " + probe + " expected value 0/1");
        }

        while (taskTime >= (double)replaySessionData.GetData(ststr).GetValue(spikeIdx[probe]))
        {
            int clu = (int)(uint)replaySessionData.GetData(scstr).GetValue(spikeIdx[probe]);

            nemanager.SetNeuronSpiking(neuronEntities[probe][clu], spikedComponent, scaledComponent);
            spikesThisFrame++;
            spikeIdx[probe]++;
        }

        return spikesThisFrame;
    }

    public override void LoadTask()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Handle a change in the Time.timeScale value
    /// </summary>
    public override void ChangeTimescale()
    {
        replayManager.UpdateVideoSpeed();
    }

    public override void SetTaskTime(float newTime)
    {
        throw new NotImplementedException();
    }

    private void UpdateRawData()
    {
        
    }
}
