using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LatencyMonitor : MonoBehaviour
{

    public int latency; // in ms
    public int maxLatency = 1000;
    public int minLatency = 0;
    public VRSlider slider;
    public RecorderReplayer recorderReplayer;
    public TMPro.TextMeshProUGUI tmpText;

    // Start is called before the first frame update
    void Start()
    {
        LoadLatency();
        
        slider.OnGrasp += SliderOnGrasp;
        slider.OnRelease += SliderOnRelease;
        slider.OnSliderChange += SliderOnSliderChange;
    }

    private void SliderOnSliderChange(object sender, float e)
    {
        latency = (int)(e * maxLatency);
        tmpText.text = latency.ToString();
    }

    private void SliderOnGrasp(object sender, EventArgs e)
    {
        if (recorderReplayer.roomClient.Me["creator"] == "1")
        {
            if (recorderReplayer.replaying && recorderReplayer.play)
            {
                recorderReplayer.menuRecRep.PlayPauseReplay();
            }
        }
    }

    private void SliderOnRelease(object sender, EventArgs e)
    {
        if (recorderReplayer.roomClient.Me["creator"] == "1")
        {
            if (recorderReplayer.replaying && !recorderReplayer.play)
            {
                Debug.Log("SliderOnRelease latency: " + latency);
                recorderReplayer.audioRecRep.LATENCY = latency;
                recorderReplayer.audioRecRep.SetLatencies(latency);
                recorderReplayer.menuRecRep.PlayPauseReplay();
            }
            else
            {
                recorderReplayer.audioRecRep.LATENCY = latency;
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveLatency();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SaveLatency()
    {
        PlayerPrefs.SetInt("latency", latency);
        PlayerPrefs.Save();
    }

    public void LoadLatency()
    {
        latency = PlayerPrefs.GetInt("latency", 0);
        recorderReplayer.audioRecRep.LATENCY = latency;
        slider.SetSliderFromNormalizedValue((latency - minLatency)/(float)(maxLatency - minLatency));
        tmpText.text = latency.ToString();
    }
}
