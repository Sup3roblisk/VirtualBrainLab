using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class IR_IBLReplayManager : MonoBehaviour
{
    [SerializeField] Networking networking;

    // Start is called before the first frame update
    void Start()
    {
        networking.startHost();

    }
}
