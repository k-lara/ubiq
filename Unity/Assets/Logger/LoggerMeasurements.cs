// Originally written by Sebastian Friston
// Adapted for the use with the logger by Felix Thiel


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TelemetryRecord
{
    // Time data
    public long unixTime;
    public float timeSinceStart;
    public string timeStamp;

    // Payload
    public Vector3 hmdPosition;
    public Vector3 hmdRotation;

    // Add additional things to log as telemetry here
}

[Serializable]
public struct EventRecord
{
    // Time data
    public long unixTime;
    public float timeSinceStart;
    public string timeStamp;

    // Payload
    public string eventIdentifier;
    public string eventDescription;

    // Add additional things to log as event here
}

public class LoggerExperiment
{
    public string id; // Participant ID

    public List<TelemetryRecord> telemetryRecords = new List<TelemetryRecord>();
    public List<EventRecord> eventRecords = new List<EventRecord>();
}

public class LoggerMeasurements : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static LoggerExperiment GenerateSyntheticLogs()
    {
        var data = new LoggerExperiment();
        data.id = "hello world";
        data.telemetryRecords.Add(new TelemetryRecord());
        data.eventRecords.Add(new EventRecord());
        return data;
    }
}
