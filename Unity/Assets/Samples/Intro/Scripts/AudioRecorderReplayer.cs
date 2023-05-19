using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Voip;
using RecorderReplayerTypes;
using System;
using System.Linq;
using Ubiq.Avatars;
using Ubiq.Spawning;
using Ubiq.Samples;


//[RequireComponent(typeof(RecorderReplayer))]
public class AudioRecorderReplayer : MonoBehaviour, INetworkObject, INetworkComponent
{
    public const int LATENCY = 200; // ms
    public const int SAMPLINGFREQ = 16000;
    public const int NUMSAMPLES = SAMPLINGFREQ; // * 2;
    public static bool MASTERONLY = false;
    private bool audioDataAvailable = false;
    //public NetworkId Id => new NetworkId("a647002b-053e-4585-8cdc-7e2bd17b0ec2");
    public NetworkId Id => new NetworkId("fc127356-581e8e54");

    // need to know about peer UUIDs, and which avatar had which peer UUID so we can alter during replay assign the correct replayed avatars such that the audio sink positions match.
    // manages the audio sinks from different peer connections to keep track of which peer sends what

    private void OnPeerConnection(VoipPeerConnection pc)
    {
        Debug.Log("AudioRecorder OnPeerConnection: " + pc.PeerUuid);
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update dictionary with new peer connection
        AudioRecorderSinkManager sinkManager = new AudioRecorderSinkManager(this, pc); // creates listener for raw audio samples
        sinkManager.SetClipNumber(CLIPNUMBER++);
        peerUuidToAudioSinkManager.Add(pc.PeerUuid, sinkManager); // list of all the sink managers
        
        //peerUuidToShort.Add(pc.PeerUuid, (short)peerUuidToShort.Count);
    }
    private void OnPeerRemoved(IPeer peer)
    {
        peerUuidToAudioSinkManager.Remove(peer.UUID);
        peerUuidToConnection.Remove(peer.UUID);
        //peerUuidToShort.Remove(peer.UUID);
    }
    private static short CLIPNUMBER = 0; // used to distinguish different audio clips from different peers 
    private short sourceCLIPNUMBER;

    public RecorderReplayer recRep;
    public NetworkScene scene;

    private NetworkContext context;
    private RoomClient roomClient;
    private AvatarManager avatarManager;

    // audio recording
    private VoipPeerConnectionManager voipConnectionManager;
    private Dictionary<string, VoipPeerConnection> peerUuidToConnection; // do not clear after recording
    private Dictionary<string, AudioRecorderSinkManager> peerUuidToAudioSinkManager; // do not clear after recording
    private Dictionary<NetworkId, short> objectidToClipNumber; // should be fine for replays of replays, also this is what we save as metadata
    private VoipMicrophoneInput audioSource; // audio from local peer who records 
    public bool initAudioFile = false; // AudioRecorderSinkManager needs it
    public BinaryWriter binaryWriterAudio; // AudioRecorderSinkManager needs it
    private List<byte[]> audioMessages = null; // collects audio samples from several frames and gathers them in a pack for writing it to file
    private List<int> audioClipLengths = new List<int>();
    public int frameNr = 0;
    public int frameX = 100; // after frameX frames write audio samples to file
    private int samplesLength = 0; // length of all current recorded samples
    private int samplesLengthUntilNextWrite = 0;
    //private string testAudioFile = "testAudio"; // test file saving float values to check if data is correct
    //private StreamWriter testStreamWriter = null;
    private List<short[]> testSamples = new List<short[]>();
    private byte[] u = new byte[2]; // clip number

    // audio replay
    private NetworkSpawner spawner;
    private Dictionary<NetworkId, short> objectidToClipNumberReplay = null;
    public Dictionary<short, AudioSource> replayedAudioSources = null;
    private Dictionary<short, AudioClip> fromRemoteReplayedClips = null; // only remote peers should use this to save clip data before the actual audio source is created
    public Dictionary<short, SpeechIndicator> speechIndicators = null;
    private Dictionary<short, int> clipNumberToLatency = null;
    [SerializeField]
    public int[] latenciesMs;
    //[SerializeField]
    //public int[] latenciesSamples;
    [SerializeField]
    public bool[] mute = null;
    private bool startReadingFromFile = false;

    private Dictionary<short, int> audioClipPositions = null;
    private Dictionary<short, int> audioClipLengthsReplay = null;
    private FileStream audioFileStream = null;
    private Dictionary<short, int> replayedAudioClipsStartIndices = null; // when recording a replay to know from where to record the replay
    private Dictionary<short, int> replayedAudioClipsRecordedLength = null; // when recording a replay to know until when to record a replay
    private bool pressedPlayFirstTime = false;
    private float refTime = 0.0f;
    //private float currentTime = 0.0f;
    private int[] currentTimeSamplesPerClip;

    // audio clip creation
    private float gain = 1.0f;

    private float replayLength;
    private List<float> frameTimes;
    private List<AudioIndicator> audioIndicators; // contain the RawImage components for the textures
    private List<Texture2D> waveformTextures;
    private List<Texture2D> pointersToSamples;
    private int[] prevPointerPos;
    private int width = 500;
    private int height = 100;
    private Color col = Color.black;
    private Color p1 = Color.black;
    private Color p2 = Color.blue;
    private Color p3 = Color.green;
    private Color p4 = Color.magenta;
    private Color p5 = Color.cyan;
    private Color[] playerCols;
    private Color transparent = Color.clear;
    private Color pointerCol = Color.red;
    private Color[] pointerCols;
    private Color[] pointerColsClear;
    private int thickness = 2; // number of pixels in width for thickness of line

    // replay test file
    //private string testAudioFileReplay = "testAudioReplay"; // test file saving float values to check if data is correct
    //private StreamWriter testStreamWriterReplay = null;

    // sets clip number for local peer in short and in bytes
    // needs to be called before every recording!!!
    private void SetClipNumber(short CLIPNUMBER)
    {
        sourceCLIPNUMBER = CLIPNUMBER;
        u = BitConverter.GetBytes(sourceCLIPNUMBER);
    }

