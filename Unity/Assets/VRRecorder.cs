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

    private bool isReplaying = false; // is a replay loaded
    private bool isPlaying = false; // is a replay playing
   
    void Start()
    {
        vrButton = GetComponentInChildren<VRButton>();
        var audioSources = GetComponents<AudioSource>();
        attachSound = audioSources[0];
        detachSound = audioSources[1];
        vrButton.OnPress += VrButtonOnPress;
        recorderReplayer.recorder.OnRecordingStopped += RecorderOnRecordingStopped;
        rackSize = tapeRack.transform.childCount;
    }

    // create new tape when recording is stopped
    private void RecorderOnRecordingStopped(object sender, EventArgs e)
    {
        string newTapeName = Path.GetFileNameWithoutExtension(recorderReplayer.recordFile);
        var t = tapeCount % rackSize;
        var newTape = Instantiate(tapePrefab, tapeRack.transform.GetChild(t).position, tapeRack.transform.GetChild(t).rotation);
        newTape.transform.parent = tapeRack.transform.GetChild(t);
        tapeCount++;
        newTape.GetComponent<Tape>().SetTapeName(newTapeName);
        if (tapeCount == rackSize)
        {
            tapeCount = 0;
        }
        
    }

    // play/pause the current replay that is loaded
    private void VrButtonOnPress(object sender, EventArgs e)
    {
        // RecorderOnRecordingStopped(this, EventArgs.Empty); \\ just for testing
        if (isPlaying) 
            {
                OnPause();
            }
            else 
            {
                OnPlay();
            }


        if (isReplaying)
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
            this.tape = null;
            detachSound.Play();
            // DeleteReplay();
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
            tape.transform.rotation = Quaternion.Euler(0, 0, 0);

            // load replay
            // LoadReplay();
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
        isReplaying = true;
    }

    public void DeleteReplay()
    {
        if (isReplaying)
        {
            recorderReplayer.menuRecRep.ToggleReplay();
            isReplaying = false;
        }
    }

    void Update()
    {
        // rotate tape when being played
        if (isPlaying)
        {
            tape.gameObject.transform.Rotate(-1.5f, 0, 0);
        }
    }
}
