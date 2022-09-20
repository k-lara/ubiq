using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// Need all the recordings (and postprocessed audio files)
/// load audio separately this time as it has to be processed
/// Shuffle them in random order, save which one is which
/// Save responses of users in an extra file
/// save times when they clicked on a button (decided which avatar to take) in responses file
/// record user while they are doing what they are doing
/// 
/// flip recordings so users don't notice that they have seen things twice
/// do different voice overs for the same recordings?
/// </summary>
///
public class Experiment : MonoBehaviour
{
    public RecorderReplayer recRep;
    
    private string pathToRecordings;
    private List<Recording> recordings; 

    public class Recording
    {
        public string infoFileName; // .txt
        public string motionFileName; // .dat
        public string audioFileName; // .wav
        
    }

    public void LoadReplay(int replayNr)
    {
        
        recRep.menuRecRep.SelectReplayFile(recordings[replayNr].infoFileName);
        recRep.menuRecRep.ToggleReplay(); // audio won't be loaded as it is no .dat file
    
    }

    public void GetRecordingsFromDir(string pathToRecordings)
    {
        recordings = new List<Recording>();
        try
        {
            var dir = new DirectoryInfo(recRep.path); // path to recordings
            if (dir.Exists)
            {
                foreach (var file in dir.EnumerateFiles("r*"))
                {
                    
                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                    Debug.Log(file.Name);
                    recordings.Add(new Recording()
                    {
                        infoFileName = fileName + ".txt",
                        motionFileName = fileName + ".dat",
                        audioFileName = fileName + ".wav"
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
