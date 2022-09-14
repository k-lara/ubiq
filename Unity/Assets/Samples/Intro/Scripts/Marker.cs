using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Spawning;
using Ubiq.XR;

public class Marker : MonoBehaviour
{
    // during a recording the user can press a button which makes the recording save the current time as a "marker" 
    // for later replays.
    // the marker times are saved in the recording ID file separately as the markers need to be drawn
    // onto the audioIndicator canvas at the beginning of the replay
    // this enables users to mark down incidents during the recording, such as a non-verbal gesture
    // or even errors in replays (wrong behaviour of characters or objects) for later evaluation

    public RecorderReplayer recRep;
    public AvatarManager manager;
    public HandController leftController;
    public HandController rightController;
    public Text info;


    private Dictionary<NetworkId, Texture2D> markerTextures;
    private int width = 500;
    private int height = 100;
    private Color col = new Color(1, 0, 0, 0.4f); // translucent red for button pressed markers
    private Color col2 = new Color(1, 0.92f, 0.016f, 0.4f); // translucent yellow for controllers
    private int thickness = 2; // of marker
    private Color[] markerLine;
    private Color[] markerLine2;

    private NetworkId localAvatarId;

    private List<AvatarMarkers> replayedMarkers;
    private float replayLength;
    private NetworkSpawner spawner;

    // markers indicate the begin and end of a marked region
    // two consecutive float time stamps always belong together, the first giving the start and the second giving the end of the marked region
    private AvatarMarkers currentAvatarMarkers;
    private AvatarMarkers handGripMarkers;


    // Start is called before the first frame update
    void Start()
    {
        //spawner = NetworkSpawner.FindNetworkSpawner(NetworkScene.FindNetworkScene(this));
        currentAvatarMarkers = new AvatarMarkers();
        handGripMarkers = new AvatarMarkers();
        markerLine = new Color[height * thickness];
        markerLine2 = new Color[height * thickness];

        for (int i = 0; i < markerLine.Length; i++)
        {
            markerLine[i] = col;
            markerLine2[i] = col2;
        }
    }

    public void SetReplayedMarkers(RecorderReplayerTypes.RecordingInfo recInfo)
    {
        //Debug.Log("recinfo markerlist " + recInfo.markerLists.Count);
        replayedMarkers = recInfo.markerLists;
        replayLength = recInfo.frameTimes[recInfo.frameTimes.Count - 1];
        markerTextures = new Dictionary<NetworkId, Texture2D>();
        foreach (var markerList in replayedMarkers)
        {
            if (!markerTextures.ContainsKey(markerList.id))
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                // set transparent background of texture
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
                tex.Apply();
                markerTextures.Add(markerList.id, tex);
            }
        }
        Debug.Log("# replayed markers: " + replayedMarkers.Count);
    }

    public void CreateMarkerCanvas(NetworkId id, AudioIndicator ai)
    {
        var i = 0;
        if (replayedMarkers == null)
        {
            Debug.Log("No marker list");
            ai.markerTex.color = Color.clear;
            return;
        }
        foreach (var markerList in replayedMarkers)
        {
            try
            {
                var newId = recRep.replayer.oldNewIds[markerList.id];
                
                if (newId == id) 
                {
                    Debug.Log("Add a marker");
                    // get texture from dict
                    var tex = markerTextures[markerList.id];
                    float size = replayLength / (float)width; // length the replay has on the texture
                    Debug.Log("size : " + size);
                    // draw markers
                    for (int m = 0; m < markerList.markers.Count; m+=2)
                    {
                        var start = Mathf.RoundToInt(markerList.markers[m] / size);
                        var end = Mathf.RoundToInt(markerList.markers[m + 1] / size);
                    
                        for (int s = start; s < end; s+=2) // thickness 
                        {
                            if (markerList.source == 0)
                            {
                                tex.SetPixels(s, 0, thickness, height, markerLine);
                            }
                            else
                            {
                                tex.SetPixels(s, 0, thickness, height, markerLine2);
                            }
                        }
                    }
                    tex.Apply();
                    // add it to audio indicator
                    ai.markerTex.texture = tex;

                    // swap old id in marker list with new id so we can keep recording
                    // because the main user will have the same id throughout the recording
                    markerList.id = newId;
                }
            }
            catch
            {
                Debug.Log("Key Not Found: Id might have been replaced already");
            }
            
            i++;
        }
    }

    [System.Serializable]
    public class AvatarMarkers
    {
        [SerializeField]
        public NetworkId id;
        public int source = 0; // 0 = button press, 1 = from hand grasps
        public List<float> markers = new List<float>();
    }

    public AvatarMarkers GetControllerMarkers()
    {
        handGripMarkers.id = manager.LocalAvatar.Id;
        handGripMarkers.source = 1;
        return handGripMarkers;
    }

    public AvatarMarkers GetAvatarMarkers()
    {
        // by then the id of the user should be available as we only call it at the end of the recording
        currentAvatarMarkers.id = manager.LocalAvatar.Id;
        currentAvatarMarkers.source = 0;
        return currentAvatarMarkers;
    }

    public void MarkControllerGrip(bool isGripped)
    {
        if (recRep.recording)
        {
            var t = Time.unscaledTime - recRep.recorder.GetRecordingStartTime();
            if (isGripped)
            {
                Debug.Log("GRIP START: " + t);
                info.text = "Marking Grip...";
            }
            else
            {
                Debug.Log("GRIP END: " + t);
                info.text = "";
            }
            // only save markers when we are actually recording something
            handGripMarkers.markers.Add(t);
        }
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
            currentAvatarMarkers.markers.Add(t);
        }
    }

    // when recording finished and markers are saved clear the list
    public void ClearCurrentAvatarMarkers()
    {
        currentAvatarMarkers.markers.Clear();
        handGripMarkers.markers.Clear();
    }
    public IEnumerator FadeTextToZeroAlpha(float t, Text i)
    {
        i.color = new Color(i.color.r, i.color.g, i.color.b, 1);
        while (i.color.a > 0.0f)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a - (Time.deltaTime / t));
            yield return null;
        }
    }

}