    // Start is called before the first frame update
    void Start()
    {
        playerCols = new Color[] { p1, p2, p3, p4, p5, p1, p2, p3, p4, p5, p1, p2, p3, p4, p5};
        //scene = GetComponent<NetworkScene>();
        recRep = GetComponent<RecorderReplayer>();
        roomClient = scene.GetComponent<RoomClient>();
        voipConnectionManager = scene.GetComponentInChildren<VoipPeerConnectionManager>();
        avatarManager = scene.GetComponentInChildren<AvatarManager>();
        spawner = scene.GetComponentInChildren<NetworkSpawner>();
        context = scene.RegisterComponent(this);
        // get voippeerconnectionmanager to get audio source and sinks
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update when peers are added or removed
        peerUuidToAudioSinkManager = new Dictionary<string, AudioRecorderSinkManager>(); // update when peers are added or removed
        //peerUuidToShort = new Dictionary<string, short>(); // fill anew for every new recording
        objectidToClipNumber = new Dictionary<NetworkId, short>();
        replayedAudioSources = new Dictionary<short, AudioSource>();
        fromRemoteReplayedClips = new Dictionary<short, AudioClip>();
        speechIndicators = new Dictionary<short, SpeechIndicator>();
        clipNumberToLatency = new Dictionary<short, int>(); 
        audioClipPositions = new Dictionary<short, int>();
        replayedAudioClipsStartIndices = new Dictionary<short, int>();
        replayedAudioClipsRecordedLength = new Dictionary<short, int>();
        //peerUuidToShort.Add(roomClient.Me.UUID, CLIPNUMBER++); // this needs to remain there for the whole session. do not remove when clearing after recording
        //uuid = peerUuidToShort[roomClient.Me.UUID]; // should be 0 for local peer

        audioSource = voipConnectionManager.audioSource; // local peer audio source
        audioSource.OnAudioSourceRawSample += AudioSource_OnAudioSourceRawSample;
        roomClient.OnPeerRemoved.AddListener(OnPeerRemoved);
        voipConnectionManager.OnPeerConnection.AddListener(OnPeerConnection, true);
        recRep.recorder.OnRecordingStopped += Recorder_OnRecordingStopped;
        recRep.replayer.OnReplayStopped += Replayer_OnReplayStopped;
        recRep.replayer.OnReplayRepeat += Replayer_OnReplayRepeat;
        //recRep.replayer.OnLoadingReplay += Replayer_OnLoadingReplay;

        // create audio message pack for local peer uuid
        audioMessages = new List<byte[]>();
        audioMessages.Add(new byte[4]); // length of pack (int)
        audioMessages.Add(new byte[2]); // clip number (short)

        SetClipNumber(CLIPNUMBER++); // sets it first and then increments 

        pointerCols = new Color[height * thickness];
        pointerColsClear = new Color[height * thickness];
        for (int i = 0; i < pointerCols.Length; i++)
        {
            pointerCols[i] = pointerCol;
            pointerColsClear[i] = transparent;
        }

    }

    public FileStream GetAudioFileStream()
    {
        return audioFileStream;
    }

    public Dictionary<short, int> GetLatencies()
    {
        return clipNumberToLatency;
    }
    public Dictionary<short, AudioSource> GetReplayAudioSources()
    {
        return replayedAudioSources;
    }

    // mute all other clips apart from master (first) clip, this is usually clip 0 and is usually last in every list or array
    public void MuteAllButMasterClip(bool masterOnly)
    {
        MASTERONLY = masterOnly;
        for (var i = 0; i < mute.Length - 1; i++) // mute every clip but last
        {
            mute[i] = masterOnly;
        } 
        
    }

    public void SetLatencies(int currentLatency)
    {
        var i = 0;
        var r = replayedAudioSources.Count; // last index (because the last audio source is the one that got recorded first and has least latency)
        foreach (var item in replayedAudioSources)
        {
            //var latency = ComputeLatencySamples(latenciesMs[i]);
            //int multiplier = Mathf.RoundToInt(Mathf.Pow(2.0f, r));
            int latency = 0;
            if (i == 0)
            {
                latency = ComputeLatencySamples(currentLatency);
                latenciesMs[i] = currentLatency;

            }
            else
            {
                latency = ComputeLatencySamples(currentLatency);
                latenciesMs[i] = currentLatency;

            }
            //var latency = ComputeLatencySamples(currentLatency);

            if (item.Value.timeSamples > 0) // if clip was already playing but latency is adapted afterwards remove previous latency and add new latency
            {
                var newTimeSamples = item.Value.timeSamples - clipNumberToLatency[item.Key] + latency;
                item.Value.timeSamples = newTimeSamples;
            }
            clipNumberToLatency[item.Key] = latency;

            // speech indicators need to know about latency too otherwise there is an error
            speechIndicators[item.Key].SetLatencySamples(latency);
            if (r > 1) // the first one already has a bit of latency too!
            {
                r--;
            }

            i++;
        }
        SendAudioMessage(new AudioMessage() { messageId = 2, timeSamples = clipNumberToLatency.Values.ToArray(), muteClips = mute });
        Debug.Log("Latencies: " + string.Join(", ", latenciesMs));
        Debug.Log("Muted: " + string.Join(", ", mute));
    }

    // computes the number of samples that should be skipped at the beginning of the clip to account for latencies
    // 16000 Hz > 16 samples per ms 
    public int ComputeLatencySamples(int ms)
    {
        return (SAMPLINGFREQ / 1000) * ms;
    }

    // recording
    private void AudioSource_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
    {
        if (recRep.recording)
        {
            // only init it on audio source, as audio source should send all the time anyways
            if (!initAudioFile)
            {
                if (recRep.replaying) // make sure to increment the clip number for all peers
                {
                    SetClipNumber(CLIPNUMBER++); // increase clip number for the newest recording (newest is always highest), first one should always be 0
                    foreach ( var sink in peerUuidToAudioSinkManager.Values )
                    {
                        sink.SetClipNumber(CLIPNUMBER++);
                    }
                    foreach( var item in replayedAudioSources) // record replayed audio too
                    {
                        replayedAudioClipsStartIndices.Add(item.Key, item.Value.timeSamples);
                        replayedAudioClipsRecordedLength.Add(item.Key, 0);
                    }
                }

                Debug.Log("Init audio file");
                //var dateTime = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"); // not good, sometimes dateTime is one hundredth too late
                recRep.audioRecordFile = recRep.path + "/audiorec" + recRep.recordingStartTimeString + ".dat";

                binaryWriterAudio = new BinaryWriter(File.Open(recRep.audioRecordFile, FileMode.OpenOrCreate)); // dispose when recording is finished
                //testStreamWriter = new StreamWriter(recRep.path + "/" + testAudioFile + ".csv");

                initAudioFile = true;
            }
            // do the audio recording
            //Debug.Log("sample length: " + sample.Length);
            samplesLength += sample.Length;
            samplesLengthUntilNextWrite += sample.Length;
            // test
            testSamples.Add(sample);
            //testStreamWriter.WriteLine(string.Join(", ", sample) + ",");

            // accumulate samples
            var tempSamples = new byte[sample.Length * sizeof(short)];
            for (var i = 0; i < sample.Length; i++)
            {
                var tmpSmpl = BitConverter.GetBytes(sample[i]);
                tempSamples[i * 2] = tmpSmpl[0];
                tempSamples[i * 2 + 1] = tmpSmpl[1];
            }

            audioMessages.Add(tempSamples);

            // after x frames, write audio sample pack to file
            //if ((frameNr % frameX) == 0)
            if (samplesLengthUntilNextWrite >= NUMSAMPLES )
            {
                //Debug.Log("Write audio data at frame: " + frameNr);
                var arr = audioMessages.SelectMany(a => a).ToArray();
                //Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
                byte[] l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
                arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
                arr[4] = u[0]; arr[5] = u[1];
                binaryWriterAudio.Write(arr);
                //testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + sourceCLIPNUMBER + ", " + string.Join(", ", testSamples.SelectMany(a => a).ToArray()) + ",");
                audioMessages.Clear();
                testSamples.Clear();
                audioMessages.Add(new byte[4]); // length of pack (int)
                audioMessages.Add(u); // clip number (short)
                samplesLengthUntilNextWrite = 0;

                WriteReplayedClipsToFile(); // running clips from previous recording if there are any
            }
        }
    }
    private void WriteReplayedClipsToFile()
    {
        foreach (var item in replayedAudioSources)
        {
            var diff = item.Value.timeSamples - replayedAudioClipsStartIndices[item.Key]; // how many samples clip has advanced since start of recording
            //Debug.Log("Diff " + diff + "- start index: " +  replayedAudioClipsStartIndices[item.Key] + " u: " + item.Key);
            if (diff == 0)
            {
                continue;
            }
            if (diff < 0)
            {
                diff = item.Value.clip.samples - replayedAudioClipsStartIndices[item.Key] + item.Value.timeSamples;
            }
            var floatSamples = new float[diff];
            var byteSamples = new byte[diff * 2 + 6]; // from short + length + clipNr
            var l = BitConverter.GetBytes(byteSamples.Length - 4); // pckg length without inlcluding 4 bytes for int pckg length
            var u = BitConverter.GetBytes(item.Key);
            //Debug.Log("replay u: " + u);
            byteSamples[0] = l[0]; byteSamples[1] = l[1]; byteSamples[2] = l[2]; byteSamples[3] = l[3];
            byteSamples[4] = u[0]; byteSamples[5] = u[1];
            item.Value.clip.GetData(floatSamples, replayedAudioClipsStartIndices[item.Key]);
            
            for (int i = 0; i < floatSamples.Length; i++)
            {
                var sample = floatSamples[i];
                sample = Mathf.Clamp(sample * gain, -.999f, .999f);
                var b = BitConverter.GetBytes((short)(sample * short.MaxValue));
                byteSamples[i*2+6] = b[0]; byteSamples[i * 2 + 7] = b[1];
                //Debug.Log(i * 2 + 6);
            }
            replayedAudioClipsStartIndices[item.Key] = item.Value.timeSamples;
            replayedAudioClipsRecordedLength[item.Key] += diff; 
            binaryWriterAudio.Write(byteSamples);
            //testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + item.Key + ", " + string.Join(", ", floatSamples) + ", ");

        }
    }

