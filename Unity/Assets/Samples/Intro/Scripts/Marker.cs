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
    private List<float> frameTimes;
    private NetworkSpawner spawner;
    private Dictionary<short, NetworkId> clipNrToOldIds;

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
        clipNrToOldIds = new Dictionary<short, NetworkId>();

        markerLine = new Color[height * thickness];
        markerLine2 = new Color[height * thickness];

        for (int i = 0; i < markerLine.Length; i++)
        {
            markerLine[i] = col;
            markerLine2[i] = col2;
        }
    }

    public List<float> GetFrameTimes()
    {
        return frameTimes;
    }
    public float GetReplayLength()
    {
        return replayLength;
    }

    public void SetReplayedMarkers(RecorderReplayerTypes.RecordingInfo recInfo)
    {
        //Debug.Log("recinfo markerlist " + recInfo.markerLists.Count);
        replayedMarkers = recInfo.markerLists;
        frameTimes = recInfo.frameTimes;
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

    public void AddClipNumber(NetworkId id, short clipNr)
    {
        // we get a new id and a clipnumber, we compare in the oldNewIds dict which of the old
        // ids corresponds to the new one, and then save the clipnumber together with the old one
        // so we know which markers to take from the marker list
        foreach (var markerList in replayedMarkers)
        {
            try
            {
                // it can be that there are no markers then we should ignore it
                var newId = recRep.replayer.oldNewIds[markerList.id];
                //Debug.Log(newId);
                if (newId.Equals(id))
                {
                    clipNrToOldIds.Add(clipNr, markerList.id);
                    Debug.Log("Add: " + clipNr + " " + markerList.id);
                }
            }
            catch
            {
                Debug.Log("Marker Class: id not found in oldNewIds dict");
            }
        }
    }
    // latency in ms (around 210ms)
    public void CreateMarkerCanvas(short clipNr, AudioIndicator ai, AudioSource source, int latency)
    {        
        if (replayedMarkers == null)
        {
            Debug.Log("No marker list");
            ai.markerTex.color = Color.clear;
            return;
        }
       
        if (clipNrToOldIds.ContainsKey(clipNr))
        {
            var oldId = clipNrToOldIds[clipNr];
            
            foreach (var markerList in replayedMarkers)
            {
                
                if (recRep.replayer.oldNewIds.ContainsKey(markerList.id))
                {
                    var newId = recRep.replayer.oldNewIds[markerList.id];
                    if (oldId.Equals(markerList.id)) 
                    {
                    
                        Debug.Log("Add a marker");
                        // get texture from dict
                        var tex = markerTextures[markerList.id];


                        // need to draw the markers not in relation to replayLength
                        // but to the length of the audio. to be able to align the pointer
                        // with audio and markers!
                        float size = replayLength / width; // length the replay has on the texture
                        
                        // get length of audio - latency in seconds (float)
                        //float audioLength = (source.clip.samples - latency) / (float)source.clip.frequency;
                        //float size = audioLength / width;
                        // shift markers according to new time
                        //float scaleFactor = audioLength / replayLength;

                        //Debug.Log("replay, audio, scale: " + replayLength + " " + audioLength + " " + scaleFactor);


                        // draw markers
                        for (int m = 0; m < markerList.markers.Count; m+=2)
                        {
                            //Debug.Log("normal vs scaled: " + markerList.markers[m] + " " + markerList.markers[m] * scaleFactor);
                            var start = Mathf.RoundToInt((markerList.markers[m]) / size);
                            var end = Mathf.RoundToInt((markerList.markers[m + 1]) / size);
                            Debug.Log(start + " " + end);
                    
                            for (int s = start; s < end && s < width; s+=2) // thickness 
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
                        break; // found it
                    }
                }
                else
                {
                    Debug.Log("Key Not Found: Id might have been replaced already");
                }
            }
        }
        else
        {
            // make canvas of markers invisible for this avatar
            ai.markerTex.color = Color.clear;
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
        clipNrToOldIds.Clear();
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

