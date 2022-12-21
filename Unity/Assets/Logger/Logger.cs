using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum LoggingMode { local, online };

// Functionality:
// This logger has two primary functions:
//  - It logs telemetry in regular intervals. This telemetry is the location and orientation of the headset
//  - It logs events whenever they occur

// How to use it - Telemetry
// To use the telemetry functionality, only the reference for the headset must be set in the unity editor
// (i.e. dragging the gameobject on top of the field labeled "Hmd" and dropping it). The logger starts recording
// automatically and does not needed to be started manually.

// How to use it - Events
// Events are defined by the researcher and are *only* logged when triggered by a script outside of the logger calling
// a log-event-function of it.
// To log an event, an external script could call the LogEvent(...) function defined below. However, for the sake
// of code clarity, it is advised that the researcher implements a dedicated log-event-function and calls that instead.
// Examples of functions like that are given from line 109 to 123.

// Where are the logs stored?
// This depends on which logging mode is set by the user. It has to be set through the drop down menu inside the Unity editor.
// Changing the mode at runtime will lead to errors.

// Where are the log files stored when the logging mode "local" is selected?
// When executed on a Windows machine, the logs are stored under C:\Users\<User name>\AppData\LocalLow\<company name>
// When executed on an Oculus Quest, the logs are stored under Android/data/<packagename>/files


// Where are the log files stored when the logging mode "online" is selected?
// With this configuration, no data is written onto the device. All data records are stored internally.
// The user of the logger may call the Method UploadLogs() at any time to upload it to the server specified by the settings of the MeasurementUploader component.
// Note that uploading the logs will halt the logger.
// To retreive the logs from the server either use the access given to you by one of the members of the VECG group or contact the VECG member that shared this logger with you.

// What the log entries contain - Telemetry
// - Timestamp, Unix time (absolute time, machine readable, timezone independent)
// - Timestamp, time since start in seconds (reltative time)
// - Timestamp, local time (absolute time, human readable, timezone dependent)
// - HMD Location
// - HMD Orientation


// What the log entries contain - Event log
// - Timestamp, Unix time (absolute time, machine readable, timezone independent)
// - Timestamp, time since start in seconds (reltative time)
// - Timestamp, local time (absolute time, human readable, timezone dependent)
// - Event identifier (machine and human readable, intended for easy filtering, should be unique for that event type)
// - Event description (human readable, intended to provide additional information)


public class Logger : MonoBehaviour
{

    private StreamWriter m_eventLogger;
    private StreamWriter m_telemetryLogger;

    private LoggerExperiment m_experiment;

    [Tooltip("The object representing the user's headset")]
    public Transform hmd;

    [Tooltip("The interval for the telemetry log in seconds.")]
    public float logIntervals = 0.5f; // 0.5 is default

    [Tooltip("Determines whether the data is stored into a file at runtime or stored internally and then can be sent off to an online server")]
    public LoggingMode loggingMode = LoggingMode.local;

    // DO NOT USE THIS VARIABLE.
    // Use ParticipantID instead. This way it is ensured that the ID is generated the first time it is requested.
    private string _participantID = null;

    // Participant ID is generated at first access, from inside this component or somewhere else
    public string ParticipantID { get => _participantID != null ? _participantID : _participantID = GenerateParticipantID(); }

    private float m_lastLogTime = 0;

    private bool m_loggerRunning = true;

    public List<EventRecord> GetEventRecords()
    {
        return m_experiment.eventRecords;
    }

    // Use this for initialization
    void Start()
    {


        if (loggingMode == LoggingMode.local)
        {
            // Check if log folder exists and create if not
            Directory.CreateDirectory(Application.persistentDataPath + "/Logs");

            // create writers
            string timeStamp = System.DateTime.Now.ToString("yyyymmdd_hhmmss");
            m_eventLogger = new StreamWriter(Application.persistentDataPath + "/Logs/" + timeStamp + "_event.log");
            m_telemetryLogger = new StreamWriter(Application.persistentDataPath + "/Logs/" + timeStamp + "_telemetry.log");

            // Write participant ID in each of them
            m_telemetryLogger.WriteLine(ParticipantID);
            m_eventLogger.WriteLine(ParticipantID);
            m_telemetryLogger.Flush();
            m_eventLogger.Flush();
        }
        else
        {
            m_experiment = new LoggerExperiment();
            m_experiment.id = ParticipantID;
        }

    }