    public (Dictionary<NetworkId, short>, List<int>) GetAudioRecInfoData()
    {
        Debug.Log("GetAudioRecInfoData()");
        foreach (var avatar in avatarManager.Avatars)
        {
            if (avatar.Peer.UUID == roomClient.Me.UUID)
            {
                objectidToClipNumber.Add(avatar.Id, sourceCLIPNUMBER);
                audioClipLengths.Add(samplesLength);
            }
            else
            {
                Debug.Log("Remote peer: " + avatar.Id.ToString() + " " + peerUuidToAudioSinkManager[avatar.Peer.UUID].sinkCLIPNUMBER + " " + peerUuidToAudioSinkManager[avatar.Peer.UUID].samplesLength);
                objectidToClipNumber.Add(avatar.Id, peerUuidToAudioSinkManager[avatar.Peer.UUID].sinkCLIPNUMBER);
                audioClipLengths.Add(peerUuidToAudioSinkManager[avatar.Peer.UUID].samplesLength);
            }
        }
        // if replayedAudioSources != null then this should mean that we just did a recording of a replay and need to store this data too
        if (replayedAudioSources != null)
        {
            foreach (var item in replayedAudioSources)
            {
                Debug.Log("replayed audio sources (rec info data): " + item.Key);
                var avatar = item.Value.gameObject.transform.parent.parent.gameObject.GetComponent<Ubiq.Avatars.Avatar>();
                //Debug.Log("old replay: " + avatar.Id.ToString() + " " + item.Key + " " + replayedAudioClipsRecordedLength[item.Key]);
                objectidToClipNumber.Add(avatar.Id, item.Key);
                audioClipLengths.Add(replayedAudioClipsRecordedLength[item.Key]);
            }
        }
                
        return (objectidToClipNumber, audioClipLengths);
    }

    // event is invoked after audio recording info (objectidsToShort) is saved ( edit: SO WHY AM I SUCH A LULI AND DO ALL THE FILE WRITING HERE!)
    public void WriteLastSamplesOnRecordingStopped()
    {
        Debug.Log("AudioRecorder OnRecordingStopped");
        //Debug.Log("Write audio data at frame: " + frameNr);
        var arr = audioMessages.SelectMany(a => a).ToArray();
        if (arr.Length > 6) // 4 bytes for int package length and 2 bytes for short clip number (if > 6 audioMessages also has data, otherwise not)
        {
            //Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
            var l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
            arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
            arr[4] = u[0]; arr[5] = u[1];
            binaryWriterAudio.Write(arr);
            //testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + sourceCLIPNUMBER + ", " + string.Join(", ", testSamples.SelectMany(a => a).ToArray()) + ",");
        }
        audioMessages.Clear();
        samplesLengthUntilNextWrite = 0;

        foreach (var manager in peerUuidToAudioSinkManager.Values) // write last samples from audio sink manager
        {
            manager.WriteRemainingAudioData();
        }
        if (replayedAudioSources != null) // write new replay to file again
        {
            Debug.Log("Write replayed audio data");
            WriteReplayedClipsToFile();
        }

    }
    // must be called last
    private void Recorder_OnRecordingStopped(object obj, EventArgs e)
    {
        Debug.Log("AudioRecorderReplayer: Recorder_OnRecordingStopped");
        testSamples.Clear();
        audioClipLengths.Clear();
        //peerUuidToShort.Clear(); do not clear as it has also the uuid of the local peer which needs to remain for the whole session
        objectidToClipNumber.Clear();
        //testStreamWriter.Dispose(); // dispose at the end as SinkManagers also need to save rest data to file
        initAudioFile = false;
        frameNr = 0;
        samplesLength = 0;
        pressedPlayFirstTime = false;

        if (binaryWriterAudio != null)
            binaryWriterAudio.Dispose();
        replayedAudioClipsStartIndices.Clear(); // when recording a replay
        replayedAudioClipsRecordedLength.Clear();
        clipNumberToLatency.Clear();
    }
    private void PlayAndConsiderLatency(AudioSource audioSource, int samples)
    {
        audioSource.Play();
        audioSource.timeSamples = samples;
        Debug.Log("Play and consider latency " + samples);
    }

    // this is used specifically for the single user case to make recordings of replays easier
    public void PlayFromStartConsiderLatency()
    {
        foreach (var item in replayedAudioSources)
        {
            if (item.Value.isPlaying)
            {
                item.Value.Stop(); // stop means it plays from the beginning (as opposed to pause)
            }
            //refTime = Time.unscaledTime; not sure if i need this here
            PlayAndConsiderLatency(item.Value, clipNumberToLatency[item.Key]);
        }
    }

