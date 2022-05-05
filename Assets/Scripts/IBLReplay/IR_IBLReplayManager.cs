using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.Video;

public class IR_IBLReplayManager : MonoBehaviour
{
    [SerializeField] CCFModelControl modelControl;
    [SerializeField] Utils util;

    // Managers
    [SerializeField] AudioManager audmanager;
    [SerializeField] LickBehavior lickBehavior;
    [SerializeField] VisualStimulusManager vsmanager;
    [SerializeField] NeuronEntityManager nemanager;

    // Gameobjects
    [SerializeField] Transform wheelTransform;
    [SerializeField] Transform pawLTransform;
    [SerializeField] Transform pawRTransform;
    [SerializeField] Transform pawCenterTargetTransform;
    [SerializeField] float pawScale = 5f;

    [SerializeField] GameObject loadingGO;

    // Probes
    [SerializeField] GameObject iblReplayProbesGO;
    List<Transform> tips;

    // UI Elemetns
    [SerializeField] TMP_Dropdown sessionDropdown;
    [SerializeField] VideoPlayer leftPlayer;
    [SerializeField] VideoPlayer bodyPlayer;
    [SerializeField] VideoPlayer rightPlayer;
    [SerializeField] Button playButton;
    [SerializeField] Button slowButton;
    [SerializeField] Button fastbutton;
    [SerializeField] Button pauseButton;
    [SerializeField] Button stopButton;

    // Addressable Assets
    [SerializeField] string assetPrefix;
    [SerializeField] AssetReference sessionAsset;

    // Sessions
    string[] sessions;
    Dictionary<string, IR_ReplaySession> loadedSessions;
    IR_ReplaySession activeSession;

    // Code that runs the actual task
    IR_IBLReplayTask activeTask;

    private void Awake()
    {
        loadedSessions = new Dictionary<string, IR_ReplaySession>();
        LoadSessionInfo();

        if (iblReplayProbesGO)
        {
            // get probe tips and inactivate them
            Transform p0tip = iblReplayProbesGO.transform.Find("probe0_tip");
            p0tip.gameObject.SetActive(false);
            Transform p1tip = iblReplayProbesGO.transform.Find("probe1_tip");
            p1tip.gameObject.SetActive(false);
            tips = new List<Transform>();
            tips.Add(p0tip); tips.Add(p1tip);
        }

        activeTask = new IR_IBLReplayTask(util, wheelTransform, audmanager, lickBehavior, vsmanager, nemanager, tips);
    }

    // Start is called before the first frame update
    void Start()
    {
        modelControl.LateStart(true);
        SetupCCFModels();
    }

    private void Update()
    {
        if (activeTask.TaskLoaded() && activeTask.TaskRunning())
            activeTask.TaskUpdate();
    }

    public (Transform, Transform) GetPawTransforms()
    {
        return (pawLTransform, pawRTransform);
    }

    public void SetLoading(bool state)
    {
        loadingGO.SetActive(state);
    }

    public string GetAssetPrefix()
    {
        return assetPrefix;
    }

    public Vector3 ConvertPawToWorld(Vector3 relativePawPosition)
    {
        //return pawCenterTargetTransform.position + relativePawPosition * pawScale;

        return relativePawPosition * pawScale;
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

    public async void SetupCCFModels()
    {
        await modelControl.GetDefaultLoaded();
        foreach (CCFTreeNode node in modelControl.GetDefaultLoadedNodes())
        {
            node.GetNodeTransform().localPosition = Vector3.zero;
            node.GetNodeTransform().localRotation = Quaternion.identity;
            node.SetNodeModelVisibility(true);
        }
    }

    public void ChangeSession(int newSessionID)
    {
        string eid = sessions[newSessionID - 1];
        Debug.Log("(RManager) Changing session to: " + eid);
        LoadSession(eid);
    }

    public async void LoadSession(string eid)
    {
        if (eid.Length == 0)
            return;

        if (loadedSessions.ContainsKey(eid))
            activeSession = loadedSessions[eid];
        else
        {
            Debug.Log("Loading new session data");
            IR_ReplaySession session = new IR_ReplaySession(eid, this, util);

            await session.LoadAssets();

            loadedSessions.Add(eid, session);

            activeSession = session;
            activeTask.SetSession(activeSession);
            activeTask.SetTaskLoaded(true);
        }
    }

    public void SetVideoData(VideoClip left, VideoClip body, VideoClip right)
    {
        leftPlayer.clip = left;
        leftPlayer.Prepare();
        bodyPlayer.clip = body;
        bodyPlayer.Prepare();
        rightPlayer.clip = right;
        rightPlayer.Prepare();
    }

    public void SetVideoFrame(int frame)
    {
        if (frame > 0)
        {
            leftPlayer.frame = frame;
            leftPlayer.Play();
            bodyPlayer.frame = frame;
            bodyPlayer.Play();
            rightPlayer.frame = frame;
            rightPlayer.Play();
        }
    }

    public void UpdateVideoSpeed()
    {
        //leftPlayer.playbackSpeed = Time.timeScale;
        //bodyPlayer.playbackSpeed = Time.timeScale;
        //rightPlayer.playbackSpeed = Time.timeScale;
    }

    public void JumpTime(float jumpTimePerc)
    {

    }

    public void PlayTask()
    {
        Debug.Log("Pressed play");
        activeTask.RunTask();
    }

    public void PauseTask()
    {
        Debug.Log("Pressed pause");
        activeTask.PauseTask();
    }

    public void StopTask()
    {
        Debug.Log("Pressed stop");
        activeTask.StopTask();
    }

    public void SpeedupTask()
    {
        Debug.Log("Pressed ff");
        Time.timeScale = Time.timeScale * 2f;
        UpdateVideoSpeed();
        UpdateReplaySpeedText();
    }

    public void SlowdownTask()
    {
        Debug.Log("Pressed slow");
        Time.timeScale = Time.timeScale / 2f;
        UpdateVideoSpeed();
        UpdateReplaySpeedText();
    }

    private void UpdateReplaySpeedText()
    {
        GameObject.Find("Replay_Speed").GetComponent<TMP_Text>().text = Time.timeScale + "x";
    }
}
