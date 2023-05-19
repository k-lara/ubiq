using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRButton : MonoBehaviour
{
    public float deadTime = 1.0f;
    private bool deadTimeActive = false;

    private AudioSource buttonSound;

    public event System.EventHandler OnPress, OnRelease;

    void Start()
    {
        buttonSound = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
       if (other.tag == "VRButton" && !deadTimeActive)
       {
            buttonSound.Play();
            OnPress.Invoke(this, System.EventArgs.Empty);
            Debug.Log("VR Button pressed!"); 
       }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "VRButton" && !deadTimeActive)
        {
            // OnRelease.Invoke(this, System.EventArgs.Empty); // not needed for now
            Debug.Log("VR Button released!");
            StartCoroutine(WaitForDeadTime());
        }
    }
    
    private IEnumerator WaitForDeadTime()
    {
        deadTimeActive = true;
        yield return new WaitForSeconds(deadTime);
        deadTimeActive = false;
    }
}