    private void PlayPause(bool play)
    {
        //Debug.Log(mute.Length + " " + replayedAudioSources.Count);
        int i = 0;
        if (mute.Length == 0)
        {
            Debug.Log("No mute set");
            mute = new bool[replayedAudioSources.Count];
        }
        if (play)
        {
            //Debug.Log("OnPlay: " + replayedAudioSources.Count);
            foreach (var item in replayedAudioSources)
            {
                item.Value.mute = mute[i];
                if (!pressedPlayFirstTime)
                {
                    refTime = Time.unscaledTime;
                    PlayAndConsiderLatency(item.Value, clipNumberToLatency[item.Key]);
                }
                else
                {
                    item.Value.UnPause();
                    refTime = Time.unscaledTime;
                }
                i++;
            }
            pressedPlayFirstTime = true;
        }
        else
        {
            Debug.Log("OnPause");

            foreach (var item in replayedAudioSources)
            {
                item.Value.mute = mute[i];
                item.Value.Pause();
                i++;
            }
        }
    }

    // is called in RecorderReplayerMenu
    public void OnPlayPauseReplay(bool play)
    {
        //Debug.Log("OnPlayPauseReplay");
        currentTimeSamplesPerClip = new int[replayedAudioSources.Count];
        int i = 0;
        foreach(var item in replayedAudioSources)
        {
            currentTimeSamplesPerClip[i] = item.Value.timeSamples;
            i++;
        }
        //var ppm = JsonUtility.ToJson(new PlayPauseMessage() { play = play, timeSamples = currentTimeSamplesPerClip });
        //context.SendJson(new Message() { id = 3, messageType = ppm });
        SendAudioMessage(new AudioMessage() { messageId = 3, timeSamples = currentTimeSamplesPerClip, play = play } );
        PlayPause(play);
    }
    // is called by RecorderReplayer during pause and when user jumps to a specific frame in the replay.
    public void JumpToFrame(int currentFrame, int numberOfFrames)
    {
        //Debug.Log("ARecRep Jump to Frame: current, total " + currentFrame + " " + numberOfFrames);
        int[] jumpSamples = new int[replayedAudioSources.Count];
        int i = 0;
        foreach (var item in replayedAudioSources)
        {
            // calculate current timeSample and add offset from latency computation
            int jumpSample = (int)((currentFrame / (float)numberOfFrames) * (item.Value.clip.samples)) + clipNumberToLatency[item.Key];

            //Debug.Log((currentFrame / (float)numberOfFrames) + " " + clipNumberToLatency[item.Key] + " Jump to " + jumpSample);
            jumpSamples[i] = jumpSample;
            item.Value.timeSamples = jumpSample;

            i++;
        }

        //var jm = JsonUtility.ToJson(new JumpMessage() { jumpSamples = jumpSamples });
        //context.SendJson(new Message() { id = 7, messageType = jm });
        SendAudioMessage(new AudioMessage() { messageId = 7, timeSamples = jumpSamples});
    }

