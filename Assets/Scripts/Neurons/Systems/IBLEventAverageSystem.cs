using System;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;


// Note that all systems that modify neuron color need this -- otherwise neuron color *could*
// get reset on the same frame when the FPS is low
[UpdateAfter(typeof(NeuronSpikingSystem))]
public partial class IBLEventAverageSystem : SystemBase
{
    IBLTask iblTask;
    //private float trialTimeIndex;
    private NeuronEntityManager nemanager;

    protected override void OnStartRunning()
    {
        //trialTimeIndex = 0;
        iblTask = GameObject.Find("main").GetComponent<ExperimentManager>().GetIBLTask();
        nemanager = GameObject.Find("main").GetComponent<NeuronEntityManager>();
    }

    protected override void OnUpdate()
    {

        //trialTimeIndex += 0.1f;
        float deltaTime = Time.DeltaTime;
        double curTime = Time.ElapsedTime;
        bool corr = iblTask.GetCorrect();
        int curIndex = iblTask.GetTimeIndex();
        float smallScale = nemanager.GetNeuronScale();

        Debug.Log(curIndex);
        int trialStartIdx;
        if (iblTask.GetSide() == -1)
        {
            trialStartIdx = corr ? 0 : 250;
        }
        else
        {
            trialStartIdx = corr ? 500 : 750;
        }
        curIndex += trialStartIdx;

        //int trialTimeIdx = trialStartIdx + iblTask.GetTimeIndex();

        double max = 0.0;
        // Update spiking neurons
        Entities
            .ForEach((ref Scale scale, ref MaterialColor color, ref SpikingComponent spikeComp, ref SpikingRandomComponent randComp, in IBLEventAverageComponent eventAverage) =>
            {
                float neuronFiringRate = eventAverage.spikeRate.ElementAt(curIndex) * deltaTime;

                // check if a random value is lower than this (Poisson spiking)
                if (randComp.rand.NextFloat(1f) < neuronFiringRate)
                {
                    spikeComp.spiking = 1f;
                    color.Value = new float4(1f, 1f, 1f, 1f);
                    scale.Value = 0.09f;
                }

            }).ScheduleParallel(); // .Run();

        // Update lerping neurons
        Entities
            .ForEach((ref MaterialColor color, ref LerpColorComponent lerpColor, in IBLEventAverageComponent eventAverage) =>
            {
                float4 maxFRColor = lerpColor.maxColor;
                float4 zeroFRColor = lerpColor.zeroColor;
                float curPercent = eventAverage.spikeRate[curIndex] / 100.0f;
                color.Value = new float4(Mathf.Lerp(zeroFRColor.x, maxFRColor.x, curPercent),
                                         Mathf.Lerp(zeroFRColor.y, maxFRColor.y, curPercent),
                                         Mathf.Lerp(zeroFRColor.z, maxFRColor.z, curPercent),
                                         Mathf.Lerp(zeroFRColor.w, maxFRColor.w, curPercent));

            }).ScheduleParallel(); // .Run();
    }
}
