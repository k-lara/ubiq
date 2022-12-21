using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Record
{
    public Vector3 position;
}

public class Experiment
{
    public string id;
    public List<Record> records = new List<Record>();
}

public class Measurements : MonoBehaviour
{
    public MeasurementsUploader uploader;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Upload()
    {
        uploader.Send(GenerateSyntheticLogs());
    }

    public static Experiment GenerateSyntheticLogs()
    {
        var data = new Experiment();
        data.id = "hello world";
        data.records.Add(new Record());
        return data;
    }
}
