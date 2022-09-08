using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.XR;
using Ubiq.Avatars;
using Avatar = Ubiq.Avatars.Avatar;

// Recordable Interactable Object: An object that can be interacted with over multiple replays
// exemplary PROCESS:
// - record initial position/rotation of object when transform does not change do not record it
// - record when obj is grabbed by an avatar
// - what happens when object is released during recording?
// - physics kicks in to let it drop correctly, however, this is not recorded! 
// - only record the position before the object is released
// - HANDING OVER during recording replay:
// - recorded avatar and live avatar need to grasp object at the same time
// - when recorded avatar releases the object the live avatar holds on and takes over
// - 
public class RIObject : MonoBehaviour, IGraspable, INetworkObject, INetworkComponent
{
    public NetworkContext context;
    public NetworkScene scene;

    private Vector3 prevPosition;
    private Quaternion prevRotation;

    // grabbing
    private Vector3 localGrabPoint;
    private Quaternion localGrabRotation;
    private Transform handTransform; // controller transform of hand that has grabbed the object
    private bool isGrabbed; // grabbed by one avatar
    private bool isGrabbed2; // grabbed by the other avatar
    private bool startingPoseSet = false;

    private Rigidbody rb;

    private Avatar avatar;
    public AvatarManager avatarManager;

    private RecorderReplayer recRep;

    public NetworkId Id { get; } = new NetworkId("abc27356-d81e8f59");

    public struct Message
    {
        public TransformMessage transform;
        public bool isGrabbed2;
        public bool isKinematic;
        public Message(Transform transform, bool isGrabbed2, bool isKinematic)
        {
            this.transform = new TransformMessage(transform);
            this.isGrabbed2 = isGrabbed2;
            this.isKinematic = isKinematic;
        }
    }


    public void Grasp(Hand controller)
    {
        //if (!isGrabbed)
        {
            Debug.Log("Live: Let's grab the object");
            handTransform = controller.transform;
            // transforms obj position and rotation from world space into local hand controller space
            localGrabPoint = handTransform.InverseTransformPoint(transform.position);
            localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
            isGrabbed = true;
            isGrabbed2 = false;
            rb.isKinematic = true;
        }
    }

    public void RecordedGrasp(Transform handTransform)
    {
        Debug.Log("RecordedGrasp");
        this.handTransform = handTransform;
        Debug.Log("Position: " + this.handTransform.position.ToString());
        localGrabPoint = handTransform.InverseTransformPoint(transform.position);
        localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
        isGrabbed = true;
        isGrabbed2 = false;
        rb.isKinematic = true;
    }
    public void ForceRelease()
    {
        Debug.Log("force release");
        handTransform = null;
        rb.isKinematic = false;
        isGrabbed = false;
        isGrabbed2 = false;
        context.SendJson(new Message(transform, isGrabbed2, rb.isKinematic));

    }

    public void Release(Hand controller)
    {
        if (isGrabbed)
        {
            Debug.Log("release");
            handTransform = null;
            rb.isKinematic = false;
            isGrabbed = false;
            context.SendJson(new Message(transform, isGrabbed, rb.isKinematic));

        }

    }

    // Start is called before the first frame update
    void Start()
    {
        if (context == null) context = NetworkScene.Register(this);
        scene = NetworkScene.FindNetworkScene(this);
        //avatar = avatarManager.LocalAvatar;
        recRep = scene.GetComponentInChildren<RecorderReplayer>();
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.5f;
        rb.mass = 0.5f;
        rb.isKinematic = false; 
    }

