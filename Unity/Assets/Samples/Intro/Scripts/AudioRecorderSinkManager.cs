using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Voip;
using System.Linq;
using System;

public class AudioRecorderSinkManager
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


    public AudioRecorderSinkManager(AudioRecorderReplayer recRepA, VoipPeerConnection pc)
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
        if (arr.Length > 6) // 4 byte for package length and 2 byte for clip number are always appended before the actual audio message
        {
            var l = BitConverter.GetBytes(arr.Length - 4); // only need length of package not length of package + 4 byte of length
            arr[0] = l[0]; arr[1] = l[1]; arr[2] = l[2]; arr[3] = l[3];
            u = BitConverter.GetBytes(sinkCLIPNUMBER);
            arr[4] = u[0]; arr[5] = u[1];
            recRepA.binaryWriterAudio.Write(arr);

        }
        //Debug.Log("arr length: " + arr.Length + " samplesLength: " + samplesLength);
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
