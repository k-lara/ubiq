using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.XR;

public class Tape : MonoBehaviour, IGraspable
{

    public TMPro.TextMeshProUGUI tapeName;
    public bool attached = false;

    private VRRecorder vrRecorder;
    private Vector3 localGrabPoint;
    private Quaternion localGrabRotation;
    private Transform handTransform; // controller transform of hand that has grabbed the object
    private bool isGrabbed = false;
    private bool inRange = false;

    public string GetTapeName()
    {
        return tapeName.text;
    }
    public void SetTapeName(string name)
    {
        tapeName.text = name;
    }

    public void SetVRRecorder(VRRecorder recorder)
    {
        vrRecorder = recorder;
    }

    public void Grasp(Hand controller)
    {
        if (attached) //attached to the recorder
        {
            // only grab if nothing is recording or playing
            if (vrRecorder.DetachTape(this.gameObject))
            {
                this.gameObject.transform.parent = null;
                handTransform = controller.transform;
                // transforms obj position and rotation from world space into local hand controller space
                localGrabPoint = handTransform.InverseTransformPoint(transform.position);
                localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
                isGrabbed = true;
                attached = false;
            }
        }
        else
        {
            handTransform = controller.transform;
            // transforms obj position and rotation from world space into local hand controller space
            localGrabPoint = handTransform.InverseTransformPoint(transform.position);
            localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
            isGrabbed = true;
        }

    }

    public void Release(Hand controller)
    {
        // snap to recorder if in range and not already snapped
        if (inRange)
        {
            attached = true;
            vrRecorder.AttachTape(this.gameObject);
        }
        handTransform = null;
        isGrabbed = false;   
        transform.parent = vrRecorder.transform; // keeps the tapes with the recorder       
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "TapeAttachment")
        {
            // Debug.Log("in range");
            inRange = true;

            if(vrRecorder == null)
            {
                vrRecorder = other.transform.parent.GetComponentInParent<VRRecorder>();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "TapeAttachment")
        {
            inRange = false;
            // Debug.Log("out of range");
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (handTransform != null)
        {
            transform.position = handTransform.TransformPoint(localGrabPoint);
            transform.rotation = handTransform.rotation * localGrabRotation; 
        }
    }
}
