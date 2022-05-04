using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/**
 * Experiment Manager
 * 
 * The experiment manager deals with synchronizing the various visual stimuli and mouse behaviors to create
 * a simulated "experiment" for the user to interact with.
 */
public class ExperimentManager : MonoBehaviour
{
    public VisualStimulusManager vsmanager;
    public AudioManager audmanager;
    public Utils util;
    public ElectrodeManager elecmanager;
    public NeuronEntityManager nemanager;

    public LickBehavior lickBehavior;
    public WheelRotationBehavior wheelRotationBehavior;
    public MouseAIBehavior mouseAI;

    public Button run;
    public Button pause;
    public Button stop;

    //Setting the experiment chooser field will enable the experiment manager
    [SerializeField] private TMP_Dropdown experimentChooser;

    [SerializeField] private GameObject UIPanel;

    private Experiment activeExperiment;

    private List<Experiment> experiments;

    void Awake()
    {
        Time.timeScale = 1.0f;

        experiments = new List<Experiment>();
        experiments.Add(new IBLTask(vsmanager,audmanager,lickBehavior,wheelRotationBehavior, UIPanel));
    }

    // Start is called before the first frame update
    private void Start()
    {
        SetupUI();
    }

    // Update is called once per frame
    void Update()
    {
        if (activeExperiment != null)
        {
            if (activeExperiment.TaskLoaded() && !run.interactable)
            {
                run.interactable = true;
            }

            if (activeExperiment.TaskRunning())
            {
                activeExperiment.TaskUpdate();

                UpdateTime();
            }
        }
    }

    /// <summary>
    /// [TODO] REMOVE THIS!! This should not be hardcoded like this
    /// </summary>
    public IBLTask GetIBLTask()
    {
        return (IBLTask)experiments[0];
    }

    /// <summary>
    /// Setup the experiment UI chooser dropdowns
    /// </summary>
    public void SetupUI()
    {
        if (experimentChooser)
        {
            List<TMP_Dropdown.OptionData> experimentNames = new List<TMP_Dropdown.OptionData>();
            foreach (Experiment exp in experiments)
            {
                experimentNames.Add(new TMP_Dropdown.OptionData(exp.Name()));
            }

            experimentChooser.options = experimentNames;
            experimentChooser.onValueChanged.AddListener(delegate { ChangeExperiment(); });

            ChangeExperiment();
        }
    }

    public void ChangeExperiment()
    {
        if (activeExperiment != null)
        {
            activeExperiment.StopTask();
        }
        activeExperiment = experiments[experimentChooser.value];

        // Set all buttons to not be interactable
        if (activeExperiment.TaskLoaded())
        {
            run.interactable = true;
            pause.interactable = false;
            stop.interactable = false;
        }
        else
        {
            activeExperiment.LoadTask();
            run.interactable = false;
            pause.interactable = false;
            stop.interactable = false;
        }
    }

    public void UpdateTime()
    {
        float seconds = activeExperiment.TaskTime();

        float displayMilliseconds = (seconds % 1) * 1000;
        float displaySeconds = seconds % 60;
        float displayMinutes = (seconds / 60) % 60;
        float displayHours = (seconds / 3600) % 24;

        //private void UpdateTime()
        //{
        //    TimeSpan timeSpan = TimeSpan.FromSeconds(sessionCurrentTime);
        //    string timeText = string.Format("{0:D2}h.{1:D2}m.{2:D2}.{3:D3}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
        //    timerText.SetText(timeText);
        //}
        GameObject replayText = GameObject.Find("Replay_Time");
        if (replayText)
            replayText.GetComponent<TextMeshProUGUI>().text = string.Format("{0:00}h:{1:00}m:{2:00}.{3:000}", displayHours, displayMinutes, displaySeconds, displayMilliseconds);
    }

    /// <summary>
    /// Start running a new experiment
    /// </summary>
    /// <param name="newExperiment"></param>
    public void ChangeExperiment(Experiment newExperiment)
    {
        if (activeExperiment != null)
            activeExperiment.StopTask();

        activeExperiment = newExperiment;
        activeExperiment.RunTask();
    }

    // TIME CONTROLS

    public void ChangeTime(float newTimePerc)
    {

    }

    // BUTTON CONTROLS

    /// <summary>
    /// Continue playing the current experiment
    /// </summary>
    public void Play()
    {
        activeExperiment.RunTask();
        pause.interactable = true;
        stop.interactable = true;
    }

    public void Pause()
    {
        activeExperiment.PauseTask();
        pause.interactable = false;
    }

    public void Stop()
    {
        activeExperiment.StopTask();
        pause.interactable = false;
        stop.interactable = false;
    }

    public void SpeedUp()
    {
        Time.timeScale = Time.timeScale * 2;
        UpdateTimescaleText();
    }

    public void SlowDown()
    {
        Time.timeScale = Time.timeScale / 2;
        UpdateTimescaleText();
    }

    private void UpdateTimescaleText()
    {
        GameObject.Find("Replay_Speed").GetComponent<TextMeshProUGUI>().text = Time.timeScale + "x";
    }
}


public abstract class Experiment
{
    private string _name;
    private bool _taskRunning;
    private bool _taskLoaded;

    public Experiment(string name)
    {
        _name = name;
    }
    public string Name()
    { 
        return _name;
    }

    public bool TaskRunning() { return _taskRunning; }
    public bool TaskLoaded() { return _taskLoaded; }
    public void SetTaskRunning(bool state) { _taskRunning = state; }
    public void SetTaskLoaded(bool state) { _taskLoaded = state; }

    public abstract void LoadTask();

    public abstract void TaskUpdate();

    public abstract void RunTask();

    public abstract void PauseTask();

    public abstract void StopTask();

    public abstract void ChangeTimescale();

    public abstract float TaskTime();

    public abstract void SetTaskTime(float newTime);
}
