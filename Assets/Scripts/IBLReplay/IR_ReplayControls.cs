using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IR_ReplayControls : MonoBehaviour
{
    [SerializeField] Button slowButton;
    [SerializeField] Button playButton;
    [SerializeField] Button fastButton;
    [SerializeField] Button pauseButton;
    [SerializeField] Button stopButton;

    public void SetControlInteraction(bool state)
    {
        slowButton.interactable = state;
        playButton.interactable = state;
        fastButton.interactable = state;
        pauseButton.interactable = state;
        stopButton.interactable = state;
    }
}
