using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// recorder for VR interaction of RecorderReplayer
// functionalities:
// Load Replay by putting a tape into the recorder
// play/pause a replay by pressing a button on the recorder
// when a replay is loaded and record button on controller is pressed start recording with the replay

public class VRRecorder : MonoBehaviour
{
    public VRUtensilRenderer utensilRenderer;
    public RecorderReplayer recorderReplayer;
    public GameObject tapePrefab;
    public Transform tapeAttachmentPoint;
    public GameObject tapeRack;

    private AudioSource attachSound;
    private AudioSource detachSound;
    private int rackSize;
    private int tapeCount = 0;

    private Tape tape;
    private VRButton vrButton;

    private bool replayLoaded = false; // is a replay loaded
    private bool isPlaying = false; // is a replay playing
   
    void Start()
    {
        vrButton = GetComponentInChildren<VRButton>();
        var audioSources = GetComponents<AudioSource>();
        attachSound = audioSources[0];
        detachSound = audioSources[1];
        vrButton.OnPress += VrButtonOnPress;
        recorderReplayer.recorder.OnRecordingStopped += RecorderOnRecordingStopped;
        recorderReplayer.menuRecRep.PlayPauseReplayEvent += MenuRecRepOnPlayPauseReplayEvent;
        recorderReplayer.menuRecRep.OnGetReplaysFromDir += MenuRecRepOnGetReplaysFromDir;
        rackSize = tapeRack.transform.childCount;
    }

    private void MenuRecRepOnGetReplaysFromDir(object sender, List<string> recordings)
    {
        foreach (var name in recordings)
        {
            var newTape = Instantiate(tapePrefab, tapeRack.transform.GetChild(tapeCount).position, tapeRack.transform.GetChild(tapeCount).rotation);
            newTape.gameObject.transform.parent = tapeRack.transform.GetChild(tapeCount);

            utensilRenderer.AddRenderers(newTape); // adds renderers and enables them depending on the visibility
            newTape.GetComponent<Tape>().SetTapeName(name);
            newTape.GetComponent<Tape>().SetVRRecorder(this);

            // newTape.gameObject.transform.position = tapeRack.transform.GetChild(t).position;
            // newTape.gameObject.transform.rotation = tapeRack.transform.GetChild(t).rotation;
            tapeCount++;
            if (tapeCount == rackSize)
            {
                tapeCount = 0;
            }
        }
    }

    private void MenuRecRepOnPlayPauseReplayEvent(object sender, bool isPlaying)
    {
        Debug.Log("VRRecorder: MenuRecRepOnPlayPauseReplayEvent: isPlaying: " + isPlaying);
        this.isPlaying = isPlaying;
    }

    // create new tape when recording is stopped and put it in the recorder already.
    // if the recorder is already full from a previous replay put the replay on the rack.
    private void RecorderOnRecordingStopped(object sender, EventArgs e)
    {
        var t = tapeCount % rackSize;
        // if replay was playing
        if (tape != null)
        {
            Debug.Log("Move tape to rack");
            OnPause(); // replay is already paused as it is stopped automatically when recording is stopped (but here we just reset the class-internal variable)

            // put current tape on rack and detach it from recorder
            tape.attached = false;
            tape.gameObject.transform.position = tapeRack.transform.GetChild(t).position;
            tape.gameObject.transform.rotation = tapeRack.transform.GetChild(t).rotation;
            tape.gameObject.transform.parent = tapeRack.transform.GetChild(t);
            tapeCount++;
            if (tapeCount == rackSize)
            {
                tapeCount = 0;
            }
        }
        replayLoaded = true; // because we automatically load a new replay when recording is stopped
        // put new tape in recorder (which is actually a replayer...whatever >.<)
        string newTapeName = Path.GetFileNameWithoutExtension(recorderReplayer.recordFile);
        var newTape = Instantiate(tapePrefab, tapeAttachmentPoint.position, this.transform.rotation);
        utensilRenderer.AddRenderers(newTape); // adds renderers and enables them depending on the visibility
        newTape.transform.parent = this.transform;
        newTape.GetComponent<Tape>().SetTapeName(newTapeName);
        this.tape = newTape.GetComponent<Tape>(); // set new tape as current tape
        this.tape.attached = true;
        this.tape.SetVRRecorder(this);
        
    }

    // play/pause the current replay that is loaded
    private void VrButtonOnPress(object sender, EventArgs e)
    {
        // RecorderOnRecordingStopped(this, EventArgs.Empty); \\ just for testing
        // if (isPlaying) 
        //     {
        //         OnPause();
        //     }
        //     else 
        //     {
        //         OnPlay();
        //     }
        if (recorderReplayer.replaying)
        {
            if (isPlaying) // pause replay
            {
                recorderReplayer.menuRecRep.PlayPauseReplay();
                OnPause();
            }
            else // play replay
            {
                recorderReplayer.menuRecRep.PlayPauseReplay();
                OnPlay();
            }
        }
    }

    public bool DetachTape(GameObject tape)
    {
        if (!recorderReplayer.recording && !isPlaying)
        {   
            Debug.Log("Detach tape");
            this.tape = null;
            detachSound.Play();
            DeleteReplay();
            return true;
        }
        else
        {
            return false;
        }
    }

    public void AttachTape(GameObject tape)
    {
        if (this.tape == null)
        {
            attachSound.Play();
            this.tape = tape.GetComponent<Tape>();
            tape.transform.position = tapeAttachmentPoint.position;
            tape.transform.rotation = this.transform.rotation;
            tape.transform.parent = this.transform;

            // load replay
            LoadReplay();
        }
    }

    public void OnPlay()
    {
        // rotate tape when being played
        isPlaying = true;
    }
    public void OnPause()
    {
        isPlaying = false;
    }

    public void LoadReplay()
    {
        // get text from tape
        recorderReplayer.menuRecRep.SelectReplayFile(tape.GetTapeName());
        // load replay
        recorderReplayer.menuRecRep.ToggleReplay();
        replayLoaded = true;
    }

    public void DeleteReplay()
    {
        if (replayLoaded)
        {
            Debug.Log("VRRecorder: DeleteReplay");
            recorderReplayer.menuRecRep.ToggleReplay();
            replayLoaded = false;
        }
    }

    void Update()
    {
        // rotate tape when being played
        if (tape != null && isPlaying)
        {
            tape.gameObject.transform.Rotate(-1.5f, 0, 0);
        }
    }
}