    // gets called once recording info is loaded in the Replayer and replayed objects are created!
    public void OnLoadingReplay(RecordingInfo recInfo)
    {
        string filepath = recRep.path + "/audio" + recRep.replayFile + ".dat";
        Debug.Log("Audiorec filepath: " + filepath);
        if (File.Exists(filepath))
        {
            //Debug.Log("Get audio file...");

            objectidToClipNumberReplay = recInfo.objectidsToClipNumber.Zip(recInfo.clipNumber, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            audioClipLengthsReplay = recInfo.clipNumber.Zip(recInfo.audioClipLengths, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            latenciesMs = new int[audioClipLengthsReplay.Count];
            mute = new bool[audioClipLengthsReplay.Count];
            clipNumberToLatency.Clear();
            MuteAllButMasterClip(MASTERONLY);
            // increase the CLIPNUMBER to the number of already existing replays so a subsequent recording has the correct clip number
            CLIPNUMBER = (short)audioClipLengthsReplay.Count; // make sure to set it back to 0 in case we do not record a replay but start anew

            audioFileStream = File.Open(filepath, FileMode.Open); // open audio byte file for loading audio data into clips
            //foreach (var item in audioClipLengthsReplay)
            //{
            //    Debug.Log("OnLoadingReplay" + item.Key + " " + item.Value);
            //}
            audioIndicators = new List<AudioIndicator>();
            foreach (var item in objectidToClipNumberReplay)
            {
                // get new object id and add audio source to respective game object
                var newId = recRep.replayer.oldNewIds[item.Key];
                var clipLength = audioClipLengthsReplay[item.Value];
                // remotely
                //var cm = JsonUtility.ToJson(new CreateMessage() { id = newId, clipNr = item.Value, clipLength = clipLength });
                //context.SendJson(new Message() { id = 0, messageType = cm });
                SendAudioMessage(new AudioMessage() { messageId = 0, objectId = newId, clipNr = item.Value, clipLengthPos = clipLength});
                //locally
                CreateAudioClip(newId, item.Value, clipLength);

                //audioSource.Play();
                //float[] testClipData = new float[audioClipLengthsReplay[0]];
                //replayedAudioSources[0].clip.GetData(testClipData, 0);
                //File.WriteAllText(recRep.path + "/" + "testClipData" + ".csv", string.Join(", ", testClipData));
            }
            startReadingFromFile = true;
            audioDataAvailable = true;
            //await ReadAudioDataFromFile();
            //OnLoadAudioDataComplete.Invoke(this, EventArgs.Empty);
            
            Debug.Log("AudioClips created!");
            //return true;
        }
        else
        {
            audioDataAvailable = audioDataAvailable || false;
            Debug.Log("Invalid audio file path or no audio file path exists!");
            //recRep.replaying = false; // should still be possible to replay even without audio
            //return false;
        }
    }
    // creates and audio clip with number clipNr and length clipLength and attaches it to an object with NetworkId id
    private void CreateAudioClip(NetworkId id, short clipNr, int clipLength)
    {
        //Debug.Log("AudioRecorderReplayer CreateAudioClip");
        var gameObject = spawner.spawned[id];
        var audioSource = gameObject.transform.GetChild(0).GetChild(0).gameObject.AddComponent<AudioSource>(); // add this to head!!!
        var speechIndicator = gameObject.GetComponentInChildren<SpeechIndicator>();
        speechIndicator.SetReplayAudioSource(audioSource);
        speechIndicators.Add(clipNr, speechIndicator);

        // for displaying waveforms
        var ai = gameObject.GetComponentInChildren<AudioIndicator>();
        
        // do not show audio indicators and markers in presentation mode
        if (recRep.experiment.mode != ReplayMode.SingleUser)
        {
            ai.gameObject.transform.GetChild(0).gameObject.SetActive(false); // deactivate canvas game object to hide audio and marker info
        }
        else
        { // show indicators in single mode
            ai.gameObject.transform.GetChild(0).gameObject.SetActive(true);
        }
        audioIndicators.Add(ai);

        audioSource.clip = AudioClip.Create(
        name: "AudioClip " + clipNr + " id: " + id.ToString(),
        lengthSamples: clipLength, // length is correct
        channels: 1,
        frequency: SAMPLINGFREQ,
        stream: false);
        audioSource.ignoreListenerPause = false;
        audioSource.spatialBlend = 1.0f;
        //audioSource.Play();
        Debug.Log(audioSource.clip.name + " length: " + clipLength);
        replayedAudioSources.Add(clipNr, audioSource);
        audioClipPositions.Add(clipNr, 0);
        clipNumberToLatency.Add(clipNr, 0);

        //recRep.marker.CreateMarkerCanvas(id, ai, LATENCY);
        if (recRep.experiment.mode == ReplayMode.SingleUser)
        {
            recRep.marker.AddClipNumber(id, clipNr);
            replayLength = recRep.marker.GetReplayLength();
            frameTimes = recRep.marker.GetFrameTimes();
        }
    }

    private void CreateRemoteAudioClip(NetworkId id, short clipNr, int clipLength)
    {
        Debug.Log("AudioRecorderReplayer CreateRemoteAudioClip");
        objectidToClipNumber.Add(id, clipNr); // to know which clip to assign to which object once they are created
        fromRemoteReplayedClips[clipNr] = AudioClip.Create(
        name: "AudioClip " + clipNr + " id: " + id.ToString(),
        lengthSamples: clipLength, // length is correct
        channels: 1,
        frequency: SAMPLINGFREQ,
        stream: false);
        audioClipPositions.Add(clipNr, 0);
        clipNumberToLatency.Add(clipNr, 0);

        StartCoroutine(AssignAudioSourceOnceObjectExists(id, clipNr));
    }

    private IEnumerator AssignAudioSourceOnceObjectExists(NetworkId id, short clipNr)
    {
        GameObject gameObject = null;
        Debug.Log("Wait until object " + id.ToString() + " is created...");
        yield return new WaitUntil(() => spawner.spawned.TryGetValue(id, out gameObject));
        var audioSource = gameObject.transform.GetChild(0).GetChild(0).gameObject.AddComponent<AudioSource>();
        audioSource.clip = fromRemoteReplayedClips[clipNr];
        audioSource.ignoreListenerPause = false;
        audioSource.spatialBlend = 1.0f;
        replayedAudioSources.Add(clipNr, audioSource);
        var speechIndicator = gameObject.GetComponentInChildren<SpeechIndicator>();
        speechIndicator.SetReplayAudioSource(audioSource);
        speechIndicators.Add(clipNr, speechIndicator);
        Debug.Log("Remote object " + id.ToString() + " created and AudioSource added!");
    }

    // iterations specifies how often the while loop should iterate during one Update() call
    private AudioMessage ReadAudioDataFromFile(int iterations)
    {
        //Debug.Log("AudioReplayer: ReadAudioDataFromFile");
        int iter = 0;
        int clipPos; 
        byte[] pckgLength = new byte[4];
        byte[] clipNumber = new byte[2];
        byte[] audioPckg;
        AudioMessage audioMessage = new AudioMessage();
        //int test = 0;
        while (iter < iterations)
        {
            if (audioFileStream.Position < audioFileStream.Length)
            //while (audioFileStream.Position < audioFileStream.Length)
            {
                //Debug.Log("stream position " + audioFileStream.Position);
                audioFileStream.Read(pckgLength, 0, 4);
                //Debug.Log("stream position " + audioFileStream.Position);
                audioFileStream.Read(clipNumber, 0, 2);
                //Debug.Log("stream position " + audioFileStream.Position);

                int l = BitConverter.ToInt32(pckgLength, 0) - 2; // pckgLength/2 = length samples
                short s = BitConverter.ToInt16(clipNumber, 0);
                //Debug.Log("checked original" + l + " " + s);
                clipPos = audioClipPositions[s];

                //Debug.Log("sizes: " + l + " " + s);
                audioPckg = new byte[l]; // contains audio data without bytes for short "uuid"
                audioFileStream.Read(audioPckg, 0, audioPckg.Length);
                //Debug.Log("stream position " + audioFileStream.Position);

                // convert samples to float
                float[] floatSamples = new float[audioPckg.Length / 2];
                for (int i = 0; i < audioPckg.Length; i+=2)
                {
                    short sample = BitConverter.ToInt16(audioPckg, i);
                    //testStreamWriterReplay.Write(sample + ",");

                    var floatSample = ((float)sample) / short.MaxValue;
                    floatSamples[i/2] = Mathf.Clamp(floatSample * gain, -.999f, .999f);
                }
                // set audio data in audio clip
                //Debug.Log("AudioClip positions: " + s + " " + clipPos);
                audioMessage = new AudioMessage() { messageId = 1, clipNr = s, clipLengthPos = audioClipPositions[s], samples = audioPckg };
                SendAudioMessage(audioMessage);
                
                replayedAudioSources[s].clip.SetData(floatSamples, clipPos);   
                audioClipPositions[s] += floatSamples.Length; // advance position

            }
            iter++;
        }
        if (audioFileStream.Position >= audioFileStream.Length)
        {
            Debug.Log("Finished reading audio data!");
            if (recRep.experiment.mode == ReplayMode.Presentation)
            {
                //recRep.loadingInfoText.text = "Loading finished!";
                //recRep.loadingInfoText.color = Color.clear;
            }
            else
            {
                recRep.loadingInfoText.text = "Loading finished!";
                StartCoroutine(recRep.marker.FadeTextToZeroAlpha(1.5f, recRep.loadingInfoText));
                //recRep.loadingInfoText.CrossFadeAlpha(0.0f, 2.0f, false);
            }
            startReadingFromFile = false;
            
            SetLatencies(LATENCY);
            // in experiment mode show all the audio indicators and markers
            if (recRep.experiment.mode == ReplayMode.SingleUser)
            {
                waveformTextures = new List<Texture2D>();
                pointersToSamples = new List<Texture2D>();
                pointersPackSize = new List<float>();
                prevPointerPos = new int[replayedAudioSources.Count];

                // add waveform to a canvas and show it to the user
                int i = 0;
                foreach (var item in replayedAudioSources)
                {
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    Texture2D texTransparent = new Texture2D(width, height, TextureFormat.RGBA32, false);

                    var latency = clipNumberToLatency[item.Key];
                    Debug.Log("Waveform Latency: " + latency + " clip: " + item.Key);

                    SetTextureBackground(tex, texTransparent, Color.clear, width, height);
                    DrawWaveform(tex, texTransparent, item.Value.clip, latency,  width, height, playerCols[i%playerCols.Length]);
                
                    // add finished texture to audioIndicators
                    audioIndicators[i].waveformTex.texture = waveformTextures[i];
                    audioIndicators[i].pointerTex.texture = pointersToSamples[i];

                    Debug.Log("replay lenghts, motion, audio, samples/sec: " + replayLength + " " + item.Value.clip.length + " " +  item.Value.clip.samples/item.Value.clip.length);
                    recRep.marker.CreateMarkerCanvas(item.Key, audioIndicators[i]);
                                
                    i++;
                }
            }
        }
        return audioMessage;
        //testStreamWriterReplay.Dispose();
    }

    public void SetTextureBackground(Texture2D tex, Texture2D texTransparent, Color col, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tex.SetPixel(x, y, col);
                texTransparent.SetPixel(x, y, Color.clear);
            }
        }
    }

