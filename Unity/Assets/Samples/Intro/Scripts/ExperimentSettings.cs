using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif
using Ubiq.Avatars;
using Ubiq.Samples;

// overall settings for experiment either single or multi user or actual study (presentation) view
public enum ReplayMode
{
    Presentation = 0, // hide audio indicators and markers
    SingleUser = 1, // single user mode (with audio indicators)
    MultiUser = 2,
}

public class ExperimentSettings : MonoBehaviour
{
    public RecorderReplayer recRep;
    public AvatarManager avatarManager;
    public List<Canvas> canvases;
    public PlayerPosition playerPosition; // create different starting player positions for study
    public Vector3 studyUIPosition; // position of study UI in front of player 
    public ReplayMode mode;

    private string pathToRecordings = "C:/Users/klara/AppData/LocalLow/ucl/interactive_recordreplay/Recordings";
    //private string pathToRecordings = "C:/Users/klara/PhD/Projects/ubiqFork/DialoguesDataset/TRANSFORMED/SELECTED/Round2";
    private string pathToVideos = "C:/Users/klara/PhD/Projects/ubiqFork/Unity/Recordings/SELECTED_videos/Round1";

    private string[] allFiles = new string[]{
        ////// FIRST ROUND //////
        "rec01r1", "rec01n",
        "rec02r2", "rec02b",
        "rec03n", "rec03r2",
        "rec04b", "rec04r1",
        "rec05r2", "rec05n",
        "rec06b", "rec06r2",
        "rec02n", "rec02r1",
        "rec03r1", "rec03b",
        "rec04n", "rec04r2",
        "rec05b", "rec05r1",
        "rec06r1", "rec06n",
        "rec01r2", "rec01b",
        ////// SECOND ROUND //////
        "rec01b","rec02r1",
        "rec03r1","rec04n",
        "rec05r2","rec02b",
        "rec06n","rec03r2",
        "rec05r1","rec04b",
        "rec06r2","rec02n",
        "rec05b","rec01r2",
        "rec03n","rec04r1",
        "rec02r2","rec06b",
        "rec01n","rec04r2",
        "rec03b","rec01r1",
        "rec06r1","rec05n"};

    private List<Recording> recordings;

# if UNITY_EDITOR
    private RecorderControllerSettings controllerSettings;
    private RecorderController recorderController;
    private MovieRecorderSettings videoRecorder;
# endif

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
        //recRep.path = pathToRecordings;

        // hide menu, script, and info canvases in presentation mode
        if (mode == ReplayMode.Presentation)
        {
            foreach (var canvas in canvases)
            {
                canvas.enabled = false;
            }
        }

        //controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        //recorderController = new RecorderController(controllerSettings);
        //videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        //videoRecorder.name = "RecordReplayVideoRecorder";
        //videoRecorder.Enabled = true;

        //videoRecorder.FrameRate = 60;
        //videoRecorder.FrameRatePlayback = FrameRatePlayback.Constant;
        //videoRecorder.CapFrameRate = true; // IMPORTANT or it is too fast
        //videoRecorder.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.WebM;
        //videoRecorder.VideoBitRateMode = VideoBitrateMode.High; // quality
        //videoRecorder.AudioInputSettings.PreserveAudio = true; // includes audio recording

        //RecorderOptions.VerboseMode = false; // log additional recording info in console
        //videoRecorder.ImageInputSettings = new CameraInputSettings()
        //{
        //    Source = ImageSource.TaggedCamera,
        //    CameraTag = "VideoCamera",
        //    FlipFinalOutput = true,
        //    OutputHeight = 1080,
        //    OutputWidth = 1920

            
        //};
        //videoRecorder.RecordMode = RecordMode.Manual;

        //videoRecorder.Take = 6;

        //controllerSettings.SetRecordModeToManual();
        //controllerSettings.AddRecorderSettings(videoRecorder);

    }

# if UNITY_EDITOR
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
# endif

    // param: number of the recording in the list of recordings
    // sets the replay file name and loads the respective recording
    public void LoadReplay(int replayNr)
    {
        
        recRep.menuRecRep.SelectReplayFile(recordings[replayNr].fileName); 
        recRep.menuRecRep.ToggleReplay();
    }
    public void LoadReplay(string fileName) // filename without extension
    {
        recRep.menuRecRep.SelectReplayFile(fileName);
        recRep.menuRecRep.ToggleReplay();
    }

    // param: path to folder where all the recordings (rec, audiorec, IDsrec) are stored
    public bool GetRecordingsFromDir(string pathToRecordings, bool fileOrder=false)
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
[CustomEditor(typeof(ExperimentSettings))]
public class ExperimentSettingsEditor : Editor 
{
    ExperimentSettings t;
    bool visible = true; // my avatar visibility

    private void OnEnable()
    {
        t = (ExperimentSettings)target;
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
