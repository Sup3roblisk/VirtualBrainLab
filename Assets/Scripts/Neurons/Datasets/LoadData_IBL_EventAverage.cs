using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class LoadData_IBL_EventAverage : MonoBehaviour
{
    [SerializeField] private ElectrodeManager elecmanager;
    [SerializeField] private CCFModelControl ccfmodelcontrol;
    [SerializeField] private NeuronEntityManager nemanager;
    [SerializeField] private VolumeDatasetManager vdmanager;
    public Utils util;

    //float scale = 1000;
    int SCALED_LEN = 250;
    int conditions = 4;
    int[] side = { -1, -1, 1, 1 };
    int[] corr = { 1, -1, 1, -1 };

    public string displayMode = "grayscaleFR"; // Options: "spiking", "grayscaleFR, "byRegionFR"

    private float[] spikeRateMap;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Loading Event Average Data...");
        //ParseIBLData_EventAverage();

        AsyncStart();
    }

    private async void AsyncStart()
    {

        await vdmanager.LoadAnnotationDataset(new List<Action> { });
        AnnotationDataset annotationDataset = vdmanager.GetAnnotationDataset();


        Dictionary<string, IBLEventAverageComponent> eventAverageData = new Dictionary<string, IBLEventAverageComponent>();

        // load the UUID information for the event average data
        // ibl/large_files/baseline_1d_clu_avgs
        // ibl/uuid_list
        string uuidListFile = Resources.Load<TextAsset>("Datasets/ibl/uuid_list").ToString();
        string[] uuidList = uuidListFile.Split(char.Parse(","));
        float[] spikeRates = util.LoadBinaryFloatHelper("ibl/large_files/baseline_1d_clu_avgs");


        List<IBLEventAverageComponent> eventAverageComponents = new List<IBLEventAverageComponent>();

        for (var ui = 0; ui < uuidList.Length; ui++)
        {
            string uuid = uuidList[ui];
            FixedList4096Bytes<float> spikeRate = new FixedList4096Bytes<float>();

            for (int i = 0; i < (SCALED_LEN * conditions); i++)
            {
                spikeRate.AddNoResize(spikeRates[(ui * (SCALED_LEN * conditions)) + i]);
            }

            IBLEventAverageComponent eventAverageComponent = new IBLEventAverageComponent();
            eventAverageComponent.spikeRate = spikeRate;
            eventAverageData.Add(uuid, eventAverageComponent);
        }


        // load the UUID and MLAPDV data
        Dictionary<string, float3> mlapdvData = util.LoadIBLmlapdv();

        //spikeRateMap = util.LoadBinaryFloatHelper("ibl/1d_clu_avgs_map");
        //byte[] spikeRates = util.LoadBinaryByteHelper("ibl/1d_clu_avgs_uint8");

        // Figure out which neurons we have both a mlapdv data and an event average dataset
        List<float3> iblPos = new List<float3>();

        foreach (string uuid in eventAverageData.Keys)
        {
            if (mlapdvData.ContainsKey(uuid))
            {
                iblPos.Add(mlapdvData[uuid]);
                eventAverageComponents.Add(eventAverageData[uuid]);
            }
        }
        int n = eventAverageComponents.Count;
        Debug.Log("Num neurons: " + eventAverageComponents.Count);

        /*for (int i = 0; i < n / 100; i++)
        {
            Debug.Log(iblPos[i] + ", " + spikeRates[i * 1000] + ", " + eventAverageComponents[i].spikeRate[0]);
        }*/

        // Add neurons with different components based on the current display mode
        switch (displayMode)
        {
            case "spiking":
                nemanager.AddNeurons(iblPos, eventAverageComponents);
                break;

            case "grayscaleFR":
                float4[] zeroColors = (float4[]) Enumerable.Repeat(new float4(0f, 0f, 0f, 0f), n);
                float4[] maxColors = (float4[]) Enumerable.Repeat(new float4(1f, 1f, 1f, 1f), n);
                nemanager.AddNeurons(iblPos, eventAverageComponents, zeroColors, maxColors);
                break;

            case "byRegionFR":
                float4[] zeroRegionColors = new float4[n];
                float4[] maxRegionColors = new float4[n];
                for (int i = 0; i < n; i++)
                {
                    float3 pos = iblPos[i];
                    //Debug.Log(pos.x);
                    //Debug.Log((int)Math.Round(pos.x * 1000, 0));
                    //Debug.Log(pos);
                    // May have to convert from mlapdv -> apdvml, but let's test first
                    Debug.Log(elecmanager);
                    int posId = annotationDataset.ValueAtIndex((int)Math.Round(pos.y * 1000 / 25, 0),
                                                          (int)Math.Round(pos.z * 1000 / 25, 0),
                                                          (int)Math.Round(pos.x * 1000 / 25, 0));
                    Color posColor = ccfmodelcontrol.GetCCFAreaColor(posId);
                    zeroRegionColors[i] = new float4(posColor.r, posColor.b, posColor.g, 0f);
                    maxRegionColors[i] = new float4(posColor.r, posColor.b, posColor.g, 1f);
                }
                nemanager.AddNeurons(iblPos, eventAverageComponents, zeroRegionColors, maxRegionColors);
                break;

            default:
                Debug.Log("Given mode is invalid");
                break;
        }
    }
}