    public void DrawWaveform(Texture2D tex, Texture2D texTransparent, AudioClip clip, int latency, int width, int height, Color col)
    {
        //Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        //Texture2D texTransparent = new Texture2D(width, height, TextureFormat.RGBA32, false);
        // draw the original waveform without any latency considerations
        // because what we see should be correct, only what we hear has to be corrected
        
        int audioLength = Mathf.RoundToInt(clip.frequency * replayLength);
        Debug.Log("clip samples vs new samples: " + clip.samples + " " + audioLength);

        //float[] samples = new float[clip.samples * clip.channels];
        float[] samples = new float[audioLength * clip.channels];
        float[] waveform = new float[width];

        clip.GetData(samples, 0); // get samples for length of replayLength

        float packSize = ((float)samples.Length / (float)width);
        int s = 0;

        for (float i = 0; Mathf.RoundToInt(i) < samples.Length-packSize && s < waveform.Length; i+=packSize)
        {
            float sAvg = 0.0f;
            for (int j = 0; j < packSize; j++)
            {
                sAvg += Mathf.Abs(samples[Mathf.RoundToInt(i) + j]);
            }
            sAvg /= packSize;
            waveform[s] = sAvg;
            //waveform[s] = Mathf.Abs(samples[Mathf.RoundToInt(i)]);
            s++;
        }

        for (int x = 0; x < waveform.Length; x++)
        {
            //Debug.Log(waveform[x] + " " + (float)height * 0.75f +  " " + (float)height);
            for (int y = 0; y <= waveform[x] * ((float)height * 4); y++)
            {
                tex.SetPixel(x, (height / 2) + y, col);
                tex.SetPixel(x, (height / 2) - y, col);
            }
        }
        tex.Apply();
        texTransparent.Apply();

        waveformTextures.Add(tex);
        pointersToSamples.Add(texTransparent);
    }

    private const int MAXPCKGS = 48000;
    private int pckgSamples = MAXPCKGS;
    private float deltaReadingTime = 0.025f; // 25ms
    private float startTime = 0.0f;
    // Update is called once per frame
    void Update()
    {
        if (roomClient.Me["creator"] == "1")
        {
            if (audioDataAvailable) // otherwise don't bother
            {
                if (recRep.recording)
                {
                    frameNr++;
                }
                if (startReadingFromFile)
                {
                    if (deltaReadingTime >= 0.025f)
                    {
                        //if (pckgSamples > 0)
                        //{
                            AudioMessage amsg = ReadAudioDataFromFile(3);
                            //if (amsg.samples == null)
                            //{
                            //    break;
                            //}
                            //SendAudioMessage(amsg);
                            //pckgSamples -= amsg.samples.Length;
                       
                        //}
                        //pckgSamples = MAXPCKGS;
                        startTime = Time.unscaledTime;
                    }

                    deltaReadingTime = Time.unscaledTime - startTime;
                }

                if (recRep.replaying && recRep.play && (Time.unscaledTime - refTime) >= 5.0f)
                {
                    refTime = 0.0f;
                    currentTimeSamplesPerClip = new int[replayedAudioSources.Count];
                    int i = 0;
                    foreach (var item in replayedAudioSources)
                    {
                        currentTimeSamplesPerClip[i] = item.Value.timeSamples;
                        i++;
                    }
                    //var sm = JsonUtility.ToJson(new SyncMessage() { timeSamples = currentTimeSamplesPerClip });
                    //context.SendJson(new Message() { id = 6, messageType = sm });
                    SendAudioMessage(new AudioMessage() { messageId = 6, timeSamples = currentTimeSamplesPerClip});
                }

                // only draw pointer on audio indicator in experiment mode
                if (recRep.experiment.mode == ReplayMode.SingleUser)
                {
                    //Debug.Log(recRep.replaying + " " + recRep.play + " " + audioDataAvailable + " " + !startReadingFromFile);
                    // when replay is playing draw a line on the audio texture for the current position
                    if (recRep.replaying && recRep.play && audioDataAvailable && !startReadingFromFile)
                    {
                        var i = 0;
                        foreach (var item in replayedAudioSources)
                        {
                            var latency = clipNumberToLatency[item.Key];
                            DrawPointerOnWaveform(i, item.Value, width, height, latency);
                            i++;
                        }
                    }
                }
            }
            
        }
    }

    List<float> pointersPackSize;
    public void DrawPointerOnWaveform(int i, AudioSource source, int width, int height, int latency)
    {
        var tex = pointersToSamples[i];
        var prev = prevPointerPos[i];
        int latencySamples = source.clip.samples - latency;

        //var packSize = (float)source.clip.samples / width;
        //var packSize = (float)latencySamples / width; // latency considered NOTE no don't do it
        var packSize = replayLength / width;

        int pointerPos = Mathf.RoundToInt((frameTimes[recRep.currentReplayFrame]) / packSize);
        //Debug.Log(pointerPos);
        if (pointerPos != prev)
        {
            //Debug.Log(prev + " " + height + " " + pointerColsClear.Length);
            //Debug.Log(pointerPos + " " + height + " " + pointerCols.Length);

            tex.SetPixels(prev, 0, thickness, height, pointerColsClear);
            if (pointerPos < width-thickness)
            {
                tex.SetPixels(pointerPos, 0, thickness, height, pointerCols);
                prevPointerPos[i] = pointerPos;
            }
            tex.Apply();
        }
    }

    private void Replayer_OnReplayRepeat(object sender, EventArgs e)
    {
        SendAudioMessage(new AudioMessage() { messageId = 4});
        Repeat();
    }
    private void Repeat()
    {
        int i = 0;
        foreach (var item in replayedAudioSources)
        {
            item.Value.mute = mute[i];
            PlayAndConsiderLatency(item.Value, clipNumberToLatency[item.Key]);
            //item.Value.timeSamples = 0; // without considering latency
            i++;
        }
    }

    private void Replayer_OnReplayStopped(object sender, EventArgs e)
    {
        Debug.Log("AudioReplayer: OnReplayStopped");
        //context.SendJson(new Message() { id = 5 });
        SendAudioMessage(new AudioMessage() { messageId = 5});
        ClearReplay();
    }
    private void ClearReplay()
    {
        // (don't do CLIPNUMBER = 0 as it won't reset the sourceCLIPNUMBER)
        SetClipNumber(0); // reset to 0 so a new recording without a replay has the correct CLIPNUMBER 
        pressedPlayFirstTime = false;
        audioDataAvailable = false;
        if (objectidToClipNumberReplay != null)
            objectidToClipNumberReplay.Clear();
        if (speechIndicators != null)
            speechIndicators.Clear();
        if (replayedAudioSources != null)
            replayedAudioSources.Clear();
        if (fromRemoteReplayedClips != null)
            fromRemoteReplayedClips.Clear();
        if (audioClipLengthsReplay != null)
            audioClipLengthsReplay.Clear();
        if (audioClipPositions != null)
            audioClipPositions.Clear();
        if (clipNumberToLatency != null)
            clipNumberToLatency.Clear();
        if (audioFileStream != null)
            audioFileStream.Dispose();
    }

    private void OnDestroy()
    {
        ClearReplay();
    }

    private enum MessageType
    {
        Create, // create the audio clips on all remote peers
        Data, // fill the audio clips on all remote peers with data from the audio file
        Latency, // sets latency (and mute flags) for clips recorded after master
        PlayPause, // set clip to play or pause
        Repeat, // start clip again from beginning + latency
        End, // clear data from last replay
        Sync, // to make sure that audio clips are running somewhat in sync on the remote clients
        Jump // jump to different position in clip based on the current jumped frame
    }
    [Serializable]
    private struct AudioMessage
    {
        public byte messageId; // 1 byte

