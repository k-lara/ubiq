using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Voip;
using RecorderReplayerTypes;
using System;
using System.Linq;
using Ubiq.Avatars;
using Ubiq.Spawning;


[RequireComponent(typeof(RecorderReplayer))]
public class AudioRecorderReplayer : MonoBehaviour, INetworkComponent
{

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

        private byte[] tempSamples;
        private byte[] tmpSmpl;
        private byte[] arr;
        private byte[] l;
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
        }
        // sets clip number for this sink manager in short and in bytes
        public void SetClipNumber(short CLIPNUMBER)
        {
            sinkCLIPNUMBER = CLIPNUMBER;
            u = BitConverter.GetBytes(sinkCLIPNUMBER);
        }

        public void Cleanup()
        {
            Debug.Log("Write audio data at frame: " + recRepA.frameNr);
            arr = audioMessages.SelectMany(a => a).ToArray();
            Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
            l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
            arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
            u = BitConverter.GetBytes(sinkCLIPNUMBER);
            arr[4] = u[1]; arr[5] = u[2];
            recRepA.binaryWriterAudio.Write(arr);
            audioMessages.Clear();
            samplesLength = 0;
        }

        // record audio from peer connections
        private void AudioSink_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            if (recRepA.initAudioFile) // can only be true if recording is true and audio file has been initialised
            {
                samplesLength += sample.Length;

                // accumulate samples
                tempSamples = new byte[sample.Length * sizeof(short)];
                for (var i = 0; i < sample.Length; i++)
                {
                    tmpSmpl = BitConverter.GetBytes(sample[i]);
                    tempSamples[i * 2] = tmpSmpl[0];
                    tempSamples[i * 2 + 1] = tmpSmpl[1];
                }

                audioMessages.Add(tempSamples);

                // MANAGER!!! NOT MAIN CLASS
                // after x frames, write audio sample pack to file
                if ((recRepA.frameNr % recRepA.frameX) == 0) // maybe do it after x samples? might make it easier to get a regular amount over the network
                {
                    arr = audioMessages.SelectMany(a => a).ToArray();
                    l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
                    arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
                    u = BitConverter.GetBytes(sinkCLIPNUMBER);
                    arr[4] = u[1]; arr[5] = u[2];
                    recRepA.binaryWriterAudio.Write(arr);
                    audioMessages.Clear();
                    audioMessages.Add(new byte[4]); // length of pack (int)
                    audioMessages.Add(u); // clip number (short)
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
    //private Dictionary<string, short> peerUuidToShort; // ???
    private Dictionary<NetworkId, short> objectidToClipNumber; // should be fine for replays of replays, also this is what we save as metadata

    private VoipMicrophoneInput audioSource; // audio from local peer who records 
    private bool initAudioFile = false;
    private BinaryWriter binaryWriterAudio;
    private List<byte[]> audioMessages = null; // collects audio samples from several frames and gathers them in a pack for writing it to file
    private List<int> audioClipLengths = new List<int>();
    private int frameNr = 0;
    private int frameX = 100; // after frameX frames write audio samples to file
    private int samplesLength = 0; // length of current recorded samples

    private string testAudioFile = "testAudio"; // test file saving float values to check if data is correct
    private StreamWriter testStreamWriter = null;
    private List<short[]> testSamples = new List<short[]>();
    
    private byte[] tempSamples;
    private byte[] tmpSmpl = new byte[2];
    private byte[] arr;
    private byte[] l = new byte[4]; // length of sample pack
    private byte[] u = new byte[2]; // clip number
    // audio replay
    private NetworkSpawner spawner;
    private Dictionary<NetworkId, short> objectidToClipNumberReplay = null;
    private Dictionary<short, AudioSource> replayedAudioSources = null;
    private Dictionary<short, int> audioClipPositions = null;
    private Dictionary<short, int> audioClipLengthsReplay = null;
    private FileStream audioFileStream = null;
    
    // audio clip creation
    byte[] pckgLength = new byte[4];
    byte[] uuidToShort = new byte[2];
    private float gain = 1.0f;

    // replay test file
    private string testAudioFileReplay = "testAudioReplay"; // test file saving float values to check if data is correct
    private StreamWriter testStreamWriterReplay = null;
    private List<short[]> testSamplesReplay = new List<short[]>();

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
        audioClipPositions = new Dictionary<short, int>();
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

    // recording
    private void AudioSource_OnAudioSourceRawSample(SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
    {
        if (recRep.recording)
        {
            // only init it on audio source, as audio source should send all the time anyways
            if (!initAudioFile)
            {
                Debug.Log("Init audio file");
                //var dateTime = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"); // not good, sometimes dateTime is one hundredth too late
                recRep.audioRecordFile = recRep.path + "/audiorec" + recRep.recordingStartTimeString + ".dat";

                binaryWriterAudio = new BinaryWriter(File.Open(recRep.audioRecordFile, FileMode.OpenOrCreate)); // dispose when recording is finished
                testStreamWriter = new StreamWriter(recRep.path + "/" + testAudioFile + ".csv");

                initAudioFile = true;
            }
            // do the audio recording
            //Debug.Log("sample length: " + sample.Length);
            samplesLength += sample.Length;
            // test
            testSamples.Add(sample);
            //testStreamWriter.WriteLine(string.Join(", ", sample) + ",");

            // accumulate samples
            tempSamples = new byte[sample.Length * sizeof(short)];
            for (var i = 0; i < sample.Length; i++)
            {
                tmpSmpl = BitConverter.GetBytes(sample[i]);
                tempSamples[i * 2] = tmpSmpl[0];
                tempSamples[i * 2 + 1] = tmpSmpl[1];
            }

            audioMessages.Add(tempSamples);

            // after x frames, write audio sample pack to file
            if ((frameNr % frameX) == 0)
            {
                Debug.Log("Write audio data at frame: " + frameNr);
                arr = audioMessages.SelectMany(a => a).ToArray();
                Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
                byte[] l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
                arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
                arr[4] = u[1]; arr[5] = u[2];
                binaryWriterAudio.Write(arr);
                testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + sourceCLIPNUMBER + ", " + string.Join(", ", testSamples.SelectMany(a => a).ToArray()) + ",");
                audioMessages.Clear();
                testSamples.Clear();
                audioMessages.Add(new byte[4]); // length of pack (int)
                audioMessages.Add(u); // clip number (short)
            }
        }
    }

    // event is invoked after audio recording info (objectidsToShort) is saved
    private void Recorder_OnRecordingStopped(object sender, EventArgs args)
    {
        Debug.Log("AudioRecorder OnRecordingStopped");

        Debug.Log("Write audio data at frame: " + frameNr);
        arr = audioMessages.SelectMany(a => a).ToArray();
        Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
        l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
        arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
        arr[4] = u[1]; arr[5] = u[2];
        binaryWriterAudio.Write(arr);
        testStreamWriter.WriteLine(BitConverter.ToInt32(l, 0) + ", " + sourceCLIPNUMBER + ", " + string.Join(", ", testSamples.SelectMany(a => a).ToArray()) + ",");
        audioMessages.Clear();

        foreach (var manager in peerUuidToAudioSinkManager.Values)
        {
            manager.Cleanup();
        }

        testSamples.Clear();
        audioClipLengths.Clear();
        //peerUuidToShort.Clear(); do not clear as it has also the uuid of the local peer which needs to remain for the whole session
        objectidToClipNumber.Clear();
        binaryWriterAudio.Dispose();
        testStreamWriter.Dispose(); // dispose at the end as SinkManagers also need to save rest data to file
        initAudioFile = false;
        frameNr = 0;
        samplesLength = 0;
    }

    private void Replayer_OnReplayRepeat(object sender, EventArgs e)
    {
        foreach(var source in replayedAudioSources.Values)
        {
            source.Play();
        }
    }

    private void Replayer_OnReplayStopped(object sender, EventArgs e)
    {
        Debug.Log("AudioReplayer: OnReplayStopped");
        if (objectidToClipNumberReplay != null)
            objectidToClipNumberReplay.Clear();
        if (replayedAudioSources != null)
            replayedAudioSources.Clear();
        if (audioClipLengthsReplay != null)
            audioClipLengthsReplay.Clear();
        if (audioClipPositions != null)
            audioClipPositions.Clear();
        if (audioFileStream != null)
            audioFileStream.Dispose();
    }

    // gets called once recording info is loaded in the Replayer and replayed objects are created!
    public bool OnLoadingReplay(RecordingInfo recInfo)
    {
        string filepath = recRep.path + "/audio" + recRep.replayFile + ".dat";
        Debug.Log("Audiorec filepath: " + filepath);
        if (File.Exists(filepath))
        {
            Debug.Log("Get audio file...");

            objectidToClipNumberReplay = recInfo.objectidsToShort.Zip(recInfo.toShort, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            audioClipLengthsReplay = recInfo.toShort.Zip(recInfo.audioClipLengths, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            audioFileStream = File.Open(filepath, FileMode.Open); // open audio byte file for loading audio data into clips
            foreach(var item in objectidToClipNumberReplay)
            {
                Debug.Log("short value: " + item.Value);
                // get new object id and add audio source to respective game object
                var id = recRep.replayer.oldNewIds[item.Key];
                var audioSource = spawner.spawned[id].AddComponent<AudioSource>();
                audioSource.clip = AudioClip.Create(
                name: "AudioClip " + item.Value + " id: " + id.ToString(),
                lengthSamples: audioClipLengthsReplay[item.Value],
                channels: 1,
                frequency: 16000,
                stream: false);
                audioSource.ignoreListenerPause = false;
                audioSource.spatialBlend = 1.0f;
                //audioSource.Play();
                Debug.Log(audioSource.clip.name + " length: " + audioClipLengthsReplay[item.Value]);
                replayedAudioSources.Add(item.Value, audioSource);
                audioClipPositions.Add(item.Value, 0);

                ReadAudioDataFromFile();
                audioSource.Play();
                float[] testClipData = new float[audioClipLengthsReplay[0]];
                replayedAudioSources[0].clip.GetData(testClipData, 0);
                File.WriteAllText(recRep.path + "/" + "testClipData" + ".csv", string.Join(", ", testClipData));

            }
            Debug.Log("AudioClips created!");
            return true;
        }
        else
        {
            Debug.Log("Invalid audio file path!");
            recRep.replaying = false;
            return false;
        }
    }

    private void ReadAudioDataFromFile()
    {
        Debug.Log("AudioReplayer: ReadAudioDataFromFile");
        testStreamWriterReplay = new StreamWriter(recRep.path + "/" + testAudioFileReplay + ".csv");
        //audioFileStream.Position = 0;
        while (audioFileStream.Position < audioFileStream.Length)
        {
            //Debug.Log("stream position " + audioFileStream.Position);
            audioFileStream.Read(pckgLength, 0, 4);
            //Debug.Log("stream position " + audioFileStream.Position);
            audioFileStream.Read(uuidToShort, 0, 2);
            //Debug.Log("stream position " + audioFileStream.Position);

            int l = BitConverter.ToInt32(pckgLength, 0) - 2; // pckgLength/2 = length samples
            short s = BitConverter.ToInt16(uuidToShort, 0);
        
            //Debug.Log("sizes: " + l + " " + s);
            byte[] audioPckg = new byte[l]; // contains audio data without bytes for short "uuid"
            audioFileStream.Read(audioPckg, 0, audioPckg.Length);
            //Debug.Log("stream position " + audioFileStream.Position);

            // convert samples to float
            float[] floatSamples = new float[audioPckg.Length / 2];
            for (int i = 0; i < audioPckg.Length; i+=2)
            {
                short sample = BitConverter.ToInt16(audioPckg, i);
                testStreamWriterReplay.Write(sample + ",");

                var floatSample = ((float)sample) / short.MaxValue;
                floatSamples[i/2] = Mathf.Clamp(floatSample * gain, -.999f, .999f);
            }
            // set audio data in audio clip
            //Debug.Log("AudioClip positions: " + audioClipPositions[s]);
            replayedAudioSources[s].clip.SetData(floatSamples, audioClipPositions[s]);
            audioClipPositions[s] += floatSamples.Length; // advance position

        }
        testStreamWriterReplay.Dispose();
    }

    public (Dictionary<NetworkId, short>, List<int>) GetAudioRecInfoData() 
    { 
        foreach (var avatar in avatarManager.Avatars)
        {
            objectidToClipNumber.Add(avatar.Id, peerUuidToAudioSinkManager[avatar.Peer.UUID].sinkCLIPNUMBER);
            if (avatar.Peer.UUID == roomClient.Me.UUID)
                audioClipLengths.Add(samplesLength);
            else
                audioClipLengths.Add(peerUuidToAudioSinkManager[avatar.Peer.UUID].samplesLength);
        }
        return (objectidToClipNumber, audioClipLengths); 
    }

    

    // Update is called once per frame
    void Update()
    {
        if (recRep.recording)
        {
            frameNr++;
        }
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        throw new NotImplementedException();
    }
}
