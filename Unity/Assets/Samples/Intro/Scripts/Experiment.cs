using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using Ubiq.Avatars;
using Ubiq.Samples;

/// <summary>
/// Need all the recordings (and postprocessed audio files)
/// load audio separately this time as it has to be processed
/// Shuffle them in random order, save which one is which
/// Save responses of users in an extra file
/// save times when they clicked on a button (decided which avatar to take) in responses file
/// record user while they are doing what they are doing
/// 
/// flip recordings so users don't notice that they have seen things twice
/// do different voice overs for the same recordings?
/// </summary>
///

// overall settings for experiment either single or multi user or actual study (presentation) view
public enum ReplayMode
{
    Presentation = 0, // hide audio indicators and markers
    SingleUser = 1, // single user mode (with audio indicators)
    MultiUser = 2,
}

public class Experiment : MonoBehaviour
{
    public RecorderReplayer recRep;
    public AvatarManager avatarManager;
    public List<Canvas> canvases;
    public ReplayMode mode;

    private string pathToRecordings = "C:/Users/klara/AppData/LocalLow/ucl/interactive_recordreplay/Recordings";
    private string pathToVideos = "C:/Users/klara/PhD/Projects/ubiqFork/Unity/Recordings/Round1";

    private List<Recording> recordings;

    private RecorderControllerSettings controllerSettings;
    private RecorderController recorderController;
    private MovieRecorderSettings videoRecorder;

    [HideInInspector] public bool visible = true;

    ////////////////////////////////////
    /// (1) REMEMBER TO SET PATH TO VIDEOS FOR NEW SET OF REPLAYS
    /// (2) SET TAKE NUMBER BACK TO 1 AFTER EACH SET OF REPLAYS
    ///////////////////////////////////

    public class Recording
    {
        public string fileName; // without extension IDsrec (.txt), rec (.dat), audiorec (.dat)
    }

    public void Start()
    {
        // set path to recordings
        recRep.path = pathToRecordings;

        // hide menu, script, and info canvases in presentation mode
        if (mode == ReplayMode.Presentation)
        {
            foreach (var canvas in canvases)
            {
                canvas.enabled = false;
            }
        }

        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        recorderController = new RecorderController(controllerSettings);
        videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        videoRecorder.name = "RecordReplayVideoRecorder";
        videoRecorder.Enabled = true;

        videoRecorder.FrameRate = 60;
        videoRecorder.FrameRatePlayback = FrameRatePlayback.Constant;
        videoRecorder.CapFrameRate = true; // IMPORTANT or it is too fast
        videoRecorder.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.WebM;
        videoRecorder.VideoBitRateMode = VideoBitrateMode.High; // quality
        videoRecorder.AudioInputSettings.PreserveAudio = true; // includes audio recording

        RecorderOptions.VerboseMode = false; // log additional recording info in console
        videoRecorder.ImageInputSettings = new CameraInputSettings()
        {
            Source = ImageSource.TaggedCamera,
            CameraTag = "VideoCamera",
            FlipFinalOutput = true,
            OutputHeight = 1080,
            OutputWidth = 1920

            
        };
        videoRecorder.RecordMode = RecordMode.Manual;

        controllerSettings.SetRecordModeToManual();
        controllerSettings.AddRecorderSettings(videoRecorder);

    }

    public IEnumerator CreateVideosFromRecordings()
    {
        Debug.Log("Wait a little...");
        yield return new WaitForSeconds(5.0f);
        Debug.Log("Let's go!");
        var allGood = GetRecordingsFromDir(pathToRecordings);

        if (allGood)
        {
            for (var i  = 0; i < recordings.Count; i++)
            {
                // load replay
                var name = recordings[i].fileName;
                recRep.menuRecRep.SelectReplayFile(name);
                recRep.menuRecRep.ToggleReplay();
                // wait for replay to be loaded
                yield return new WaitForSeconds(4.0f); // should be enough to load everything
                var maxFrame = recRep.replayer.recInfo.frames;
                //Debug.Log(maxFrame);

                videoRecorder.OutputFile = pathToVideos + "/" + DefaultWildcard.Take;

                // start the replay
                recRep.menuRecRep.PlayPauseReplay();
                yield return new WaitForSeconds(0.01f); 
                // start the video recording
                Debug.Log("START VIDEO RECORDING OF VIDEO " + i + " (" + name + ", frames: " + maxFrame + ") to file " + videoRecorder.OutputFile);
                recorderController.PrepareRecording(); // has to be called before StartRecording()
                recorderController.StartRecording();

                // wait for the replay to be played almost to the end
                if (maxFrame > 0)
                {
                    yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame-10);
                }
                // stop video recording a few frames before the end
                recorderController.StopRecording();
                Debug.Log("STOP VIDEO RECORDING OF VIDEO " + i + ".");
                // stop the replay
                recRep.menuRecRep.PlayPauseReplay();
                // delete the replay
                recRep.menuRecRep.ToggleReplay();

                Debug.Log("Wait...");
                // just in case 
                yield return new WaitForSeconds(4.0f);

            }
        }
        else
        {
            yield return null;
        }

    }

    // param: number of the recording in the list of recordings
    // sets the replay file name and loads the respective recording
    public void LoadReplay(int replayNr)
    {
        
        recRep.menuRecRep.SelectReplayFile(recordings[replayNr].fileName); 
        recRep.menuRecRep.ToggleReplay();
    }
    // param: path to folder where all the recordings (rec, audiorec, IDsrec) are stored
    public bool GetRecordingsFromDir(string pathToRecordings)
    {
        recordings = new List<Recording>();
        try
        {
            var dir = new DirectoryInfo(recRep.path); // path to recordings
            if (dir.Exists)
            {
                foreach (var file in dir.EnumerateFiles("r*"))
                {
                    
                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                    Debug.Log(file.Name);
                    recordings.Add(new Recording()
                    {
                        fileName = fileName
                    });
                }
                return true;
            }
            else
            {
                Debug.Log("Directory does not exist!");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.ToString());
            return false;
        }
    }

    public void SetMyAvatarVisibility()
    {
        if (visible)
        {
            avatarManager.LocalAvatar.gameObject.GetComponent<ObjectHider>().SetLayer(0);
        }
        else
        {
            avatarManager.LocalAvatar.gameObject.GetComponent<ObjectHider>().SetLayer(8);   
        }
    }

}

# if UNITY_EDITOR
[CustomEditor(typeof(Experiment))]
public class ExperimentEditor : Editor 
{
    Experiment t;
    bool visible = true; // my avatar visibility

    private void OnEnable()
    {
        t = (Experiment)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Label("---------------------------------");

        visible = GUILayout.Toggle(visible, "Show Avatar");
        {
            if (Application.isPlaying && t.visible != visible)
            {
                Debug.Log(visible);
                t.visible = visible;
                t.SetMyAvatarVisibility();
            }
        }

        if (GUILayout.Button("Start Video Recording"))
        {
            t.StartCoroutine(t.CreateVideosFromRecordings());
        }
    }
}
# endif
