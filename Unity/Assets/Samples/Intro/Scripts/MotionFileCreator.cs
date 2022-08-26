using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;
using System.Text;
using Ubiq.Messaging;
using Ubiq.Avatars;
using Ubiq.Spawning;

public class MotionFileCreator : MonoBehaviour
{
    public RecorderReplayerMenu menu;
    public string pathToDir = "C:/Users/klara/PhD/Projects/MLVR/Datasets/PuzzleTask/";
    //public Toggle replayOnlyToggle;
    //public Toggle writeToFileToggle;
    private Toggle autoReplayToggle;
    public bool fromReplayOnly = true; // if true only consider replayed avatars for motion file, otherwise consider all avatars
    public bool writeToFile = false; // use this when writing mixed data (recorded and current)
    [HideInInspector] public bool automatedReplay;
    private bool init = false;
    private NetworkScene scene;
    private AvatarManager avatarManager;
    private NetworkSpawner spawner;
    private RecorderReplayer recRep;
    private List<ThreePointTrackedAvatar> trackedAvatars;
    private List<HandAnimation> handAnims;

    private StreamWriter writer;
    private StringBuilder builder;
    private int frame;
    private float replayLength; // in seconds 
    private string header;
    private string header2 = "ID,time(s),frame,HposX,HposY,HposZ,HrotX,HrotY,HrotZ,HrotW,LposX,LposY,LposZ,LrotX,LrotY,LrotZ,LrotW,RposX,RposY,RposZ,RrotX,RrotY,RrotZ,RrotW,Lgrip,Rgrip,";


    // Start is called before the first frame update
    void Start()
    {
        scene = NetworkScene.FindNetworkScene(this);
        avatarManager = AvatarManager.Find(this);
        spawner = NetworkSpawner.FindNetworkSpawner(scene);
        recRep = scene.GetComponentInChildren<RecorderReplayer>();
        builder = new StringBuilder();
        recRep.replayer.OnLoadingReplay += Replayer_OnLoadingReplay;
        recRep.replayer.OnReplayLoaded += WriteToFile;
        recRep.replayer.OnReplayStopped += Replayer_OnReplayStopped; // set writeToFile false 
        menu.PlayPauseReplayEvent += Menu_PlayPauseReplayEvent; // toggle writeToFile
        recRep.EndAutomatedReplayEvent += RecRep_EndAutomatedReplayEvent; // set automatedReplay false
    }

    private void WriteToFile(object sender, System.EventArgs e)
    {
        writeToFile = true;
    }

    public void SetToggle(Toggle toggle)
    {
        Debug.Log(toggle.name);
        switch (toggle.name)
        {
            case "ReplayOnlyToggle":
                fromReplayOnly = toggle.isOn;
                break;
            case "WriteToFileToggle":
                writeToFile = toggle.isOn;
                if (!writeToFile) Cleanup();
                break;
            case "AutoReplayToggle":
                Debug.Log(toggle.isOn ? " AutoReplay on" : " AutoReplay off");
                autoReplayToggle = toggle;
                automatedReplay = toggle.isOn;
                menu.gameObject.SetActive(automatedReplay);
                SetAutomatedReplayInRecRep(automatedReplay);
                recRep.replaying = automatedReplay;
                // only set writeToFile true once the replay is loaded
                //writeToFile = automatedReplay;
                //if (!writeToFile) Cleanup();
                break;
            default:
                break;              
        }
            
    }

    private void Replayer_OnReplayStopped(object sender, System.EventArgs e)
    {
        Debug.Log("Write to file: false");
        writeToFile = false;
        Cleanup();
    }

    private void RecRep_EndAutomatedReplayEvent(object sender, System.EventArgs e)
    {
        automatedReplay = autoReplayToggle.isOn = false;
    }

    private void Replayer_OnLoadingReplay(object sender, RecorderReplayerTypes.RecordingInfo recInfo)
    {
        replayLength = recInfo.frameTimes[recInfo.frameTimes.Count - 1];
    }

    // is called when the play/pause button in the menu is pressed
    private void Menu_PlayPauseReplayEvent(object sender, bool play)
    {
        if (fromReplayOnly)
        {
            Debug.Log("Motion File Creator: writeToFile = " + play);
            writeToFile = play;

            if (!play) Cleanup();

        }
    }

    // always call Cleanup() when writeToFile is set to false to make sure remaining data is written to file and StreamWriter is closed properly
    private void Cleanup()
    {
        if (builder.Length > 0)
        {
            builder.Remove(builder.Length - 1, 1); // remove last comma
            writer.WriteLine(builder.ToString());
            builder.Clear();
        }
        if (writer != null)
            writer.Dispose();
        init = false; // important for next file to be created!
        wait = 0;
    }

