using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Spawning;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Messaging;
using System.IO;
using System;
using RecorderReplayerTypes;
using UnityEngine.UI;
public class ReplayedAvatarRemover : MonoBehaviour
{
    // WOrkflow:
    // Load a replay
    // Play (to see the avatars)
    // Pause 
    //---------------------------
    // Move to the avatar that should be deleted and touch them (grab them)
    // Delete the avatar or let it fade and then delete
    // Show an info text with: "Delete avatar and create new recording..."
    // Show an info text when new recording is finished.
    // Unload the current replay
    //---------------------------
    // proceed with new recording as you would with any other

    public RecorderReplayer recRep;
    public RecorderReplayerMenu recRepMenu;
    public Material transparentMat;
    public Text infoText;
    public GameObject heartPrefab;

    private List<GameObject> avatarGOs; // list of avatars in the replay to be modified

    private BinaryWriter binaryWriter;
    private BinaryWriter audioBinaryWriter;
    private FileStream replayStream;
    private FileStream audioFileStream;

    private bool avatarSelected = false;
    private int currentFrame = 0;
    private RecordingInfo recInfo;
    private List<int> newPckgSizePerFrame;
    private List<long> newIdxFrameStart;
    private NetworkId oldAvatarId; // the id of the avatar from the recording file
    private short clipNr; // the clipNr of the avatar to be removed
    private bool writeMotion = false;
    private bool writeAudio = false;
    private int iterations = 5;
    //private string newRecordFile = "";

    public void Cleanup()
    {
        if (replayStream != null) replayStream.Dispose();
        if (binaryWriter != null) binaryWriter.Dispose();
        if (audioBinaryWriter != null) audioBinaryWriter.Dispose();
        if (audioFileStream != null) audioFileStream.Dispose();

        recInfo = null;
        avatarGOs.Clear();
        avatarSelected = false;
        currentFrame = 0;
        writeMotion = false;
        writeAudio = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        avatarGOs = new List<GameObject>();
    }

    // TODOs:
    // equip avatar with bounding box for grasping
    // remove deleted avatar from rec info data;
    // write audio data and movement data from remaining avatars (and objects) to a new file

    public void AddAvatar(GameObject avatarGO, Heart heart)
    {
        // when replaying is started again, 
        Debug.Log("Add avatar: " + avatarGO.GetComponent<Avatar>().Id);
        avatarGOs.Add(avatarGO);
        heart.EndLifeEvent += ReplayedAvatarRemover_EndLifeEvent;
    }

