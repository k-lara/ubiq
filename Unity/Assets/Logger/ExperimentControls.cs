using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExperimentControls : MonoBehaviour
{
    public Logger logger;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            logger.LogStart();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            logger.LogContinuation();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            logger.LogEnd();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            logger.UploadLogs();
        }
    }
}
