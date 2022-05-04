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
    private ExperimentManager emanager;
    private VisualStimulusManager vsmanager;
    private NeuronEntityManager nemanager;
    private IR_IBLReplayManager replayManager;

    private GameObject _uiPanel;
    private TMP_Dropdown uiDropdown;

    private bool dataLoaded = false;

    private Dictionary<int, Entity> neurons;

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

    private float taskTime;

    // Local accessors
    private string cEID;
    private List<int> probes;
    private int[] si; // spike/cluster index
    private Vector2 wheelTime;
    private Vector2 wheelPos; // current wheel pos and next wheel pos
    private int wi; // wheel index
    private int gi; // go cue index
    private int fi; // feedback index
    private int li; // lick index
    private bool videoPlaying;
    VideoPlayer[] videos;

    // DATA
    IR_ReplaySession replaySessionData;

    // OTHER
    const float scale = 1000;
    private SpikingComponent spikedComponent;

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
        emanager = GameObject.Find("main").GetComponent<ExperimentManager>();
        spikedComponent = new SpikingComponent { spiking = 1f };
    }

    public void SetSession(IR_ReplaySession newSessionData)
    {
        replaySessionData = newSessionData;
        SetupTask();
    }

    public void UpdateTime()
    {
        float seconds = TaskTime();

        float displayMilliseconds = (seconds % 1) * 1000;
        float displaySeconds = seconds % 60;
        float displayMinutes = (seconds / 60) % 60;
        float displayHours = (seconds / 3600) % 24;

        GameObject replayText = GameObject.Find("Replay_Time");
        if (replayText)
            replayText.GetComponent<TextMeshProUGUI>().text = string.Format("{0:00}h:{1:00}m:{2:00}.{3:000}", displayHours, displayMinutes, displaySeconds, displayMilliseconds);
    }

    private void SetupTask()
    {
        // reset indexes
        taskTime = 0f;
        wi = 0;
        gi = 0;
        fi = 0;
        li = 0;
        videoPlaying = false;

        probes = new List<int>();



        foreach (int probe in sessionCluUUIDIdxs[cEID].Keys)
        {
            probes.Add(probe);
            // Make the actual probes visible
            AddVisualProbe(trajKey, probe);
        }
        si = new int[probes.Count];
        Debug.Log("Found " + probes.Count + " probes in this EID");

        foreach (VideoPlayer video in videos)
        {
            video.Prepare();
        }
    }

    private void AddVisualProbe(string eid, int pid)
    {
        Vector3 mlapdv = trajectoryData[eid][pid][0] / scale;
        tips[pid].localPosition = new Vector3(-mlapdv.x, -mlapdv.z, mlapdv.y);
        // angle of attack
        Vector3 angles = trajectoryData[eid][pid][1];
        tips[pid].localRotation = Quaternion.Euler(new Vector3(0f,angles.z,angles.y));
        // depth
        tips[pid].Translate(Vector3.down * angles.x / scale);
        tips[pid].gameObject.SetActive(true);
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
        // Get the MLAPDV data 

        neurons = new Dictionary<int, Entity>();
        List<float3> positions = new List<float3>();
        List<Color> replayComp = new List<Color>();

        Color[] colors = { new Color(0.42f, 0.93f, 1f, 0.4f), new Color(1f, 0.78f, 0.32f, 0.4f) };

        foreach (int probe in probes)
        {
            Debug.Log("Initializing components and positions for neurons in probe: " + probe);
            for (int i = 0; i < sessionCluUUIDIdxs[cEID][probe].Length; i++)
            {
                int uuidIdx = sessionCluUUIDIdxs[cEID][probe][i];
                float ml = (float)mlapdv[uuidIdx]["ml"] / scale;
                float dv = (float)mlapdv[uuidIdx]["dv"] / scale;
                float ap = (float)mlapdv[uuidIdx]["ap"] / scale;
                positions.Add(new float3(ml, ap, dv));
                replayComp.Add(colors[probe]);
            }

        }

        List<Entity> neuronEntities = nemanager.AddNeurons(positions, replayComp);

        foreach (int probe in probes)
        {
            int offset = probe == 0 ? 0 : sessionCluUUIDIdxs[cEID][0].Length;
            Debug.Log("Saving entities for probe: " + probe);
            for (int i = 0; i < sessionCluUUIDIdxs[cEID][probe].Length; i++)
            {
                int uuidIdx = sessionCluUUIDIdxs[cEID][probe][i];
                neurons.Add(uuidIdx, neuronEntities[offset+i]);
            }
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
        if (TaskLoaded() && TaskRunning())
        {

            if (!dataLoaded)
            {
                SetupTask();
                LoadNeurons();
            }
            else
            {
                // If the video is not playing yet
                if (!videoPlaying)
                {
                    if (taskTime >= 8.6326566f)
                    {
                        Debug.Log("Starting videos");
                        foreach (VideoPlayer video in videos)
                        {
                            video.Play();
                        }
                        videoPlaying = true;
                    }
                }

                // Play the current spikes
                taskTime += Time.deltaTime;
                UpdateTime();

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
                    wheelTime = new Vector2((float)(double)sessionData[cEID]["wheel.timestamps"].GetValue(wi), (float)(double)sessionData[cEID]["wheel.timestamps"].GetValue(wi + 1));
                    wheelPos = new Vector2((float)(double)sessionData[cEID]["wheel.position"].GetValue(wi), (float)(double)sessionData[cEID]["wheel.position"].GetValue(wi + 1));
                    float dwheel = (wheelPos.y - wheelPos.x) * -rad2mm2deg;
                    stimPosDeg += new Vector2(dwheel, dwheel);

                    // Move stimuli
                    // Freeze stimuli if they go past zero, or off the screen
                    if (stimL!=null && !stimFrozen)
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
                if (taskTime >= (double)sessionData[cEID]["goCue_times"].GetValue(gi))
                {
                    audmanager.PlayGoTone();
                    // Stimulus shown

                    // Check left or right contrast
                    float conL = (float)(double)sessionData[cEID]["contrastLeft"].GetValue(gi);
                    float conR = (float)(double)sessionData[cEID]["contrastRight"].GetValue(gi);
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
                if (taskTime >= (double)sessionData[cEID]["feedback_times"].GetValue(fi))
                {
                    // Check type of feedback
                    if ((long)sessionData[cEID]["feedbackType"].GetValue(fi) == 1)
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

                // Check if lick time was passed
                if (taskTime >= (double)sessionData[cEID]["lick.times"].GetValue(li))
                {
                    lickBehavior.Lick();

                    li++;
                }
            }
        }
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
        while (taskTime >= (double)sessionData[cEID][ststr].GetValue(si[probe]))
        {
            int clu = (int)(uint)sessionData[cEID][scstr].GetValue(si[probe]);
            int uuid = sessionCluUUIDIdxs[cEID][probe][clu];

            nemanager.SetComponentData(neurons[uuid], spikedComponent);
            spikesThisFrame++;
            si[probe]++;
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
}
