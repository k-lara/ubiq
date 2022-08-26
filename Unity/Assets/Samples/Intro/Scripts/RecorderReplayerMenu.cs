using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using Ubiq.Messaging;
using UnityEngine.UI;
using Ubiq.Rooms;
using Ubiq.Samples;

public class RecorderReplayerMenu : MonoBehaviour
{
    public event EventHandler<bool> PlayPauseReplayEvent = delegate { };
    //private bool automatedReplay = false; // replays all the files in the directory one after the other (useful for gathering motion data post-hoc)

    public NetworkScene scene;
    public Sprite playSprite;
    public Sprite pauseSprite;
    
    public GameObject buttonPrefab; // for scroll view content

    public Button recordReplayButtonMain;
    public GameObject recordBtn; // Buttons Panel (top left)
    public Image recordImage;
    public Text recordText;
    public GameObject replayBtn;
    private Button replayButton;
    public Image replayImage;
    public Text replayText;
    public GameObject playPauseBtn;
    private Button playPauseButton;
    public Image playPauseImage;
    public Text playPauseText;
    public Image playPauseTextImage;
    public Text currentReplayFileName; // Current Replay File (top right)
    public Slider slider; // Slider Panel (middle)
    public Text sliderText;
    public GameObject content; // Scroll View (bottom)

    private Color white = new Color(0.937f, 0.937f, 0.937f, 1.0f);

    private PanelSwitcher panelSwitcher;
    private EventTrigger trigger; // for changing the slider value

    private RecorderReplayer recRep;
    private AudioRecorderReplayer audioRecRep;
    private RoomClient roomClient;
    private DirectoryInfo dir;

    private List<string> recordings;
    private List<string> newRecordings;
    private bool infoSet = false;
    private bool loaded = false;
    private bool needsUpdate = true;
    private bool resetReplayImage = false; // in case we loaded an invalid file path

    private List<Button> replayFileButtons;

    public event EventHandler OnPointerUp;

    //private event EventHandler<bool> OnPlayPauseReplay;

    public string GetRecording(int index) 
    { 
        return recordings != null ? recordings[index] : string.Empty; 
    }
    public int NumberOfRecordings() { return recordings.Count; }


    //public void SetAutomatedReplay(bool b)
    //{
    //    automatedReplay = b;
    //}

