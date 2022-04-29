using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class IR_RawDataHandler : MonoBehaviour
{
    RawImage rawImage;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }
    // Start is called before the first frame update
    void Start()
    {

        // let's try pulling a test texture from Cyrille's database

        //StartCoroutine(GetTexture());
    }

    // Update is called once per frame
    void Update()
    {
        //float offset = Time.time * scrollSpeed;
        //GameObject.Find("ReplayRawBody").GetComponent<RawImage>().material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
    }


    //IEnumerator GetTexture()
    //{
    //    yield return new WaitForSeconds(5);
    //    float t0 = Time.realtimeSinceStartup;
    //    UnityWebRequest www = UnityWebRequestTexture.GetTexture("http://viz.internationalbrainlab.org/0851db85-2889-4070-ac18-a40e8ebd96ba/raw/2?dt=1");
    //    yield return www.SendWebRequest();
    //    Debug.Log(Time.realtimeSinceStartup - t0);

    //    t0 = Time.realtimeSinceStartup;
    //    www = UnityWebRequestTexture.GetTexture("http://viz.internationalbrainlab.org/0851db85-2889-4070-ac18-a40e8ebd96ba/raw/2?dt=2");
    //    yield return www.SendWebRequest();
    //    Debug.Log(Time.realtimeSinceStartup - t0);

    //    t0 = Time.realtimeSinceStartup;
    //    www = UnityWebRequestTexture.GetTexture("http://viz.internationalbrainlab.org/0851db85-2889-4070-ac18-a40e8ebd96ba/raw/2?dt=4");
    //    yield return www.SendWebRequest();
    //    Debug.Log(Time.realtimeSinceStartup - t0);

    //    t0 = Time.realtimeSinceStartup;
    //    www = UnityWebRequestTexture.GetTexture("http://viz.internationalbrainlab.org/0851db85-2889-4070-ac18-a40e8ebd96ba/raw/2?dt=8");
    //    yield return www.SendWebRequest();
    //    Debug.Log(Time.realtimeSinceStartup - t0);
    //    Texture myTexture = DownloadHandlerTexture.GetContent(www);

    //    //myTexture.

    //    rawImage.color = Color.white;
    //    rawImage.texture = myTexture;
    //}

    private float scrollSpeed = 1f;
}