    // Update is called once per frame
    void Update()
    {
        // log telemetry in regular intervals
        //if (m_lastLogTime + logIntervals < Time.time)
        //{

        //    // Check if the logger is running at the moment. If not, we do nothing
        //    if (m_loggerRunning)
        //    {
        //        switch (loggingMode)
        //        {
        //            case LoggingMode.local:
        //                LogTelemetryLocal();
        //                break;
        //            case LoggingMode.online:
        //                LogTelemetryOnline();
        //                break;
        //        }
        //    }

        //    m_lastLogTime = Time.time;
        //}
    }

    #region duplication check
    // Method to test how likely it is for the ID generator to give us a duplicate
    // DO NOT USE IN PRODUCTION CODE
    private void TestParticipantIDGeneration()
    {
        HashSet<string> keys = new HashSet<string>();

        int iterations = 0;
        bool noDuplicate = true;


        while (noDuplicate)
        {
            iterations++;
            if (iterations % 20 == 0)
            {
                Debug.Log("Current Iteration: " + iterations);
            }

            string newKey = GenerateParticipantID();

            if (keys.Contains(newKey))
            {
                noDuplicate = false;
                Debug.Log("Duplicate found in Iteration: " + iterations);
                Debug.Log("Duplicate key is: " + newKey);
            }
            else
            {
                keys.Add(newKey);
            }
        }
    }
    #endregion