        public NetworkId objectId;
        public short clipNr;
        public int clipLengthPos;
        public byte[] samples;
        public int[] timeSamples;
        public bool[] muteClips;
        public bool play;
        //public byte[] message; // x bytes
        
        // byteMessage includes 10 byte header for ReferenceCountedMessage
        public byte[] MessageToBytes()
        {
            byte[] byteMessage = null;
            byte[] cn;

            switch (messageId)
            {
                case (int)MessageType.Create:
                    //Debug.Log("objectid " + objectId.ToString());
                    //Debug.Log("clipnr: " + clipNr);
                    //Debug.Log("cliplength: " + clipLengthPos);

                    byteMessage = new byte[10 + 1 + 14]; // NetworkId 8 bytes, clipNr 2 bytes, clipLength 4 bytes
                    //Debug.Log("byteMessage length: " + byteMessage.Length);

                    byteMessage[10] = messageId;
                    byte[] oid = new byte[8]; 
                    objectId.ToBytes(oid, 0);
                    cn = BitConverter.GetBytes(clipNr);
                    byte[] l = BitConverter.GetBytes(clipLengthPos);
                    Array.Copy(oid, 0, byteMessage, 10 + 1, oid.Length);
                    Array.Copy(cn, 0, byteMessage, 10 + 1 + oid.Length, cn.Length);
                    Array.Copy(l, 0, byteMessage, 10 + 1 + oid.Length + cn.Length, l.Length);
                    break;

                case (int)MessageType.Data:
                    byteMessage = new byte[10 + 1 + 6 + samples.Length]; // clipNr 2 bytes, clipPosition 4 bytes, samples samples.Length bytes
                    //Debug.Log("byteMessage length: " + byteMessage.Length);

                    byteMessage[10] = messageId;
                    cn = BitConverter.GetBytes(clipNr);
                    //Debug.Log("clipnr: " + clipNr);
                    //Debug.Log("clipPos: " + clipLengthPos);
                    //Debug.Log("samples length: " + samples.Length);
                    byte[] p = BitConverter.GetBytes(clipLengthPos);
                    Array.Copy(cn, 0, byteMessage, 10 + 1, cn.Length);
                    Array.Copy(p, 0, byteMessage, 10 + 1 + cn.Length, p.Length);
                    Array.Copy(samples, 0, byteMessage, 10 + 1 + cn.Length + p.Length, samples.Length);
                    break;
                case (int)MessageType.Latency:
                case (int)MessageType.PlayPause:
                case (int)MessageType.Sync:
                case (int)MessageType.Jump:
                    if (muteClips == null)
                    {
                        muteClips = new bool[timeSamples.Length];
                    }
                    //Debug.Log("Timesamples length: " + timeSamples.Length);
                    //Debug.Log("muteClips length: " + muteClips.Length);
                    //Debug.Log("play: " + play);
                    byteMessage = new byte[10 + 1 + timeSamples.Length * 4 + muteClips.Length * 1 + 1]; // timeSamples.Length * 4 bytes, muteClips.Length * 1 bytes, bool play 1 byte
                    //Debug.Log("byteMessage length: " + byteMessage.Length);

                    byteMessage[10] = messageId;
                    for (int i = 0; i < timeSamples.Length; i++)
                    {
                        byte[] ts = BitConverter.GetBytes(timeSamples[i]);
                        Array.Copy(ts, 0, byteMessage, 10 + 1 + 4 * i, ts.Length);
                        byte[] mc = BitConverter.GetBytes(muteClips[i]);
                        Array.Copy(mc, 0, byteMessage, 10 + 1 + timeSamples.Length * 4 + i, mc.Length);
                    }
                    byte[] pp = BitConverter.GetBytes(play);
                    Array.Copy(pp, 0, byteMessage, byteMessage.Length - 1, pp.Length);
                    break;
                case (int)MessageType.Repeat:
                case (int)MessageType.End:
                    byteMessage = new byte[10 + 1];
                    byteMessage[10] = messageId;
                    //Debug.Log("byteMessage length: " + byteMessage.Length);
                    //Debug.Log("MessageType: Repeat or End");
                    break;
                default:
                    Debug.Log("Something went wrong");
                    break;
            }
            return byteMessage;
        }

        public void BytesToMessage(byte[] bytes)
        {
            messageId = bytes[10];
            //Debug.Log("message id: " + messageId);
            switch (messageId)
            {
                case (int)MessageType.Create:
                    objectId = new NetworkId(bytes, 10 + 1);
                    clipNr = BitConverter.ToInt16(bytes, 10 + 9); // 1 + 8
                    clipLengthPos = BitConverter.ToInt32(bytes, 10 + 11); // 1 + 8 + 2
                     //Debug.Log("Create " + objectId.ToString() + " " + clipNr + " " + clipLengthPos);
                    break;

                case (int)MessageType.Data:
                    clipNr = BitConverter.ToInt16(bytes, 10 + 1);
                    clipLengthPos = BitConverter.ToInt32(bytes, 10 + 3); // 1 + 2
                    //Debug.Log("Data " + clipNr + " " + clipLengthPos + " " + bytes.Length);
                    samples = new byte[bytes.Length - (10 + 7)];
                    Array.Copy(bytes, 10 + 7, samples, 0, bytes.Length - (10 + 7));

                    break;
                case (int)MessageType.Latency:
                case (int)MessageType.PlayPause:
                case (int)MessageType.Sync:
                case (int)MessageType.Jump:
                    int arrLength = bytes.Length - (10 + 2);
                    int clips = 0;
                    for (int i = 0; i < arrLength; i+=4) // what the hack... to figure out how many clips the arrays have
                    {
                        arrLength -= 1;
                        clips++;
                    }
                    timeSamples = new int[clips];
                    muteClips = new bool[clips];
                    for (int c = 0; c < clips; c++)
                    {
                        timeSamples[c] = BitConverter.ToInt32(bytes, 10 + 1 + 4 * c);
                        muteClips[c] = BitConverter.ToBoolean(bytes, 10 + 1 + 4 * clips + c);
                    }
                    play = BitConverter.ToBoolean(bytes, bytes.Length - 1);
                    //Debug.Log("LPSJ: " + string.Join(", ", timeSamples) + " " + string.Join(", ", muteClips) + " " + play); 
                    break;
                case (int)MessageType.Repeat:
                case (int)MessageType.End:
                    Debug.Log("MessageType: Repeat or End");
                    break;
                default:
                    Debug.Log("Something went wrong");
                    break;
            }
        }

