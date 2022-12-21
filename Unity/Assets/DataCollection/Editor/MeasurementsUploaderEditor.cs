using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeasurementsUploader))]
public class MeasurementsUploaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUILayout.Button("Upload"))
        { 
            var component = target as MeasurementsUploader;
            component.Send(Measurements.GenerateSyntheticLogs());
        }
    }
}