    /// <summary>
    /// Generator for the participant ID
    /// from https://stackoverflow.com/questions/1344221/how-can-i-generate-random-alphanumeric-strings
    /// </summary>
    /// <returns></returns>
    private string GenerateParticipantID()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 8);
    }

    /// <summary>
    /// Log that the logging is paused, then set a flag to halt the logging.
    /// </summary>
    public void HaltLogging()
    {
        LogLoggerHalt();
        m_loggerRunning = false;
    }

    /// <summary>
    /// Set a flag to resume the logging, then log that it is resuming.
    /// </summary>
    public void ResumeLogging()
    {
        m_loggerRunning = true;
        LogLoggerResume();
    }

    /// <summary>
    /// Halts the logger and uploads the logs to the remote server
    /// </summary>
    public void UploadLogs()
    {
        if (loggingMode == LoggingMode.online)
        {
            // get uploader
            MeasurementsUploader uploader = this.GetComponent<MeasurementsUploader>();

            // stop logging
            HaltLogging();

            // Send experiment data
            uploader.Send(m_experiment);
        }
    }

    /// <summary>
    /// Log telemetry internally so it can later be uploaded
    /// This adds one telemetry record to the memory
    /// </summary>
    private void LogTelemetryOnline()
    {
        long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float timeSinceStart = Time.time;
        string timeStamp = System.DateTime.Now.ToString("hhmmss");

        Vector3 hmdPos = hmd.position;

        Vector3 hmdRot = hmd.eulerAngles;

        TelemetryRecord tRecord = new TelemetryRecord();
        tRecord.unixTime = unixTime;
        tRecord.timeSinceStart = timeSinceStart;
        tRecord.timeStamp = timeStamp;

        tRecord.hmdPosition = hmdPos;
        tRecord.hmdRotation = hmdRot;

        m_experiment.telemetryRecords.Add(tRecord);
    }

    /// <summary>
    /// Log telemetry on a local file
    /// This adds one line of telemetry data to the local text file
    /// </summary>
    private void LogTelemetryLocal()
    {
        string unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string timeSinceStart = Time.time.ToString();
        string timeStamp = System.DateTime.Now.ToString("hhmmss");

        Vector3 hmdPos = hmd.position;
        string hmdPosString = hmdPos.ToString();

        Vector3 hmdRot = hmd.eulerAngles;
        string hmdRotString = hmdRot.ToString();

        m_telemetryLogger.WriteLine(unixTime + "," + timeSinceStart + "," + timeStamp + "," + hmdPosString + "," + hmdRotString);
        m_telemetryLogger.Flush();
    }

    /// <summary>
    /// Method to log an event. This is actually just a wrapper that checks if logging is currently halted and redirects
    /// to the appropriate methods for online and offline logging.
    /// </summary>
    /// <param name="eventIdentifier">The two letter identifier used in data anlysis later to easily filter the entries.</param>
    /// <param name="eventDescription">The full description to make entries more meaningful to the human reader later on</param>
    private void LogEvent(string eventIdentifier, string eventDescription)
    {
        // Check if the logger is running at the moment. If not, we do nothing
        if (m_loggerRunning)
        {
            // Decide which function to all depending on whether we want to upload later or write locally
            switch (loggingMode)
            {
                case LoggingMode.local:
                    LogEventLocal(eventIdentifier, eventDescription);
                    break;
                case LoggingMode.online:
                    LogEventOnline(eventIdentifier, eventDescription);
                    break;
            }
        }
    }

    /// <summary>
    /// Log event data internally so it can later be uploaded
    /// This adds one event record to the memory
    /// </summary>
    private void LogEventOnline(string eventIdentifier, string eventDescription)
    {
        long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float timeSinceStart = Time.time;
        string timeStamp = System.DateTime.Now.ToString("hhmmss");

        EventRecord eRecord = new EventRecord();
        eRecord.unixTime = unixTime;
        eRecord.timeSinceStart = timeSinceStart;
        eRecord.timeStamp = timeStamp;

        eRecord.eventIdentifier = eventIdentifier;
        eRecord.eventDescription = eventDescription;

        m_experiment.eventRecords.Add(eRecord);
    }

    /// <summary>
    /// Log event on a local file
    /// This adds one line of event data to the local text file
    /// </summary>
    private void LogEventLocal(string eventIdentifier, string eventDescription)
    {
        string unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string timeSinceStart = Time.time.ToString();
        string timeStamp = System.DateTime.Now.ToString("hhmmss");

        m_eventLogger.WriteLine(unixTime + "," + timeSinceStart + "," + timeStamp + "," + eventIdentifier + "," + eventDescription);
        m_eventLogger.Flush();
    }


    #region Examples for log-event-functions
    public void LogStart()
    {
        string eventDescription = "The Experimenter Started the Experiment";

        LogEvent("ES", eventDescription);
    }

    public void LogEnd()
    {
        string eventDescription = "The Experiment ended";
        LogEvent("EE", eventDescription);
    }

    public void LogContinuation()
    {
        string eventDescription = "The Experimenter Continued the Experiment";
        LogEvent("EC", eventDescription);
    }

    public void LogLoggerHalt()
    {
        string eventDescription = "The Logging was Halted";
        LogEvent("LH", eventDescription);
    }

    public void LogLoggerResume()
    {
        string eventDescription = "The Logging was Resumed";
        LogEvent("LR", eventDescription);
    }

    public void LogVideoPanelSwitch()
    {
        string eventdescription = "";//"INDEX: " + INDEX + ", firstVideo: " + firstVideo;
        LogEvent("Switch: Video", eventdescription);
    }

    public void LogSelectionPanelSwitch()
    {
        string eventdescription = "";
        LogEvent("Switch: Selection", eventdescription);
    }

    public void LogQuestionPanelSwitch(int questionPanelNum)
    {
        string eventdescription = "Number: " + questionPanelNum;
        LogEvent("Switch: Question", eventdescription);
    }

    public void LogQuestionnairePanelSwitch()
    {
        LogEvent("Switch: Questionnaire", "");
    }

    public void LogFinishPanelSwitch()
    {
        string eventdescription = "";
        LogEvent("Switch: Finish", eventdescription);
    }
    #endregion

    void OnApplicationQuit()
    {
        if (loggingMode == LoggingMode.local)
        {
            m_eventLogger.Close();
            m_telemetryLogger.Close();
        }
    }
}
