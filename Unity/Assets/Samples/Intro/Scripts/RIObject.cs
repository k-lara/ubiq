using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.XR;
using Ubiq.Avatars;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Rooms;

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

    private RoomClient roomClient;
    // grabbing
    private Vector3 localGrabPoint;
    private Quaternion localGrabRotation;
    private Transform handTransform; // controller transform of hand that has grabbed the object
    private bool isGrabbed; // grabbed by one avatar
    private bool isGrabbed2; // grabbed by the other avatar
    private NetworkId currentGrabberId =  new NetworkId(0); // id of the avatar that is currently grabbing the object
    private NetworkId zeroId = new NetworkId(0);
    private bool startingPoseSet = false;

    private Rigidbody rb;

    public AvatarManager avatarManager;

    private RecorderReplayer recRep;


    public NetworkId Id { get; } = new NetworkId("abc27356-d81e8f59");

    public struct Message
    {
        public TransformMessage transform;
        public bool isGrabbed2;
        public bool isKinematic;
        public NetworkId grabberId;

        public Message(Transform transform, bool isGrabbed2, bool isKinematic, NetworkId grabberId)
        {
            this.transform = new TransformMessage(transform);
            this.isGrabbed2 = isGrabbed2;
            this.isKinematic = isKinematic;
            this.grabberId = grabberId;
        }
    }


    public void Grasp(Hand controller)
    {
        Debug.Log("Live: Let's grab the object");
        handTransform = controller.transform;
        // transforms obj position and rotation from world space into local hand controller space
        localGrabPoint = handTransform.InverseTransformPoint(transform.position);
        localGrabRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
        isGrabbed = true;
        //isGrabbed2 = false;
        rb.isKinematic = true;
        if (recRep.experiment.mode == ReplayMode.MultiUser)
        {
            currentGrabberId = avatarManager.LocalAvatar.Id;
            context.SendJson(new Message(transform, isGrabbed, rb.isKinematic, currentGrabberId));
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
    // for single user 
    public void ForceRelease()
    {
        Debug.Log("force release");
        handTransform = null;
        rb.isKinematic = false;
        isGrabbed = false;
        isGrabbed2 = false;
        context.SendJson(new Message(transform, isGrabbed2, rb.isKinematic, zeroId));

    }

    public void Release(Hand controller)
    {
        if (isGrabbed) // for handing it between left and right hand (&& handTransform.Equals(controller.transform))
        {
            Debug.Log("release");
            handTransform = null;
            rb.isKinematic = false;
            isGrabbed = false;
            context.SendJson(new Message(transform, isGrabbed, rb.isKinematic, avatarManager.LocalAvatar.Id));
            currentGrabberId = zeroId;

        }

    }

    // Start is called before the first frame update
    void Start()
    {
        if (context == null) context = NetworkScene.Register(this);
        scene = NetworkScene.FindNetworkScene(this);
        roomClient = scene.GetComponent<RoomClient>();
        recRep = scene.GetComponentInChildren<RecorderReplayer>();
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.5f;
        rb.mass = 0.5f;
        rb.isKinematic = false;

        //prevPosition = transform.position;
        //prevRotation = transform.rotation;
    }

    // note to self: need the position of the object because the replayed avatar
    // does not have a hand controller transform
    // Update is called once per frame
    void Update()
    {
        // isGrabbed = True follow whoever grabbed the object
        if (handTransform != null) // object has been grabbed, isKinematic = true
        {
            //Debug.Log("hand transform");
            transform.position = handTransform.TransformPoint(localGrabPoint);
            transform.rotation = handTransform.rotation * localGrabRotation;           
            
        }
        if (recRep.experiment.mode == ReplayMode.SingleUser)
        {
            if (recRep.recording) // this is only true for the user who is the creator and able to record
            //if (scene.recorder != null && scene.recorder.IsRecording()) // this can be true for everyone
            {
                // record/send initial position of GO once and any other grabbed transforms when they change
                // handles: initial pos/rot of object and while grabbed
                if (transform.position != prevPosition || transform.rotation != prevRotation)
                {
                    //Debug.Log("transform changed");
                    if (!startingPoseSet) // do this once
                    {
                        Debug.Log("Set starting pos!");
                        context.SendJson(new Message(transform, isGrabbed, rb.isKinematic, zeroId));
                        prevPosition = transform.position;
                        prevRotation = transform.rotation;
                        startingPoseSet = true;
                    }
                    //Debug.Log("is grabbed");
                    if (isGrabbed || isGrabbed2) // for 2 user case isGrabbed2 means that remote user has grabbed it
                    {
                        context.SendJson(new Message(transform, true, rb.isKinematic, zeroId));
                        prevPosition = transform.position;
                        prevRotation = transform.rotation;
                    }
                    // if not kinematic it won't be recorded
                }

            }
            else
            { 
                startingPoseSet = false;
                prevPosition = Vector3.zero;      
            }     
        }
        // in multi user mode we also need to send the position of the object when we are not recording
        else if (recRep.experiment.mode == ReplayMode.MultiUser)
        {
            if (isGrabbed && currentGrabberId.Equals(avatarManager.LocalAvatar.Id)) // if not grabbed we rely on physics and hope this will be ok
            {
                context.SendJson(new Message(transform, isGrabbed, rb.isKinematic, zeroId));   // only send actual id when grabbing first time or releasing
            }

            if (recRep.recording)
            {
                if (transform.position != prevPosition)
                {
                    if (!startingPoseSet)
                    {
                        Debug.Log("Set starting pos!");
                        context.SendJson(new Message(transform, isGrabbed, rb.isKinematic, zeroId));
                        prevPosition = transform.position;
                        prevRotation = transform.rotation;
                        startingPoseSet = true;
                    }
                }
            }
            else
            {
                startingPoseSet = false;
                prevPosition = Vector3.zero;
            }
        }
    }

    // whoever is grabbing the object sends the message that gets received here by all other peers
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        // in case of 2 users: 
        // isGrabbed: local, isGrabbed2: remote

        // in multi user mode, the person who takes an object from the current owner takes priority over ownership
        if (recRep.experiment.mode == ReplayMode.MultiUser)
        {
            var msg = message.FromJson<Message>();

            if (!msg.grabberId.Equals(zeroId))
            {
                if (msg.isGrabbed2)
                {
                    currentGrabberId = msg.grabberId;

                    if (isGrabbed)
                    {
                        isGrabbed = false;
                        handTransform = null;
                    }
                }
                else // released
                {
                    currentGrabberId = zeroId;
                }
                isGrabbed2 = msg.isGrabbed2;
                rb.isKinematic = msg.isKinematic;
            }
            if (!isGrabbed)
            {
                transform.localPosition = msg.transform.position;
                transform.localRotation = msg.transform.rotation;
            }
            
       

        }
        else
        {
            // if it is already grabbed before an incoming message that might set isGrabbed2 = false
            // then isGrabbed2 stays true and needs to be set false some other way!!!
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
    }

    // this only gets triggered by replayed avatars as their colliders are turned on
    // not for live avatars, their colliders are turned off (script: RIOInteractable)
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
}
   
