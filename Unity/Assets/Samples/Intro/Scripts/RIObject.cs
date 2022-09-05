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

    private HandAnimation handAnim; // hand animation of recorded avatar that might be carrying the object
    private Collider collider;
    private List<Ubiq.Avatars.Avatar> avatars;
    private List<Collider> colliders;
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
        if (!isGrabbed)
        {
            Debug.Log("Let's grab the object");
            handTransform = controller.transform;
            // transforms obj position and rotation from world space into local hand controller space
            localGrabPoint = handTransform.InverseTransformPoint(transform.position);
            localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
            isGrabbed = true;
            rb.isKinematic = true;
        }
    }

    public void RecordedGrasp(Transform handTransform)
    {
        Debug.Log("RecordedGrasp");
        this.handTransform = handTransform;
        localGrabPoint = handTransform.InverseTransformPoint(transform.position);
        localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
        isGrabbed = true;
        rb.isKinematic = true;
    }
    public void ForceRelease()
    {
        Debug.Log("force release");
        handTransform = null;
        rb.isKinematic = false;
        isGrabbed = false;
        context.SendJson(new Message(transform, isGrabbed, rb.isKinematic));

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
        avatar = avatarManager.LocalAvatar;
        recRep = scene.GetComponentInChildren<RecorderReplayer>();
        rb = GetComponent<Rigidbody>();
        avatars = new List<Ubiq.Avatars.Avatar>();
        colliders = new List<Collider>();
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
                if (isGrabbed) 
                {
                    context.SendJson(new Message(transform, isGrabbed, rb.isKinematic));
                    prevPosition = transform.position;
                    prevRotation = transform.rotation;
                }
                // if not kinematic it won't be recorded
            }

        }
        else
        {
            if (!recRep.replaying)
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
        if (!isGrabbed) // if it is not grabbed by current live avatar to avoid flickering between positions
        {
            //Debug.Log("Process Message RIO");
            var msg = message.FromJson<Message>();
            transform.localPosition = msg.transform.position;
            transform.localRotation = msg.transform.rotation;
            isGrabbed2 = msg.isGrabbed2;
            rb.isKinematic = msg.isKinematic;
        }
    }

    
    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out handAnim))
        {
            (var left, var right) = handAnim.GetGripTargets();

            if (right > 0.8 || left > 0.8)
            {
                if (!isGrabbed) // real user is not grabbing
                {
                    Debug.Log("recorded avatar grab");
                    RecordedGrasp(other.transform);
                    // then it is grabbed but by the recorded avatar
                }
            }
            else
            {
                if (other.transform.position.Equals(handTransform.position)) // recorded avatar is holding the object
                {
                    Debug.Log("Force release");
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