    // not to self: need the position of the object because the replayed avatar
    // does not have a hand controller transform
    // Update is called once per frame
    void Update()
    {
        if (handTransform != null) // object has been grabbed, isKinematic = true
        {
            //Debug.Log("hand transform");
            transform.position = handTransform.TransformPoint(localGrabPoint);
            transform.rotation = handTransform.rotation * localGrabRotation;           
        }

        //if (handAnim != null) // some recorded avatar is grabbing the object for the at least second time
        //{

        //    Debug.Log("Hand anim doing stuff");
        //    (var left, var right) = handAnim.GetGripTargets();

        //    if (right < 0.8 || left < 0.8)
        //    {
        //        ForceRelease();
        //        handAnim = null;
        //        collider = null;
        //    }
        //    else
        //    {
        //        Debug.Log("HandAnim grasp: " + left + " " + right);
        //        if (right > 0.8 || left > 0.8)
        //        {
        //            // "grasp" the object
        //            // if held by live user: release
        //            if (!isGrabbed && !isGrabbed2)
        //            {
        //                ForceRelease();
        //                RecordedGrasp(collider.transform);
        //            }
        //        }
        //    }
        //}
        if (scene.recorder != null && scene.recorder.IsRecording())
        {
            // record/send initial position of GO once and any other grabbed transforms when they change
            // handles: initial pos/rot of object and while grabbed
            if (transform.position != prevPosition || transform.rotation != prevRotation)
            {
                //Debug.Log("transform changed");
                if (!startingPoseSet) // do this once
                {
                    context.SendJson(new Message(transform, isGrabbed, rb.isKinematic));
                    prevPosition = transform.position;
                    prevRotation = transform.rotation;
                    startingPoseSet = true;
                }
                //Debug.Log("is grabbed");
                if (isGrabbed || isGrabbed2) 
                {
                    context.SendJson(new Message(transform, true, rb.isKinematic));
                    prevPosition = transform.position;
                    prevRotation = transform.rotation;
                }
                // if not kinematic it won't be recorded
            }

        }
        else
        {
            //if (!recRep.replaying)
            {
                startingPoseSet = false;
                prevPosition = Vector3.zero;
                // just reset position, don't worry about rotation
                //avatars.Clear();
                //colliders.Clear();
                //handAnim = null;
            }
        }
        
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        // if it is already grabbed before an incoming message that might set isGrabbed2 = false
        // then isGrabbed stays true and needs to be set false some other way!!!
        if (!isGrabbed) // if it is not grabbed by current live avatar to avoid flickering between positions
        {
            //Debug.Log("Process Message RIO");
            var msg = message.FromJson<Message>();
            transform.localPosition = msg.transform.position;
            transform.localRotation = msg.transform.rotation;
            isGrabbed2 = msg.isGrabbed2;
            rb.isKinematic = msg.isKinematic;
        }
        else
        {
            isGrabbed2 = false;
        }
    }

    private RIOInteractable rioI;
    private void OnTriggerStay(Collider other)
    {
        //Debug.Log(isGrabbed + " " + isGrabbed2);
        if (isGrabbed2)
            return;

        if (other.TryGetComponent(out rioI)) // not ideal, but ok for now
        {
            //Debug.Log("got interactable");
            (var left, var right) = rioI.handAnimation.GetGripTargets();

            if (right > 0.8 || left > 0.8)
            {
                //Debug.Log("wants to grab if nobody else does");
                if (!isGrabbed) // real user is not grabbing
                {
                    Debug.Log("Recorded avatar grab");
                    RecordedGrasp(other.transform);
                    // then it is grabbed but by the recorded avatar
                }
            }
            else
            {
                //Debug.Log("grip button < 0.8: " + right);
                //if (handTransform != null) 
                //    Debug.Log(handTransform.position.ToString() + " " + other.transform.position.ToString());
                
                if (handTransform != null && other.transform.position.Equals(handTransform.position)) // recorded avatar is holding the object
                {
                    Debug.Log("OnTriggerStay: Force release");
                    ForceRelease();
                }
            }

        }
    }

    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other.gameObject.tag == "TriggerRIO")
    //    {
    //        if (other.transform.parent.parent.parent.parent.TryGetComponent(out handAnim))
    //        {
    //            Debug.Log("got hand anim");

    //            if (!handAnim.avatar.IsLocal)
    //            {
    //                collider = other;
    //            }
    //        }
    //    }

    //}
}
