using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PhysicsButton : MonoBehaviour
{
    public enum Buttons
    {
        LeftButton, RightButton, NextButton, BeginButton, EndButton, HeightButton
    }

    [SerializeField] private Buttons buttonName;
    
    private float threshold = 0f;
    private float deadZone = 8f;
    
    private bool isPressed;
    private bool pressedOnce = false;
    private Vector3 startPos;
    private ConfigurableJoint joint;
    private AudioSource audio;

    [System.Serializable]
    public class MyIntEvent : UnityEvent<Buttons>{}

    public MyIntEvent onPressed, onReleased;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.localPosition;
        joint = GetComponent<ConfigurableJoint>();
        audio = GetComponent<AudioSource>();
    }

    public void ResetButtonPress()
    {
        Debug.Log("ResetButtonPress");
        pressedOnce = false;
        isPressed = false;
        transform.localPosition = startPos;
    }

    private void Pressed()
    {
        if (!pressedOnce)
        {
            isPressed = true;
            pressedOnce = true;
            audio.Play();

            onPressed.Invoke(buttonName);
            switch(buttonName)
            {
                case Buttons.LeftButton:
                    Debug.Log("Left Button pressed");
                    break;
                case Buttons.RightButton:
                    Debug.Log("Right Button pressed");
                    break;
                case Buttons.NextButton:
                    Debug.Log("Next Button pressed");
                    break;
                default:
                    Debug.Log("Height Adjust/Begin/End Button pressed");
                    break;
            }
        }
    }

    private void Released()
    {
        isPressed = false;
        onPressed.Invoke(buttonName);

        switch (buttonName)
        {
            case Buttons.LeftButton:
                Debug.Log("Left Button released");
                break;
            case Buttons.RightButton:
                Debug.Log("Right Button released");
                break;
            case Buttons.NextButton:
                Debug.Log("Next Button released");
                break;
            default:
                Debug.Log("Height Adjust/Begin/End Button released");
                break;

        }
    }

    private float GetValue()
    {
        var value = Vector3.Distance(startPos, transform.localPosition) / joint.linearLimit.limit;

        if (Mathf.Abs(value) < deadZone)
            value = 0;

        //Debug.Log(value + " " + Mathf.Clamp(value, -1, 1));
        return Mathf.Clamp(value, -1, 1); 
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPressed && GetValue() + threshold >= 1)
        {
            //Debug.Log(GetValue());
            Pressed();
        }
        if (isPressed && GetValue() + threshold <= 0)
        {
            //Debug.Log(GetValue());
            Released();
        }
    }
}