        public AudioMessage(byte[] rcsgm)
        {
            messageId = 0; // 1 byte
            objectId = new NetworkId(0);
            clipNr = 0;
            clipLengthPos = 0;
            samples = null;
            timeSamples = null;
            muteClips = null;
            play = false;
            BytesToMessage(rcsgm);
        }

    }
    private ReferenceCountedSceneGraphMessage CreateRCSGM (byte[] msg)
    {
        //Debug.Log(string.Join(",", msg));
        Ubiq.Networking.ReferenceCountedMessage rcm = new Ubiq.Networking.ReferenceCountedMessage(msg);
        //Debug.Log(rcm.bytes.Length);
        ReferenceCountedSceneGraphMessage rcsgm = new ReferenceCountedSceneGraphMessage(rcm);
        rcsgm.objectid = Id;
        rcsgm.componentid = context.componentId;
        return rcsgm;
    }

    private void SendAudioMessage(AudioMessage audioMessage)
    {
        var rcsgm = CreateRCSGM(audioMessage.MessageToBytes());
        context.Send(rcsgm);
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = new byte[message.length + 10]; // just take header and message
        Buffer.BlockCopy(message.bytes, 0, msg, 0, message.length + 10);
        AudioMessage m = new AudioMessage(msg);
        //AudioMessage m = message.FromJson<AudioMessage>();
        //Debug.Log("AudioRecorderReplayer ProcessMessage id: " + m.id);
        if (m.messageId == (int)MessageType.Create)
        {
            //CreateMessage cm = JsonUtility.FromJson<CreateMessage>(m.messageType);
            CreateRemoteAudioClip(m.objectId, m.clipNr, m.clipLengthPos);
        }
        else if (m.messageId == (int)MessageType.Data)
        {
            Debug.Log("Write data to clip");
            //DataMessage dm = JsonUtility.FromJson<DataMessage>(m.messageType);
            var floatSamples = ConvertToFloat(m.samples);
            fromRemoteReplayedClips[m.clipNr].SetData(floatSamples, m.clipLengthPos);
            //replayedAudioSources[dm.clipNr].clip.SetData(dm.floatSamples, dm.clipPosition); audio source with avatar might not exist yet

        }
        else if (m.messageId == (int)MessageType.Latency)
        {
            //LatencyMessage lm = JsonUtility.FromJson<LatencyMessage>(m.messageType);
            Debug.Log("ProcessMessage latencies and mute: " + m.timeSamples + " " + m.muteClips);
            mute = m.muteClips;
            int i = 0;
            foreach (var item in replayedAudioSources)
            {
                if (item.Value.timeSamples > 0)
                {
                    var newTimeSamples = item.Value.timeSamples - clipNumberToLatency[item.Key] + m.timeSamples[i];
                    item.Value.timeSamples = newTimeSamples;
                }
                clipNumberToLatency[item.Key] = m.timeSamples[i];
                // speech indicators need to know about latency too otherwise there is an error
                speechIndicators[item.Key].SetLatencySamples(m.timeSamples[i]);
                i++;
            }
        }
        else if (m.messageId == (int)MessageType.PlayPause)
        {
            //PlayPauseMessage ppm = JsonUtility.FromJson<PlayPauseMessage>(m.messageType);
            Debug.Log("PlayPauseMessage: " + m.play);
            if (m.play)
            {
                int i = 0;
                foreach (var item in replayedAudioSources)
                {
                    SyncClip(item.Value, m.timeSamples[i]);
                    i++;
                }
            }
            PlayPause(m.play);
        }
        else if (m.messageId == (int)MessageType.Repeat)
        {
            Repeat();
        }
        else if (m.messageId == (int)MessageType.End)
        {
            ClearReplay();
        }
        else if (m.messageId == (int)MessageType.Sync)
        {
            //SyncMessage sm = JsonUtility.FromJson<SyncMessage>(m.messageType);
            // update timeSamples position only if 
            int i = 0;
            foreach (var item in replayedAudioSources)
            {
                SyncClip(item.Value, m.timeSamples[i]);
                item.Value.UnPause();
                i++;
            }
        }
        else if (m.messageId == (int)MessageType.Jump)
        {
            //JumpMessage jm = JsonUtility.FromJson<JumpMessage>(m.messageType);
            int i = 0;
            foreach (var item in replayedAudioSources)
            {
                //Debug.Log("clips " + replayedAudioSources.Count);
                //Debug.Log("jum samples size: " + m.timeSamples.Length);
                //Debug.Log("i " + i);
                Debug.Log("Jump to: " + m.timeSamples[i]);
                item.Value.timeSamples = m.timeSamples[i];
                i++;
            }
        }
    }

    private float[] ConvertToFloat(byte[] samples)
    {
        //Debug.Log("samples: " + string.Join(", ", samples));
        //Debug.Log("samples length: " + samples.Length);
        // convert samples to float
        float[] floatSamples = new float[samples.Length / 2];
        //Debug.Log("floatSamples length: " + floatSamples.Length);
        for (int i = 0; i < samples.Length; i += 2)
        {
            short sample = BitConverter.ToInt16(samples, i);
            //Debug.Log("short sample: " + sample);
            var floatSample = ((float)sample) / short.MaxValue;
            floatSamples[i / 2] = Mathf.Clamp(floatSample * gain, -.999f, .999f);
        }
        return floatSamples;
    }

    private void SyncClip(AudioSource source, int timeSample)
    {
        if (Math.Abs(source.timeSamples - timeSample) >= SAMPLINGFREQ)
        {
            if (source.isPlaying)
            {
                source.Pause();
            }
            Debug.Log("Sync clip " + source.clip.name + " from " + source.timeSamples + " to " + timeSample);
            source.timeSamples = timeSample;            
        }
    }
}

# if UNITY_EDITOR
//[CustomEditor(typeof(AudioRecorderReplayer))]
//public class RecorderReplayerEditor : Editor
//{
//    AudioRecorderReplayer t;
//    SerializedProperty Latencies;
//    SerializedProperty Mute;
//    bool masterOnly;
//    //SerializedProperty MasterOnly;

//    void OnEnable()
//    {
//        t = (AudioRecorderReplayer)target;
//        // Fetch the objects from script to display in the inspector
//        Latencies = serializedObject.FindProperty("latenciesMs");
//        Mute = serializedObject.FindProperty("mute");
//        //MasterOnly = serializedObject.FindProperty("MASTERONLY");
//    }

//    public override void OnInspectorGUI()
//    {
//        // disable GUI when no replay is loaded and while replay is playing (to avoid weird behaviour)
//        EditorGUI.BeginDisabledGroup(!t.recRep.replaying || (t.recRep.replaying && t.recRep.play));

//        //The variables and GameObject from the GameObject script are displayed in the Inspector and have the appropriate label
//        EditorGUILayout.LabelField(new GUIContent("Audio Clips are ordered from newest (most latency) to oldest."));
//        EditorGUILayout.PropertyField(Latencies, new GUIContent("Latency: "));
//        EditorGUILayout.Space();
//        masterOnly = EditorGUILayout.Toggle("Master Only ", masterOnly);
//        EditorGUILayout.PropertyField(Mute, new GUIContent("Mute: "));


//        // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
//        serializedObject.ApplyModifiedProperties();

//        if (GUILayout.Button("Apply changes!"))
//        {
//            if (masterOnly)
//            {
//                t.MuteAllButMasterClip(masterOnly);
//            }
//            t.SetLatencies();
//        }
//        EditorGUI.EndDisabledGroup();

//    }
//}
# endif

