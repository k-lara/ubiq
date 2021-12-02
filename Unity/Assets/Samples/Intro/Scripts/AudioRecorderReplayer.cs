using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Voip;
using RecorderReplayerTypes;
using System;


[RequireComponent(typeof(RecorderReplayer))]
public class AudioRecorderReplayer : MonoBehaviour
{

    public RecorderReplayer recRep;
    public NetworkScene scene;

    private RoomClient roomClient;
    // audio recording
    private VoipPeerConnectionManager voipConnectionManager;
    private Dictionary<string, VoipPeerConnection> peerUuidToConnection;
    private Dictionary<string, AudioSinkManager> peerUuidToAudioSinkManager;
    private Dictionary<string, short> peerUuidToShort;
    private VoipMicrophoneInput audioSource; // audio from local peer who records 
    private bool initAudioFile = false;
    private BinaryWriter binaryWriterAudio;
    private AudioMessagePack audioMessages = null; // collects audio samples from several frames and gathers them in a pack for writing it to file

    private int frameNr = 0;
    private int frameX = 100; // after frameX frames write audio samples to file

    // Start is called before the first frame update
    void Start()
    {
        roomClient = GetComponent<RoomClient>();
        // get voippeerconnectionmanager to get audio source and sinks
        voipConnectionManager = recRep.scene.GetComponentInChildren<VoipPeerConnectionManager>();
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update when peers are added or removed
        peerUuidToAudioSinkManager = new Dictionary<string, AudioSinkManager>(); // update when peers are added or removed
        peerUuidToShort = new Dictionary<string, short>(); // fill anew for every new recording
        peerUuidToShort.Add(roomClient.Me.UUID, (short)peerUuidToShort.Count);

        audioSource = voipConnectionManager.audioSource; // local peer audio source
        audioSource.OnAudioSourceRawSample += AudioSource_OnAudioSourceRawSample;

        roomClient.OnPeerRemoved.AddListener(OnPeerRemoved);
        voipConnectionManager.OnPeerConnection.AddListener(OnPeerConnection, true);

        recRep.recorder.OnRecordingStopped += Recorder_OnRecordingStopped;

        // create audio message pack for local peer uuid
        audioMessages = new AudioMessagePack(peerUuidToShort[roomClient.Me.UUID]);
    }

    private void OnPeerRemoved(IPeer peer)
    {
        peerUuidToAudioSinkManager.Remove(peer.UUID);
        peerUuidToConnection.Remove(peer.UUID);
    }

    private void Recorder_OnRecordingStopped(object sender, EventArgs args)
    {
        Debug.Log("AudioRecorder OnRecordingStopped");
        peerUuidToShort.Clear();
        audioMessages.Clear();
        binaryWriterAudio.Dispose();
        foreach (var manager in peerUuidToAudioSinkManager.Values)
        {
            manager.Cleanup();
        }
        frameNr = 0;
    }

    public Dictionary<string, short> GetPeerUuidToShort() { return peerUuidToShort; }

    // need to know about peer UUIDs, and which avatar had which peer UUID so we can alter during replay assign the correct replayed avatars such that the audio sink positions match.
    // manages the audio sinks from different peer connections to keep track of which peer sends what
    private class AudioSinkManager
    {
        public AudioRecorderReplayer recRepA;
        public VoipPeerConnection pc;
        public VoipAudioSourceOutput audioSink;
        public Transform sinkTransform;
        public string peerUuid;
        public AudioMessagePack audioMessages;

        public AudioSinkManager(AudioRecorderReplayer recRepA, VoipPeerConnection pc)
        {
            this.pc = pc;
            audioSink = pc.audioSink;
            sinkTransform = audioSink.transform;
            peerUuid = pc.PeerUuid;
            this.recRepA = recRepA;

            audioMessages = new AudioMessagePack(recRepA.peerUuidToShort[peerUuid]);
            audioSink.OnAudioSourceRawSample += AudioSink_OnAudioSourceRawSample;
        }

        public void Cleanup()
        {
            audioMessages.Clear();
        }

        // record audio from peer connections
        private void AudioSink_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            if (recRepA.initAudioFile) // can only be true if recording is true and audio file has been initialised
            {
                // accumulate samples
                audioMessages.Add(sample);

                // after x frames, write audio sample pack to file
                if ((recRepA.frameNr % recRepA.frameX) == 0) 
                {
                    recRepA.binaryWriterAudio.Write(audioMessages.GetBytes());
                    audioMessages.Clear();
                }
            }
        }
    }

    // TODO: what happens when pc is destroyed... what to do with the respective AudioSinkManagers
    private void OnPeerConnection(VoipPeerConnection pc)
    {
        Debug.Log("AudioRecorder OnPeerConnection: " + pc.PeerUuid);
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update dictionary with new peer connection
        AudioSinkManager sinkManager = new AudioSinkManager(this, pc); // creates listener for raw audio samples
        peerUuidToAudioSinkManager.Add(pc.PeerUuid, sinkManager);
        peerUuidToShort.Add(pc.PeerUuid, (short)peerUuidToShort.Count);
    }

    // for replay fake peer connection
    private void AudioSource_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
    {
        if (recRep.recording)
        {
            // only init it on audio source, as audio source should send all the time anyways
            if (!initAudioFile)
            {
                Debug.Log("Init audio file");
                var dateTime = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                recRep.audioRecordFile = recRep.path + "/audio " + dateTime + ".dat";

                //idxFrameStart.Add(0); // first frame in byte data has idx 0

                binaryWriterAudio = new BinaryWriter(File.Open(recRep.audioRecordFile, FileMode.OpenOrCreate)); // dispose when recording is finished
                //recordingStartTime = Time.unscaledTime;

                initAudioFile = true;
            }
            else // do the audio recording
            {
                // accumulate samples
                audioMessages.Add(sample);

                // after x frames, write audio sample pack to file
                if ((frameNr % frameX) == 0)
                {
                    Debug.Log("Write sample pack at frame " + frameNr);
                    binaryWriterAudio.Write(audioMessages.GetBytes());
                    audioMessages.Clear();
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (recRep.recording)
        {
            frameNr++;
        }
    }
}
