using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Avatar = Ubiq.Avatars.Avatar;

public class AudioIndicator : MonoBehaviour
{
    public RawImage waveformTex;
    public RawImage pointerTex;
    public RawImage markerTex;
    public Canvas canvas;
    public GameObject canvasGO;
    public Camera mainCamera;
    public Avatar avatar;

    void Awake()
    {
        // find player camera
        mainCamera = Camera.main;
        canvas.gameObject.SetActive(false); // gets enabled when audio clips are created
        //if (avatar.IsLocal)
        //{
        //}
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
