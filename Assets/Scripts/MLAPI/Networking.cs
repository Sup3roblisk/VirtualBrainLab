using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class Networking : MonoBehaviour
{
    public ElectrodeManager emanager;
    [SerializeField] private GameObject probeAddPanel;

    GameObject serverPanel;

    // Start is called before the first frame update
    void Start()
    {
        serverPanel = GameObject.Find("ServerPanel");

        // Add callbacks to trigger the onClient events
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoins;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            serverPanel.SetActive(false);
            emanager.ServerSideSetup();
        }
        if (NetworkManager.Singleton.IsClient)
        {
            serverPanel.SetActive(false);
            // Don't load datsets on client
        }

        startHost();
    }

    public void startServer()
    {
        NetworkManager.Singleton.StartServer();
        if (serverPanel != null)
            serverPanel.SetActive(false);
        LateStartServer();
    }

    public void startClient()
    {
        NetworkManager.Singleton.StartClient();
        if (serverPanel != null)
            serverPanel.SetActive(false);
        // Don't load datsets on client
        LateStartClient();
    }

    public void startHost()
    {
        NetworkManager.Singleton.StartHost();
        if (serverPanel != null)
            serverPanel.SetActive(false);
        LateStartServer();
        LateStartClient();
    }

    void LateStartServer()
    {
        emanager.ServerSideSetup();
    }

    private async void LateStartClient()
    {
        // Call things that need to run Start() but only after confirming this is a client build
        GameObject main = GameObject.Find("main");
        main.GetComponent<CCFModelControl>().LateStart(true);
        await main.GetComponent<CCFModelControl>().GetDefaultLoaded();
        List<CCFTreeNode> defaultNodes = main.GetComponent<CCFModelControl>().GetDefaultLoadedNodes();
        foreach (CCFTreeNode node in defaultNodes)
        {
            node.SetNodeModelVisibility(true);
            node.GetNodeTransform().localPosition = Vector3.zero;
            node.GetNodeTransform().localRotation = Quaternion.identity;
            node.SetNodeModelVisibility(true);
        }
        // PlayerManager has a ClientStart() function you can call here, if needed
        probeAddPanel.SetActive(true);
    }
}
