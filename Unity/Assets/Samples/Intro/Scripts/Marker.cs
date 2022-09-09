using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Marker : MonoBehaviour
{
    // during a recording the user can press a button which makes the recording save the current time as a "marker" 
    // for later replays.
    // the marker times are saved in the recording ID file separately as the markers need to be drawn
    // onto the audioIndicator canvas at the beginning of the replay
    // this enables users to mark down incidents during the recording, such as a non-verbal gesture
    // or even errors in replays (wrong behaviour of characters or objects) for later evaluation

    public RecorderReplayer recRep;
    public Text info;

    // markers indicate the begin and end of a marked region
    // two consecutive float time stams always belong together, the first giving the start and the second giving the end of the marked region
    private List<float> markers;
    
    public List<float> GetMarkers()
    {
        return markers;
    }

    public void MarkData(bool buttonPress)
    {
        if (recRep.recording)
        {
            var t = Time.unscaledTime - recRep.recorder.GetRecordingStartTime();
            if (buttonPress)
            {
                Debug.Log("MARK START: " + t);
                info.text = "Marking...";
            }
            else
            {
                Debug.Log("MARK END: " + t);
                info.text = "";
            }
            // only save markers when we are actually recording something
            markers.Add(t);
        }
    }

    // when recording finished and markers are saved clear the list
    public void ClearMarkerList()
    {
        markers.Clear();
    }
    
    // Start is called before the first frame update
    void Start()
    {
        markers = new List<float>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
