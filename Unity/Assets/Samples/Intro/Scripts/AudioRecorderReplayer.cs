using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Voip;
using RecorderReplayerTypes;
using System;
using System.Linq;
using Ubiq.Avatars;
using Ubiq.Spawning;
using Ubiq.Samples;


[RequireComponent(typeof(RecorderReplayer))]
public class AudioRecorderReplayer : MonoBehaviour, INetworkComponent
{
    public static int SAMPLINGFREQ = 16000;
    public static int NUMSAMPLES = SAMPLINGFREQ * 2;
    public static bool MASTERONLY = true;
    // need to know about peer UUIDs, and which avatar had which peer UUID so we can alter during replay assign the correct replayed avatars such that the audio sink positions match.
    // manages the audio sinks from different peer connections to keep track of which peer sends what
    private class AudioSinkManager
    {
        public AudioRecorderReplayer recRepA;
        public VoipPeerConnection pc;
        public VoipAudioSourceOutput audioSink;
        //public Transform sinkTransform;
        //public string peerUuid;
        //public short uuid;
        public List<byte[]> audioMessages;
        public int samplesLength = 0;
        public short sinkCLIPNUMBER;

        private int samplesLengthUntilNextWrite = 0;
        private byte[] u; // sink clip number in bytes


        public AudioSinkManager(AudioRecorderReplayer recRepA, VoipPeerConnection pc)
        {
            this.pc = pc;
            audioSink = pc.audioSink;
            this.recRepA = recRepA;
            //sinkTransform = audioSink.transform;
            //peerUuid = pc.PeerUuid;
            //this.uuid = recRepA.peerUuidToShort[peerUuid];

            audioMessages = new List<byte[]>();
            audioMessages.Add(new byte[4]); // length of pack (int)
            audioMessages.Add(new byte[2]); // uuid (short)
            audioSink.OnAudioSourceRawSample += AudioSink_OnAudioSourceRawSample;
            recRepA.recRep.recorder.OnRecordingStopped += Recorder_OnRecordingStopped;
        }

        private void Recorder_OnRecordingStopped(object sender, EventArgs e)
        {
            Cleanup();
        }

        // sets clip number for this sink manager in short and in bytes
        public void SetClipNumber(short CLIPNUMBER)
        {
            sinkCLIPNUMBER = CLIPNUMBER;
            u = BitConverter.GetBytes(sinkCLIPNUMBER);
        }

        public void WriteRemainingAudioData()
        {
            //Debug.Log("Write audio data at frame: " + recRepA.frameNr);
            var arr = audioMessages.SelectMany(a => a).ToArray();
            //Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
            var l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
            arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
            u = BitConverter.GetBytes(sinkCLIPNUMBER);
            arr[4] = u[0]; arr[5] = u[1];
            recRepA.binaryWriterAudio.Write(arr);   
        }

        public void Cleanup()
        {
            audioMessages.Clear();
            samplesLength = 0;
            samplesLengthUntilNextWrite = 0;
        }

