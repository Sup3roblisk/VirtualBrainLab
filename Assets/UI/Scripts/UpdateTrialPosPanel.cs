using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Should be attached to TrialPosPanel
public class UpdateTrialPosPanel : MonoBehaviour
{
    private RectTransform trialPosLineTransform;
    private float trialPosPanelWidth;
    private IBLTask iblTask;

    // Start is called before the first frame update
    void Start()
    {
        iblTask = GameObject.Find("main").GetComponent<ExperimentManager>().GetIBLTask();
        RectTransform trialPosPanelBody = (RectTransform) this.GetComponent<RectTransform>().Find("Body");
        trialPosLineTransform = (RectTransform) trialPosPanelBody.Find("CurTrialPosLine");
        trialPosPanelWidth = trialPosPanelBody.rect.width;
    }

    // Update is called once per frame
    void Update()
    {
        int curIndex = iblTask.GetTimeIndex();
        float curX = trialPosPanelWidth * (curIndex / 250.0f);
        //Debug.Log(curIndex + " " + curX);
        trialPosLineTransform.anchoredPosition = new Vector2(curX, trialPosLineTransform.anchoredPosition.y);
    }
}
