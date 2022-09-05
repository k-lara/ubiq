using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Ubiq.Avatars.Avatar;

public class AudioIndicator : MonoBehaviour
{
    public RawImage waveformTex;
    public RawImage pointerTex;
    public Canvas canvas;
    public Camera mainCamera;
    public Avatar avatar;

    // Start is called before the first frame update
    void Start()
    {
        // find player camera
        mainCamera = Camera.main;
        if (avatar.IsLocal)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!avatar.IsLocal)
        {
            transform.LookAt(mainCamera.transform);
        }
    }
}
