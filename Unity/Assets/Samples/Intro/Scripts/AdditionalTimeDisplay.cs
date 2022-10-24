using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdditionalTimeDisplay : MonoBehaviour
{
    // copies values from menu canvas to an additional time display
    public Text otherTimeRecord; 
    public Text otherTimeReplay;

    public Text thisTimeRecord;
    public Text thisTimeReplay;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        thisTimeRecord.text = otherTimeRecord.text;
        thisTimeReplay.text = otherTimeReplay.text;
    }
}