    private void GetThreePointTrackedAvatarsAndHandAnimation()
    {
        trackedAvatars = new List<ThreePointTrackedAvatar>();
        handAnims = new List<HandAnimation>();
        
        if (!fromReplayOnly)
        {
            foreach (var avatar in avatarManager.Avatars)
            {
                trackedAvatars.Add(avatar.gameObject.GetComponent<ThreePointTrackedAvatar>());
                handAnims.Add(avatar.gameObject.GetComponent<HandAnimation>());
            }
        }

        foreach (var spawned in spawner.spawned.Values)
        {
            if (spawned.TryGetComponent(out ThreePointTrackedAvatar trackedAvatar)) // if it has this it should also have HandAnimation
            {
                trackedAvatars.Add(trackedAvatar);
                handAnims.Add(spawned.GetComponent<HandAnimation>());
            }
        }
        header = "#avatars," + trackedAvatars.Count + ",length(s)," + replayLength + ",";
    }

    // Update is called once per frame
    // wait x frames until writing starts to give replayed avatars a chance to set their position
    int wait = 0;
    float startTime;
    void LateUpdate()
    {
        if (writeToFile)
        {
            //if (wait <= 1)
            //{
            //    wait++;
            //    return;
            //}
            
            if (!init)
            {
                init = true;
                writer = new StreamWriter(pathToDir + "/ubiqtracking_" + Mathf.RoundToInt(replayLength) + "_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv");
                GetThreePointTrackedAvatarsAndHandAnimation();
                Debug.Log("Number of avatars: " + trackedAvatars.Count);
                writer.WriteLine(header);
                writer.WriteLine(header2);
                startTime = Time.unscaledTime;
                frame = 0;
            }

            if (builder.Length > 0)
            {
                writer.WriteLine(builder.ToString());
                builder.Clear();
            }
            //var avatarID = 0;
            var time = Time.unscaledTime - startTime;
            for (int i = 0; i < trackedAvatars.Count; i++)
            {
                ThreePointTrackedAvatar.State state = trackedAvatars[i].GetState();
                (float lg, float rg) = handAnims[i].GetGripTargets();
                var hp = state.head.position;
                var hr = state.head.rotation;
                var lp = state.leftHand.position;
                var lr = state.leftHand.rotation;
                var rp = state.rightHand.position;
                var rr = state.rightHand.rotation;
                
                builder.Append(i + ","); // avatar id
                builder.Append(time + ",");
                builder.Append(frame + ",");
                builder.Append(hp.x + "," + hp.y + "," + hp.z + "," + hr.x + "," + hr.y + "," + hr.z + ", " + hr.w + ","); // head
                builder.Append(lp.x + "," + lp.y + "," + lp.z + "," + lr.x + "," + lr.y + "," + lr.z + ", " + lr.w + ","); // left hand
                builder.Append(rp.x + "," + rp.y + "," + rp.z + "," + rr.x + "," + rr.y + "," + rr.z + ", " + rr.w + ","); // right hand
                builder.Append(lg + "," + rg + ",");
                
                if (i < trackedAvatars.Count - 1)
                {
                    writer.WriteLine(builder.ToString());
                    builder.Clear();
                }
            }
            frame++;
        }
        //else
        //{
        //    //if (builder.Length > 0)
        //    //{
        //    //    builder.Remove(builder.Length - 1, 1); // remove last comma
        //    //    writer.WriteLine(builder.ToString());
        //    //    builder.Clear();
        //    //}
        //    //if (writer != null)
        //    //    writer.Dispose();
        //    //init = false;
        //    //wait = 0;
        //}

    }

    public void SetAutomatedReplayInRecRep(bool auto)
    {
        recRep.automatedReplay = auto;
    }

    private void OnDestroy()
    {
        if (writer != null) writer.Dispose();
    }
}

//# if UNITY_EDITOR
//[CustomEditor(typeof(MotionFileCreator))]
//public class MotionFileCreatorEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        DrawDefaultInspector();
//        if (Application.isPlaying)
//        {
//            var t = (MotionFileCreator)target;
//            t.automatedReplay = EditorGUILayout.Toggle("Auto Replay", t.automatedReplay);
//            //t.menu.SetAutomatedReplay(t.automatedReplay);
//            if (t.automatedReplay)
//            {
//                t.SetAutomatedReplayInRecRep(t.automatedReplay);
//                t.menu.gameObject.SetActive(true);
//            }
//            else
//            {
//                t.SetAutomatedReplayInRecRep(t.automatedReplay);
//                t.menu.gameObject.SetActive(false);
//            }
//        }
//    }
//}
//# endif
