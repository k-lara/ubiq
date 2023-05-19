using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Ubiq.XR;
using Ubiq.Avatars;

public class VRSlider : MonoBehaviour, IGraspable
{
    public float minX; // min position of the slider
    public float maxX; // max position of the slider

    public Vector3 currentPos;
    public Transform grabPoint; // point where the hand grabs the slider
    public event EventHandler OnGrasp;
    public event EventHandler OnRelease;
    public event EventHandler<float> OnSliderChange;

    private Transform handTransform;
    private bool isGrasped = false;

    private AudioSource grabHandleSound;
    private AudioSource releaseHandleSound;



    
    // Start is called before the first frame update
    void Start()
    {
        currentPos = gameObject.transform.localPosition;
        currentPos.x = minX;
        gameObject.transform.localPosition = currentPos;
        // Debug.Log(currentPos.x);

        var audioSources = GetComponents<AudioSource>();
        grabHandleSound = audioSources[0];
        releaseHandleSound = audioSources[1];
    }

    // Update is called once per frame
    void Update()
    {
        // check where x-position of hand controller is
        if (isGrasped)
        { 
            currentPos.x = gameObject.transform.parent.InverseTransformPoint(handTransform.position).x; // get x-position of hand controller in local space
            gameObject.transform.localPosition = currentPos; // move slider to hand controller
       
            if (currentPos.x <= minX)
            {
                currentPos.x =  minX;
                gameObject.transform.localPosition = currentPos; 
            }
            if (currentPos.x >= maxX)
            {
                currentPos.x = maxX;
                gameObject.transform.localPosition = currentPos;
            }
            OnSliderChange.Invoke(this, GetNormalizedSliderValue());
        }
    }

    public void SetSliderFromNormalizedValue(float value)
    {
        currentPos.x = minX + value * (maxX - minX);
        gameObject.transform.localPosition = currentPos;
    }

    public float GetNormalizedSliderValue()
    {
        return (float)Math.Round((currentPos.x - minX) / (maxX - minX), 2);
    }

    public bool IsGrasped()
    {
        return isGrasped;
    }

    public void OnCollisionStay(Collision collision)
    {
        // change color of handle to show interactability
    }

    public void Grasp(Hand controller)
    {
        Debug.Log("Grasp Slider");
        grabHandleSound.Play();
        isGrasped = true;
        handTransform = controller.transform;
        controller.gameObject.GetComponentInChildren<AvatarHintPositionRotation>().otherTransform = grabPoint;
        OnGrasp.Invoke(this, EventArgs.Empty);
    }

    public void Release(Hand controller)
    {
        Debug.Log("Release Slider");
        releaseHandleSound.Play();
        isGrasped = false;
        handTransform = null;
        controller.gameObject.GetComponentInChildren<AvatarHintPositionRotation>().otherTransform = null;
        OnRelease.Invoke(this, EventArgs.Empty);
    }
}