    private void ReplayedAvatarRemover_EndLifeEvent(object sender, System.EventArgs e)
    {
        if (recRep.replaying && !recRep.play && !avatarSelected) // only do this when we are in replay
        {
            Avatar avatar = sender as Avatar;

            if (avatarGOs.Count <= 1)
            {
                Debug.Log("Only one avatar in the scene");

                infoText.text = "Cannot delete avatar!";
                StartCoroutine(recRep.marker.FadeTextToZeroAlpha(2.0f, infoText));
                return;
            }

            Debug.Log(avatar.Id);
            foreach (var go in avatarGOs)
            {
                if (go.GetComponent<Avatar>().Id == avatar.Id)
                {
                    //make current avatar disappear
                    StartCoroutine(FadeOutAvatar(go, 2.0f));
                    break;
                }
            }
            //Debug.Log("Fade Avatar: " + avatar.Id);
            // user has to stop the replay because it does not makes sense to delete one avatar from a one-avatar recording

            // update recinfo and remove anything related to the avatar
            recInfo = recRep.replayer.recInfo;
            // find out what this avatar's old id in the file was
            NetworkId oldId = new NetworkId();
            oldAvatarId = oldId;
            foreach (var item in recRep.replayer.oldNewIds)
            {
                if (item.Value.Equals(avatar.Id))
                {
                    oldId = item.Key;
                    //Debug.Log("old id was: " + oldId.ToString());
                    break;
                }

            }
            // some things changes like number of avatars, and clipNrs and pckgSize,
            // frames and frameTimes stay the same 
            newPckgSizePerFrame = new List<int>();
            newIdxFrameStart = new List<long>();
            newIdxFrameStart.Add(0);

            var indexOfClipNr = recInfo.objectidsToClipNumber.IndexOf(oldId);
            if (indexOfClipNr >= 0)
            {
                recInfo.objectidsToClipNumber.Remove(oldId); // remove objectid
                // clip numbers are saved in descending order
                // adapt remaining clip numbers accordingly
                var clipNr = recInfo.clipNumber[indexOfClipNr];
                recInfo.clipNumber.Remove(clipNr);
                for (int i = 0; i < recInfo.clipNumber.Count; i++)
                {
                    if (recInfo.clipNumber[i] > clipNr)
                    {
                        recInfo.clipNumber[i] = (short)(recInfo.clipNumber[i] - 1);
                    }
                }
                recInfo.audioClipLengths.RemoveAt(indexOfClipNr);
                recInfo.numberOfObjects--;
                var index = recInfo.objectids.IndexOf(oldId); // in case this is not the same order, because there could be other objects too that don't have audio
                recInfo.objectids.Remove(oldId);
                recInfo.textures.RemoveAt(index);
                recInfo.prefabs.RemoveAt(index);

                for ( int i = 0; i < recInfo.markerLists.Count; i++)
                {
                    if (recInfo.markerLists[i].id.Equals(oldId))
                    {
                        recInfo.markerLists.RemoveAt(i);
                        // can't break here in case there are more lists (e.g. gips and button press markers)
                    }
                }


                // disable play button for the time being
                recRepMenu.PlayPauseButtonInteractable(false);

                string newRecordFile = recRep.path + "/" + recRep.replayFile + "_mod" + (avatarGOs.Count - 1) + ".dat";
                string newAudioFile = recRep.path + "/audio" + recRep.replayFile + "_mod" + (avatarGOs.Count - 1) + ".dat";

                replayStream = recRep.replayer.GetReplayStream();
                audioFileStream = recRep.audioRecRep.GetAudioFileStream();
                audioFileStream.Position = 0;
                binaryWriter = new BinaryWriter(File.Open(newRecordFile, FileMode.OpenOrCreate));
                audioBinaryWriter = new BinaryWriter(File.Open(newAudioFile, FileMode.OpenOrCreate));
                writeMotion = true;
                writeAudio = true;
                avatarSelected = true;

                infoText.color = new Color(infoText.color.r, infoText.color.g, infoText.color.b, 1);
                infoText.text = "Deleting avatar...";
            }

        }
    }

    public IEnumerator FadeOutAvatar(GameObject avatar, float t)
    {
        avatar.TryGetComponent(out Ubiq.Samples.FloatingAvatar fa);
        Texture tex = fa.headRenderer.material.mainTexture;
        transparentMat.mainTexture = tex;
        fa.headRenderer.material = transparentMat;
        fa.torsoRenderer.material = transparentMat;
        fa.leftHandRenderer.material = transparentMat;
        fa.rightHandRenderer.material = transparentMat;

        transparentMat.color = new Color(transparentMat.color.r, transparentMat.color.g, transparentMat.color.b, 1);
        while (transparentMat.color.a > 0.0f)
        {
            transparentMat.color = new Color(transparentMat.color.r, transparentMat.color.g, transparentMat.color.b, transparentMat.color.a - (Time.deltaTime / t));
            yield return null;
        }
    }

    public void OnDestroy()
    {
        Cleanup();
    }
    // some lists need to be updated:
    // pckgSizePerFrame
    // idxFrameStart
    public void ReplayWithoutAvatar() 
    {
        // all the messages from one frame
        // i could do this the same as with the audio... call filestream.Read()
        MessagePack messages = new MessagePack();
        var pckgSize = recInfo.pckgSizePerFrame[currentFrame];
        // use file stream from original replay
        replayStream.Position = recInfo.idxFrameStart[currentFrame];
        byte[] msgPack = new byte[pckgSize];

        var numberBytes = replayStream.Read(msgPack, 0, pckgSize);

        int i = 4; // first 4 bytes are length of package
        while (i < numberBytes)
        {
            int lengthMsg = BitConverter.ToInt32(msgPack, i);
            i += 4;
            byte[] msg = new byte[lengthMsg];
            Buffer.BlockCopy(msgPack, i, msg, 0, lengthMsg);

            // first 8 byte are the object id
            byte[] byteId = new byte[8];
            Buffer.BlockCopy(msg, 0, byteId, 0, 8);
            var id = new NetworkId(byteId, 0);

            if (!id.Equals(oldAvatarId))
            {
                // take message and add it to new recording
                byte[] msgWithLength = new byte[lengthMsg + 4];
                Buffer.BlockCopy(msgPack, i - 4, msgWithLength, 0, lengthMsg + 4);
                messages.AddMessage(msgWithLength);
            }

            i += lengthMsg;
        }

        byte[] bMessages = messages.GetBytes();
        binaryWriter.Write(bMessages);
        newPckgSizePerFrame.Add(bMessages.Length);
        newIdxFrameStart.Add(newIdxFrameStart[newIdxFrameStart.Count - 1] + bMessages.Length);
    }

