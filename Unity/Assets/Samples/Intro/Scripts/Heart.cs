using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.XR;
using System;
using Avatar = Ubiq.Avatars.Avatar;

public class Heart : MonoBehaviour, IGraspable
{
    // Start is called before the first frame update
    public Material transparent;
    public Material normal;
    public Renderer renderer;

    private ParticleSystem particles;
    private Rigidbody body;
    private Avatar avatar;

    public event EventHandler EndLifeEvent;

    private Vector3 localGrabPoint;
    private Quaternion localGrabRotation;
    private Transform handTransform; // controller transform of hand that has grabbed the object
    private bool isGrabbed = false; // starts this way as the heart spawns in the hand
    private bool firstGrab = true;
    private bool toBeDestroyed = false;

    // User Inputs
    private float degreesPerSecond = 10.0f;
    private float amplitude = 0.08f;
    private float frequency = 1f;

    private IEnumerator coroutine;

    // Position Storage Variables
    Vector3 posOffset = new Vector3();
    Vector3 tempPos = new Vector3();

    public void RenderInvisible()
    {
        renderer.material = transparent;
        renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 0);
        renderer.enabled = false;
    }

    public void SetAvatar(Avatar avatar)
    {
        this.avatar = avatar;
    }

    public void Grasp(Hand controller)
    {
        if (!toBeDestroyed)
        {

            if (firstGrab)
            {
                Debug.Log("Invoke Life End Event");
                transform.SetParent(null);
                EndLifeEvent.Invoke(avatar, EventArgs.Empty);
            }
            firstGrab = false;
        
            Debug.Log("Heart grasped");
            if (!particles.isStopped)
            {
                Debug.Log("Stop particles");
                particles.Stop();
                StopCoroutine(coroutine);
            }
            handTransform = controller.transform;
            renderer.enabled = true;
            renderer.material = normal;
            renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 1);
            // transforms obj position and rotation from world space into local hand controller space
       
            localGrabPoint = handTransform.InverseTransformPoint(transform.position);
            localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
            isGrabbed = true;
            body.isKinematic = true;
        }
    }

    public void Release(Hand controller)
    {
        
        Debug.Log("heart released");
        if (isGrabbed)
        {
            handTransform = null;
            posOffset = transform.position;
            body.isKinematic = false;
            isGrabbed = false;
            if (!particles.isPlaying)
            {
                Debug.Log("Play particles");
                particles.Play();
            }
            coroutine = FadeHeartAndDestroy(5.0f);
            StartCoroutine(coroutine);
        }
    }

    public IEnumerator FadeHeartAndDestroy(float t)
    {
        toBeDestroyed = true;
        Debug.Log("Fade heart...");
        renderer.material = transparent;

        renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 1);
        while (renderer.material.color.a > 0.0f && !isGrabbed)
        {
            renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, renderer.material.color.a - (Time.deltaTime / t));
            
            yield return null;
        }
        Debug.Log("Heart destroyed");
        Destroy(gameObject);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        particles = GetComponent<ParticleSystem>();
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (handTransform != null) // object has been grabbed, isKinematic = true
        {
            //Debug.Log("hand transform");
            transform.position = handTransform.TransformPoint(localGrabPoint);
            transform.rotation = handTransform.rotation * localGrabRotation;
        }
        else
        {
            if (!firstGrab)
            {
                // Spin object around Y-Axis
                transform.Rotate(new Vector3(0f, Time.deltaTime * degreesPerSecond, 0f), Space.World);

                // Float up/down with a Sin()
                tempPos = posOffset;
                tempPos.y += Mathf.Sin(Time.fixedTime * Mathf.PI * frequency) * amplitude;

                transform.position = tempPos;
            }
        }
    }
}
