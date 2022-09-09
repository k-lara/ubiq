using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ubiq.XR;
public class JoystickScroll : MonoBehaviour
{

    public ScrollRect scrollRect;
    public HandController controller;
    public float speed = 0.2f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Mathf.Abs(controller.Joystick.y) > 0.3f)
        {
            //Debug.Log(scrollRect.verticalNormalizedPosition + " " + controller.Joystick.y + " " + Time.deltaTime);
            scrollRect.verticalNormalizedPosition += controller.Joystick.y * speed * Time.deltaTime;
            //Debug.Log(scrollRect.verticalNormalizedPosition);
        }
    }
}