        // record audio from peer connections
        private void AudioSink_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            if (recRepA.initAudioFile) // can only be true if recording is true and audio file has been initialised
            {
                samplesLength += sample.Length;
                samplesLengthUntilNextWrite += sample.Length;

                // accumulate samples
                var tempSamples = new byte[sample.Length * sizeof(short)];
                for (var i = 0; i < sample.Length; i++)
                {
                    var tmpSmpl = BitConverter.GetBytes(sample[i]);
                    tempSamples[i * 2] = tmpSmpl[0];
                    tempSamples[i * 2 + 1] = tmpSmpl[1];
                }

                audioMessages.Add(tempSamples);

                // MANAGER!!! NOT MAIN CLASS
                // after x frames, write audio sample pack to file
                if ((recRepA.frameNr % recRepA.frameX) == 0) // maybe do it after x samples? might make it easier to get a regular amount over the network
                //if (samplesLengthUntilNextWrite >= NUMSAMPLES)
                {
                    var arr = audioMessages.SelectMany(a => a).ToArray();
                    var l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
                    arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
                    u = BitConverter.GetBytes(sinkCLIPNUMBER);
                    arr[4] = u[0]; arr[5] = u[1];
                    recRepA.binaryWriterAudio.Write(arr);
                    audioMessages.Clear();
                    audioMessages.Add(new byte[4]); // length of pack (int)
                    audioMessages.Add(u); // clip number (short)
                    samplesLengthUntilNextWrite = 0;
                }
            }
        }
    }
    private void OnPeerConnection(VoipPeerConnection pc)
    {
        Debug.Log("AudioRecorder OnPeerConnection: " + pc.PeerUuid);
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update dictionary with new peer connection
        AudioSinkManager sinkManager = new AudioSinkManager(this, pc); // creates listener for raw audio samples
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
    private Dictionary<string, AudioSinkManager> peerUuidToAudioSinkManager; // do not clear after recording
    private Dictionary<NetworkId, short> objectidToClipNumber; // should be fine for replays of replays, also this is what we save as metadata
    private VoipMicrophoneInput audioSource; // audio from local peer who records 
    private bool initAudioFile = false;
    private BinaryWriter binaryWriterAudio;
    private List<byte[]> audioMessages = null; // collects audio samples from several frames and gathers them in a pack for writing it to file
    private List<int> audioClipLengths = new List<int>();
    private int frameNr = 0;
    private int frameX = 100; // after frameX frames write audio samples to file
    private int samplesLength = 0; // length of all current recorded samples
    private int samplesLengthUntilNextWrite = 0;
    private string testAudioFile = "testAudio"; // test file saving float values to check if data is correct
    //private StreamWriter testStreamWriter = null;
    private List<short[]> testSamples = new List<short[]>();
    private byte[] u = new byte[2]; // clip number

    // audio replay
    private event EventHandler OnLoadAudioDataComplete;
    private NetworkSpawner spawner;
    private Dictionary<NetworkId, short> objectidToClipNumberReplay = null;
    public Dictionary<short, AudioSource> replayedAudioSources = null;
    public Dictionary<short, SpeechIndicator> speechIndicators = null;
    private Dictionary<short, int> clipNumberToLatency = null;
    [SerializeField]
    public int[] latenciesMs;
    [SerializeField]
    public int[] latenciesSamples;
    [SerializeField]
    public bool[] mute;    
    //public int LatencyClip1, LatencyClip2, LatencyClip3, LatencyClip4, LatencyClip5, LatencyClip6, LatencyClip7;
    //public bool MuteClip1, MuteClip2, MuteClip3, MuteClip4, MuteClip5, MuteClip6, MuteClip7;

    private Dictionary<short, int> audioClipPositions = null;
    private Dictionary<short, int> audioClipLengthsReplay = null;
    private FileStream audioFileStream = null;
    private Dictionary<short, int> replayedAudioClipsStartIndices = null; // when recording a replay to know from where to record the replay
    private Dictionary<short, int> replayedAudioClipsRecordedLength = null; // when recording a replay to know until when to record a replay
    private bool pressedPlayFirstTime = false;

    // audio clip creation
    private float gain = 1.0f;

    // replay test file
    private string testAudioFileReplay = "testAudioReplay"; // test file saving float values to check if data is correct
    private StreamWriter testStreamWriterReplay = null;

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
        roomClient = GetComponent<RoomClient>();
        scene = GetComponent<NetworkScene>();
        context = scene.RegisterComponent(this);
        // get voippeerconnectionmanager to get audio source and sinks
        voipConnectionManager = GetComponentInChildren<VoipPeerConnectionManager>();
        avatarManager = GetComponentInChildren<AvatarManager>();
        spawner = GetComponentInChildren<NetworkSpawner>();
        peerUuidToConnection = voipConnectionManager.peerUuidToConnection; // update when peers are added or removed
        peerUuidToAudioSinkManager = new Dictionary<string, AudioSinkManager>(); // update when peers are added or removed
        //peerUuidToShort = new Dictionary<string, short>(); // fill anew for every new recording
        objectidToClipNumber = new Dictionary<NetworkId, short>();
        replayedAudioSources = new Dictionary<short, AudioSource>();
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

        SetClipNumber(CLIPNUMBER++);
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

    public void SetLatencies()
    {
        var i = 0;
        foreach (var item in replayedAudioSources)
        {
            var latency = SetLatency(item.Key, latenciesMs[i]); // latencies are in ms

            // speech indicators need to know about latency too otherwise there is an error
            speechIndicators[item.Key].SetLatencySamples(latency);
            i++;
        }
        var lm = JsonUtility.ToJson(new LatencyMessage() { latencySamples = clipNumberToLatency.Values.ToArray(), mute = mute });
        context.SendJson(new Message() { id = 2, messageType = lm });
        Debug.Log("Latencies: " + string.Join(", ", latenciesMs));
        Debug.Log("Muted: " + string.Join(", ", mute));
    }

    public int SetLatency(short clipNr, int ms)
    {
        var latency = ComputeLatencySamples(ms);
        clipNumberToLatency[clipNr] = latency;
        return latency;
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
            if ((frameNr % frameX) == 0)
            //if (samplesLengthUntilNextWrite >= (16000 * 2) )
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
    // the cool thing is that this should also record when the clip is not playing! oh no no .... this is not the case obviously!
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
                var avatar = item.Value.gameObject.GetComponent<Ubiq.Avatars.Avatar>();
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
        //Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
        var l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
        arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
        arr[4] = u[0]; arr[5] = u[1];
        binaryWriterAudio.Write(arr);
        //testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + sourceCLIPNUMBER + ", " + string.Join(", ", testSamples.SelectMany(a => a).ToArray()) + ",");
        audioMessages.Clear();
        samplesLengthUntilNextWrite = 0;

        foreach (var manager in peerUuidToAudioSinkManager.Values)
        {
            manager.WriteRemainingAudioData();
        }
        if (recRep.replaying) // write new replay to file again
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

    // 
    private void PlayAndConsiderLatency(AudioSource audioSource, int samples)
    {
        audioSource.Play();
        audioSource.timeSamples = samples;
        Debug.Log("Play and consider latency" + samples);
    }

    private void PlayPause(bool play)
    {
        int i = 0;
        if (play)
        {
            //Debug.Log("OnPlay: " + replayedAudioSources.Count);
            foreach (var item in replayedAudioSources)
            {
                item.Value.mute = mute[i];
                if (!pressedPlayFirstTime)
                {
                    PlayAndConsiderLatency(item.Value, clipNumberToLatency[item.Key]);
                }
                else
                {
                    item.Value.UnPause();
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
        var ppm = JsonUtility.ToJson(new PlayPauseMessage() { play = play });
        context.SendJson(new Message() { id = 3, messageType = ppm });

        PlayPause(play);
    }

    // gets called once recording info is loaded in the Replayer and replayed objects are created!
    public async void OnLoadingReplay(RecordingInfo recInfo)
    {
        string filepath = recRep.path + "/audio" + recRep.replayFile + ".dat";
        Debug.Log("Audiorec filepath: " + filepath);
        if (File.Exists(filepath))
        {
            Debug.Log("Get audio file...");

            objectidToClipNumberReplay = recInfo.objectidsToClipNumber.Zip(recInfo.clipNumber, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            audioClipLengthsReplay = recInfo.clipNumber.Zip(recInfo.audioClipLengths, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            latenciesMs = new int[audioClipLengthsReplay.Count];
            mute = new bool[audioClipLengthsReplay.Count];
            clipNumberToLatency.Clear();
            MuteAllButMasterClip(MASTERONLY);
            // increase the CLIPNUMBER to the number of already existing replays so a subsequent recording has the correct clip number
            CLIPNUMBER = (short)audioClipLengthsReplay.Count;

            audioFileStream = File.Open(filepath, FileMode.Open); // open audio byte file for loading audio data into clips
            //foreach (var item in audioClipLengthsReplay)
            //{
            //    Debug.Log("OnLoadingReplay" + item.Key + " " + item.Value);
            //}
     
            foreach (var item in objectidToClipNumberReplay)
            {
                // get new object id and add audio source to respective game object
                var newId = recRep.replayer.oldNewIds[item.Key];
                var clipLength = audioClipLengthsReplay[item.Value];
                // remotely
                var cm = JsonUtility.ToJson(new CreateMessage() { id = newId, clipNr = item.Value, clipLength = clipLength });
                context.SendJson(new Message() { id = 0, messageType = cm });
                //locally
                CreateAudioClip(newId, item.Value, clipLength);

                //audioSource.Play();
                //float[] testClipData = new float[audioClipLengthsReplay[0]];
                //replayedAudioSources[0].clip.GetData(testClipData, 0);
                //File.WriteAllText(recRep.path + "/" + "testClipData" + ".csv", string.Join(", ", testClipData));
            }

            await ReadAudioDataFromFile();
            //OnLoadAudioDataComplete.Invoke(this, EventArgs.Empty);
            
            Debug.Log("AudioClips created!");
            //return true;
        }
        else
        {
            Debug.Log("Invalid audio file path!");
            recRep.replaying = false;
            //return false;
        }
    }
    // creates and audio clip with number clipNr and length clipLength and attaches it to an object with NetworkId id
    private void CreateAudioClip(NetworkId id, short clipNr, int clipLength)
    {        
        var gameObject = spawner.spawned[id];
        var audioSource = gameObject.AddComponent<AudioSource>();
        var speechIndicator = gameObject.GetComponentInChildren<SpeechIndicator>();
        speechIndicator.SetReplayAudioSource(audioSource);
        speechIndicators.Add(clipNr, speechIndicator);
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
    }

    private async Task ReadAudioDataFromFile()
    {
        Debug.Log("AudioReplayer: ReadAudioDataFromFile");
        //testStreamWriterReplay = new StreamWriter(recRep.path + "/" + testAudioFileReplay + ".csv");
        //audioFileStream.Position = 0;
        byte[] pckgLength = new byte[4];
        byte[] clipNumber = new byte[2];
        //int test = 0;
        while (audioFileStream.Position < audioFileStream.Length)
        {
            
            //Debug.Log("stream position " + audioFileStream.Position);
            audioFileStream.Read(pckgLength, 0, 4);
            //Debug.Log("stream position " + audioFileStream.Position);
            audioFileStream.Read(clipNumber, 0, 2);
            //Debug.Log("stream position " + audioFileStream.Position);

            int l = BitConverter.ToInt32(pckgLength, 0) - 2; // pckgLength/2 = length samples
            short s = BitConverter.ToInt16(clipNumber, 0);

            //Debug.Log("sizes: " + l + " " + s);
            byte[] audioPckg = new byte[l]; // contains audio data without bytes for short "uuid"
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
            //Debug.Log("AudioClip positions: " + s + " ");
            var clipPos = audioClipPositions[s];
            Debug.Log("AudioClip positions: " + s + " " + clipPos);
            var dm = JsonUtility.ToJson(new DataMessage() { clipNr = s, clipPosition = clipPos, floatSamples = floatSamples });
            context.SendJson(new Message() { id = 1, messageType = dm });
            replayedAudioSources[s].clip.SetData(floatSamples, clipPos);   
            audioClipPositions[s] += floatSamples.Length; // advance position
                //Debug.Log(s + " " + audioClipPositions[s] + " " + replayedAudioSources[s].clip.samples);
            

        }
        //testStreamWriterReplay.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        if (recRep.recording)
        {
            frameNr++;
        }
    }

    private void Replayer_OnReplayRepeat(object sender, EventArgs e)
    {
        context.SendJson(new Message() { id = 4 });
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
        context.SendJson(new Message() { id = 5 });
        ClearReplay();
    }
    private void ClearReplay()
    {
        pressedPlayFirstTime = false;
        if (objectidToClipNumberReplay != null)
            objectidToClipNumberReplay.Clear();
        if (speechIndicators != null)
            speechIndicators.Clear();
        if (replayedAudioSources != null)
            replayedAudioSources.Clear();
        if (audioClipLengthsReplay != null)
            audioClipLengthsReplay.Clear();
        if (audioClipPositions != null)
            audioClipPositions.Clear();
        if (clipNumberToLatency != null)
            clipNumberToLatency.Clear();
        if (audioFileStream != null)
            audioFileStream.Dispose();
    }

    private enum MessageType
    {
        Create, // create the audio clips on all remote peers
        Data, // fill the audio clips on all remote peers with data from the audio file
        Latency, // sets latency (and mute flags) for clips recorded after master
        PlayPause, // set clip to play or pause
        Repeat, // start clip again from beginning + latency
        End // clear data from last replay
    }
    [Serializable]
    public struct CreateMessage
    {
        public NetworkId id;
        public short clipNr;
        public int clipLength;
    }
    public struct DataMessage
    {
        public short clipNr;
        public float[] floatSamples;
        public int clipPosition;
    }
    public struct LatencyMessage
    {
        public int[] latencySamples;
        public bool[] mute;
    }
    public struct PlayPauseMessage
    {
        public bool play;
    }

    public struct Message
    {
        public int id;
        public string messageType;
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        Message m = message.FromJson<Message>();
        if (m.id == (int)MessageType.Create)
        {
            CreateMessage cm = JsonUtility.FromJson<CreateMessage>(m.messageType);
            CreateAudioClip(cm.id, cm.clipNr, cm.clipLength);
        }
        else if (m.id == (int)MessageType.Data)
        {
           DataMessage dm = JsonUtility.FromJson<DataMessage>(m.messageType);
           replayedAudioSources[dm.clipNr].clip.SetData(dm.floatSamples, dm.clipPosition);

        }
        else if (m.id == (int)MessageType.Latency)
        {
            LatencyMessage lm = JsonUtility.FromJson<LatencyMessage>(m.messageType);
            mute = lm.mute;
            int i = 0;
            foreach (var item in replayedAudioSources)
            {
                clipNumberToLatency[item.Key] = lm.latencySamples[i];
                // speech indicators need to know about latency too otherwise there is an error
                speechIndicators[item.Key].SetLatencySamples(lm.latencySamples[i]);
                i++;
            }
        }
        else if (m.id == (int)MessageType.PlayPause)
        {
            PlayPauseMessage ppm = JsonUtility.FromJson<PlayPauseMessage>(m.messageType);
            PlayPause(ppm.play);
        }
        else if (m.id == (int)MessageType.Repeat)
        {
            Repeat();
        }    
        else if (m.id == (int)MessageType.End)
        {
            ClearReplay();
        }
    }
}

# if UNITY_EDITOR
[CustomEditor(typeof(AudioRecorderReplayer))]
public class RecorderReplayerEditor : Editor
{
    AudioRecorderReplayer t;
    SerializedProperty Latencies;
    SerializedProperty Mute;
    bool masterOnly;
    //SerializedProperty MasterOnly;

    void OnEnable()
    {
        t = (AudioRecorderReplayer)target;
        // Fetch the objects from script to display in the inspector
        Latencies = serializedObject.FindProperty("latencies");
        Mute = serializedObject.FindProperty("mute");
        //MasterOnly = serializedObject.FindProperty("MASTERONLY");
    }

    public override void OnInspectorGUI()
    {
        // disable GUI when no replay is loaded and while replay is playing (to avoid weird behaviour)
        EditorGUI.BeginDisabledGroup(!t.recRep.replaying || (t.recRep.replaying && t.recRep.play));

        //The variables and GameObject from the GameObject script are displayed in the Inspector and have the appropriate label
        EditorGUILayout.LabelField(new GUIContent("Audio Clips are ordered from newest (most latency) to oldest."));
        EditorGUILayout.PropertyField(Latencies, new GUIContent("Latency: "));
        EditorGUILayout.Space();
        masterOnly = EditorGUILayout.Toggle("Master Only ", masterOnly);
        EditorGUILayout.PropertyField(Mute, new GUIContent("Mute: "));


        // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();

        if(GUILayout.Button("Apply changes!"))
        {
            if (masterOnly)
            {
                t.MuteAllButMasterClip(masterOnly);
            }
            t.SetLatencies();
        }
        EditorGUI.EndDisabledGroup();

    }
}
# endif

