using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Ubiq.Messaging;

namespace RecorderReplayerTypes {
    
    
    public class AudioMessagePack
    {
        public byte[] uuid;
        public List<byte[]> samples = new List<byte[]>();
        
        // for debugging
        public string sUuid;
        public string sSamples = "";
        public int length = 0;

        public AudioMessagePack(short uuid) 
        {
            sUuid = uuid.ToString();
            this.uuid = BitConverter.GetBytes(uuid); 
        }

        public void Add(short[] samples) 
        {
            sSamples += string.Join(", ", samples) + ", "; // for debugging
            length += samples.Length;

            byte[] bSamples = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(samples[i]), 0, bSamples, i * 2, 2);
            }
            this.samples.Add(bSamples); 
        }

        public void Clear() { samples.Clear(); sSamples = ""; length = 0;  }

        // convert gathered samples into package [length [4 byte], uuid [2 byte], samples [2 * samples.length bytes]]
        public byte[] GetBytes()
        {
            byte[] samplesArr = samples.SelectMany(a => a).ToArray();
            byte[] byteMsg = new byte[ samplesArr.Length + 2 + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(samplesArr.Length + 2), 0, byteMsg, 0, 4); // length of audio message pack (int)
            Buffer.BlockCopy(uuid, 0, byteMsg, 4, 2); // uuid (short)
            Buffer.BlockCopy(samplesArr, 0, byteMsg, 6, samplesArr.Length); // samples
            return byteMsg;
        }

        // for debugging purposes to check if recorded data makes sense
        public override string ToString()
        {           
            // length without byte length for uuid
            return length + ", " + sUuid + ", " + sSamples;
        }

    }
    
    /// <summary>
    /// A MessagePack contains all the messages (SingleMessages) that are recorded in one frame.
    /// </summary>
    public class MessagePack
    {
        public List<byte[]> messages;

        public void AddMessage(byte[] message)
        {
            messages.Add(message);
        }
        public MessagePack()
        {
            messages = new List<byte[]>();
            messages.Add(new byte[4]); // save space for size?
        }

        public MessagePack(byte[] messagePack) // 4 byte at beginning for size
        {
            messages = new List<byte[]>();
            messages.Add(new byte[] { messagePack[0], messagePack[1], messagePack[2], messagePack[3] });

            int i = 4;
            while (i < messagePack.Length) // error here!!!
            {
                int lengthMsg = BitConverter.ToInt32(messagePack, i);
                i += 4;
                byte[] msg = new byte[lengthMsg];
                Buffer.BlockCopy(messagePack, i, msg, 0, lengthMsg);
                messages.Add(msg);
                i += lengthMsg;
            }
        }

        public byte[] GetBytes()
        {
            byte[] toBytes = messages.SelectMany(a => a).ToArray();
            byte[] l = BitConverter.GetBytes(toBytes.Length - 4); // only need length of package not length of package + 4 byte of length
            toBytes[0] = l[0]; toBytes[1] = l[1]; toBytes[2] = l[2]; toBytes[3] = l[3];
            //int t = BitConverter.ToInt32(messages[0], 0);
            //if (BitConverter.IsLittleEndian)
            //    Array.Reverse(messages[0]);
            //t = BitConverter.ToInt32(messages[0], 0);
            return toBytes;

        }
    }
    // Kind of obsolete when I think about it... it just encapsulates the ReferenceCountedSceneGraphMessage
    public class SingleMessage
    {
        public byte[] message; // whole message including object and component ids
        public SingleMessage(byte[] message)
        {
            this.message = message;
        }
        public byte[] GetBytes()
        {
            byte[] bLength = BitConverter.GetBytes(message.Length);
            //if (BitConverter.IsLittleEndian)
            //    Array.Reverse(bLength);
            byte[] toBytes = new byte[bLength.Length + message.Length];
            Buffer.BlockCopy(bLength, 0, toBytes, 0, bLength.Length);
            Buffer.BlockCopy(message, 0, toBytes, bLength.Length, message.Length);
            return toBytes;
        }
    }

    public class ReplayedObjectProperties
    {
        public GameObject gameObject;
        public ObjectHider hider; // only avatars currently have this
        public NetworkId id;
        public Dictionary<int, INetworkComponent> components = new Dictionary<int, INetworkComponent>();

    }

    [System.Serializable]
    public class RecordingInfo
    {
        public int[] listLengths; // frameTimes.Count, pckSizePerFrame.Count, idxFrameStart.Count
        public int frames;
        public List<NetworkId> objectidsToClipNumber;
        public List<short> clipNumber;
        public List<int> audioClipLengths;
        public int numberOfObjects;
        public List<NetworkId> objectids; // objectids from prefabs (unnecessary?)
        public List<string> textures; // textures
        public List<string> prefabs; // prefabs
        public List<Marker.AvatarMarkers> markerLists; // two consecutive values indicate the begin and end of a significant occurence
        public List<float> frameTimes;
        public List<int> pckgSizePerFrame;
        public List<long> idxFrameStart; // long! index could get extremely high

        public RecordingInfo(int frames, List<NetworkId> objectidsToClipNumber, List<short> clipNumber, List<int> audioClipLengths, int numberOfObjects, 
            List<NetworkId> objectids, List<string> textures, List<string> prefabs,
            List<Marker.AvatarMarkers> markerLists,
            List<float> frameTimes, List<int> pckgSizePerFrame, List<long> idxFrameStart)
        {
            listLengths = new int[3] { frameTimes.Count, pckgSizePerFrame.Count, idxFrameStart.Count };
            this.frames = frames;
            this.objectidsToClipNumber = objectidsToClipNumber;
            this.clipNumber = clipNumber;
            this.audioClipLengths = audioClipLengths;
            this.numberOfObjects = numberOfObjects;
            this.objectids = objectids;
            this.textures = textures;
            this.prefabs = prefabs;
            this.markerLists = markerLists;
            this.frameTimes = frameTimes;
            this.pckgSizePerFrame = pckgSizePerFrame;
            this.idxFrameStart = idxFrameStart;
        }
    }
}