    int test = 0;
    public void AudioDataWithoutAvatar(int iterations)
    {
        byte[] pckgLength = new byte[4];
        byte[] clipNumber = new byte[2];
        byte[] audioPckg;
        for (int i = 0; i < iterations; i++)
        {
            if (audioFileStream.Position < audioFileStream.Length)
            {
                audioFileStream.Read(pckgLength, 0, 4);
                int l = BitConverter.ToInt32(pckgLength, 0);

                audioPckg = new byte[l]; // contains audio data without bytes for short "uuid"
                audioFileStream.Read(audioPckg, 0, audioPckg.Length);

                short clipNr = BitConverter.ToInt16(audioPckg, 0); // first 2 bytes are clipNr

                //Debug.Log(clipNr);
                if (clipNr != this.clipNr)
                {
                    byte[] msgWithLength = new byte[audioPckg.Length + 4];
                    Buffer.BlockCopy(pckgLength, 0 , msgWithLength, 0, pckgLength.Length);

                    if (clipNr > this.clipNr) // decrease clipNr by 1
                    {
                        clipNr--;
                        //Debug.Log("new bytes");
                        byte[] newBytes = BitConverter.GetBytes(clipNr);
                        audioPckg[0] = newBytes[0]; audioPckg[1] = newBytes[1];
                    }
                    Buffer.BlockCopy(audioPckg, 0, msgWithLength, pckgLength.Length, audioPckg.Length);
                    //Debug.Log("checked original" + l + " " + clipNr + " " + pckgLength.Length + " " + audioPckg.Length);
                    //audioBinaryWriter.Write(pckgLength);
                    //audioBinaryWriter.Write(audioPckg);
                    audioBinaryWriter.Write(msgWithLength);
                }
                //Debug.Log(audioFileStream.Position + " " + audioFileStream.Length);
            }

        }
        
    }

    public void EndReplay()
    {
        infoText.text = "New recording without avatar saved!";
        StartCoroutine(recRep.marker.FadeTextToZeroAlpha(2.0f, infoText));
        recRepMenu.ToggleReplay();
        Cleanup();

    }

    // Update is called once per frame
    void Update()
    {
        if (writeMotion)
        {
            for (int i = 0; i < iterations; i++)
            {
                ReplayWithoutAvatar(); // writes package at specific frame time to new binaryWriter

                currentFrame++;
                i++;

                if (currentFrame == recInfo.frames)
                {
                    writeMotion = false;
                    recInfo.pckgSizePerFrame = newPckgSizePerFrame;
                    recInfo.idxFrameStart = newIdxFrameStart;
                    // done writing data;
                    string newIdFile = recRep.path + "/IDs" + recRep.replayFile + "_mod" + (avatarGOs.Count - 1) + ".txt";
                    File.WriteAllText(newIdFile, JsonUtility.ToJson(recInfo, true));
                    recRepMenu.AddNewRecording(recRep.replayFile + "_mod" + (avatarGOs.Count - 1));
                    recRepMenu.AddReplayFiles(true);
                    newPckgSizePerFrame.Clear();
                    newIdxFrameStart.Clear();

                    Debug.Log("Finished motion recording (without avatar)!");

                    if (!writeAudio) // audio already done too
                    {
                        EndReplay();
                    }
                    break;
                }
            }
        }
        if (writeAudio)
        {
            for (int i = 0; i < iterations/2; i++)
            {
                AudioDataWithoutAvatar(3);
            }

            if (audioFileStream.Position >= audioFileStream.Length)
            {
                Debug.Log("Finished writing audio data (without avatar)!");
                writeAudio = false;
                Debug.Log("Audio length new file: " + test);
                test = 0;

                if (!writeMotion) // motion already done too
                {
                    EndReplay();
                }
            }
        }
    }
}