    void Start()
    {
        panelSwitcher = GetComponentInParent<PanelSwitcher>();
        panelSwitcher.OnPanelSwitch.AddListener(OnPanelSwitch);
        playPauseButton = playPauseBtn.GetComponent<Button>();
        playPauseButton.interactable = false;
        replayButton = replayBtn.GetComponent<Button>();
        replayButton.interactable = false; // when no replay file is selected replay is not possible
        slider.interactable = false;

        playPauseButton.interactable = false;
        playPauseImage.color = Color.clear;
        playPauseText.color = Color.clear;
        playPauseTextImage.color = Color.clear;

        recordings = new List<string>(); // recordings that are shown in scroll view
        newRecordings = new List<string>(); // new recordings that have been added since app start
        replayFileButtons = new List<Button>(); // buttons showing the recordings in the scroll view

        Debug.Log("Set RecorderReplayer in Menu");
        recRep = scene.GetComponentInChildren<RecorderReplayer>();
        audioRecRep = recRep.audioRecRep;

        // Changing slider value (adds trigger behaviour)
        trigger = slider.gameObject.GetComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerUp;
        entry.callback.AddListener((data) => { OnPointerUpDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
        //EventTrigger.Entry entry = new EventTrigger.Entry();
        //entry.eventID = EventTriggerType.EndDrag;
        //entry.callback.AddListener((data) => { OnEndDrag((PointerEventData)data); });
        //trigger.triggers.Add(entry);

        roomClient = scene.GetComponent<RoomClient>();
        roomClient.OnPeerUpdated.AddListener(OnPeerUpdated);
        roomClient.OnPeerAdded.AddListener(OnPeerAdded);
        roomClient.OnJoinedRoom.AddListener(OnJoinedRoom);

        recRep.replayer.OnLoadingReplay += Replayer_OnLoadingReplay;

        GetReplayFilesFromDir();
        AddReplayFiles();
    }
    private void Replayer_OnLoadingReplay(object sender, RecorderReplayerTypes.RecordingInfo e)
    {
        slider.maxValue = recRep.replayer.recInfo.frames - 1;
        //Debug.Log("Set slider max value: " + slider.maxValue);
    }

    public void OnPeerAdded(IPeer peer)
    {
        Debug.Log("Menu: OnPeerAdded");
        OnPeerUpdated(peer);

    }

    public void OnJoinedRoom(IRoom room)
    {
        if (roomClient.Me["creator"] == "1")
        {
            GetReplayFilesFromDir();
        }
        //UpdateMenu(roomClient.Me);
    }

    public void OnPeerUpdated(IPeer peer)
    {
        if (peer.UUID == roomClient.Me.UUID) // check this otherwise we also update wrong peer and hide menu accidentally
        {
            UpdateMenu(peer);
        }
    }

    private void UpdateMenu(IPeer peer) // this method does not make a lot of sense here, as the RecorderReplayer Panel is inactive in the beginning
    {
        if (peer["creator"] == "1")
        {
            Debug.Log("Menu: creator");
            recordReplayButtonMain.interactable = true;
            //recordBtn.SetActive(true);
            //replayBtn.SetActive(true);
            // set color of record/replay button back to gray in case of ongoing recording/replaying
            if (!recRep.recording)
            {
                recordImage.color = white;
                recordText.color = white;

            }
            if (!recRep.replaying)
            {
                replayImage.color = white;
                replayText.color = white;
            }
        }
        else
        {
            Debug.Log("Menu: NOT creator");
            recordReplayButtonMain.interactable = false;
            //recordBtn.SetActive(false);
            //replayBtn.SetActive(false);
        }
    }

    public void OnPanelSwitch()
    {
        if (recRep.recording)
        {
            Debug.Log("OnPanelSwitch: End ongoing recording (replay)");
            EndRecordingAndCleanup();
        }
        if (recRep.replaying)
        {
            Debug.Log("OnPanelSwitch: End ongoing replay");
            EndReplayAndCleanup();
        }
    }

    public void OnPointerUpDelegate(PointerEventData data)
    {
        SetReplayFrame();
        OnPointerUp.Invoke(this, EventArgs.Empty);
    }

    private void GetReplayFilesFromDir()
    {
        recordings.Clear();
        try
        {
            dir = new DirectoryInfo(recRep.path); // path to recordings
            if (dir.Exists)
            {
                foreach (var file in dir.EnumerateFiles("r*"))
                {
                    recordings.Add(Path.GetFileNameWithoutExtension(file.Name));
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }
    public void AddReplayFiles()
    {
        if (!loaded)
        {
            Debug.Log("Add all files");
            AddFiles(recordings, content);
            loaded = true;
            needsUpdate = false;
            newRecordings.Clear();
        }
        else if (needsUpdate)
        {
            Debug.Log("Add new!");
            AddFiles(newRecordings, content);
            needsUpdate = false;
            newRecordings.Clear();
        }
    }
    private void AddFiles(List<string> recordings, GameObject content)
    {
        //for (int i = recordings.Count - 1; i >= 0; i--) // show newest recording first 
        foreach (var file in recordings)
        {
            //var file = recordings[i];
            GameObject go = Instantiate(buttonPrefab, content.transform);
            var button = go.GetComponent<Button>();
            button.image.color = Color.clear;
            button.onClick.AddListener(delegate { SelectReplayFile(file); } );
            //go.GetComponent<Button>().onClick.AddListener(delegate { CloseFileWindow(content); });
            button.transform.SetAsFirstSibling();
            replayFileButtons.Add(button);

            Text t = go.GetComponentInChildren<Text>();
            t.text = file;
        }
    }

    public void SelectReplayFile(string file)
    {
        recRep.replayFile = file;
        currentReplayFileName.text = file;
        currentReplayFileName.color = Color.green;
        replayButton.interactable = true;
    }

    private void EnableReplayFileSelection(bool isEnabled)
    {
        foreach (var button in replayFileButtons)
        {
            button.interactable = isEnabled;
        }
    }

    public void ToggleRecord()
    {        
        if (recRep.recording) // if recording stop it
        {
            Debug.Log("Toggle Record (STOP)");

            EndRecordingAndCleanup();
            if (recRep.replaying)
            {
                EndReplayAndCleanup(); // just in case there could have been a replay going on too
                EnableReplayFileSelection(true);
            }
            AddReplayFiles();
        }
        else // start recording
        {
            Debug.Log("Toggle Record (START)");
            recordImage.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
            recordText.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
            recRep.recording = true;
            recRep.SetRecordingStartTime(System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        }
        
    }

    private void EndRecordingAndCleanup()
    {
        // add new recording to dropdown and set it as current replay file
        string rec = Path.GetFileNameWithoutExtension(recRep.recordFile);
        Debug.Log(rec);
        recordings.Add(rec);
        newRecordings.Add(rec);
        SelectReplayFile(rec);
        needsUpdate = true;

        recordImage.color = white;
        recordText.color = white;
        if (recRep.replaying)
        {
            slider.interactable = false;
        }
        recRep.recording = false;
        recRep.replayFile = rec; // is probably set twice (in RecorderReplayer SetReplayFile() too)
        //currentReplayFileName.text = rec;
        infoSet = false;
    }

    public void ToggleReplay()
    {
        //Image img = icon.GetComponent<Image>();
        //replayImage = img;

        if (recRep.replaying) // if replaying stop it
        {
            EndReplayAndCleanup();
            EnableReplayFileSelection(true);
        }
        else // start replaying
        {
            EnableReplayFileSelection(false);
            slider.interactable = false;
            sliderText.text = "";
            playPauseButton.interactable = true; // only clickable during replay
            playPauseImage.color = new Color(0.0f, 0.8f, 0.2f, 1.0f);
            playPauseText.color = Color.green;
            playPauseTextImage.color = new Color(0.65f, 0.65f, 0.65f, 1.0f);
            
            replayImage.color = new Color(0.0f, 0.8f, 0.2f, 1.0f);
            replayText.text = "Delete Replay";
            replayText.color = new Color(0.0f, 0.8f, 0.2f, 1.0f);
            
            recRep.replaying = true;
            resetReplayImage = true;
            slider.minValue = 0;
        }
    }

    private void EndReplayAndCleanup()
    {
        slider.interactable = false;
        slider.value = 0;
        sliderText.text = "";
        replayImage.color = white;
        replayText.text = "Load Replay";
        replayText.color = white;
        recRep.replaying = false;
        resetReplayImage = false;
        playPauseButton.interactable = false;
        playPauseImage.color = new Color(0.0f, 0.8f, 0.2f, 0.0f);
        playPauseText.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        playPauseTextImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        // when replay is initialised again the correct sprite is visible in the slider panel
        playPauseImage.sprite = playSprite;
        playPauseText.text = "Play";
    }

    public void PlayPauseReplay()
    {
        PlayPauseReplayEvent.Invoke(this, !recRep.play);
        if (recRep.play) // if playing pause it
        {
            Debug.Log("Pause");
            //OnPlayPauseReplay.Invoke(this, false);
            audioRecRep.OnPlayPauseReplay(false);
            playPauseImage.sprite = playSprite;
            playPauseText.text = "Play";
            recRep.play = false;
            slider.interactable = true;
        }
        else // resume
        {
            Debug.Log("Play");
            //OnPlayPauseReplay.Invoke(this, true);
            audioRecRep.OnPlayPauseReplay(true);
            playPauseImage.sprite = pauseSprite;
            playPauseText.text = "Pause";
            recRep.play = true;
            slider.interactable = false;
        }

    }

    public void SetReplayFrame()
    {
        Debug.Log("Set slider value on pointer up delegate");
        try
        {
            recRep.sliderFrame = (int)slider.value;
            //sliderText.text = slider.value.ToString();

        }
        catch (System.Exception e)
        {

            Debug.Log("Really uncool... " + e.ToString());
           
        }
    }
    void Update()
    {
        Replay();
    }

    public void Replay()
    {
        if (recRep.replaying)
        {
            if (!recRep.play) // only display frame number during pause otherwise it looks confusing because text changes so fast
            {
                sliderText.text = slider.value.ToString();
                //Debug.Log(sliderText.text);
            }
            else
            {
                //Debug.Log(recRep.currentReplayFrame);
                sliderText.text = "";
                slider.value = recRep.currentReplayFrame;
                //Debug.Log(slider.value);
            }
        }
        else
        {
            infoSet = false;
            if (resetReplayImage)
            {
                replayImage.color = white;
                replayText.color = white;
                resetReplayImage = false;
                slider.interactable = false;
            }
        }
    }
}
