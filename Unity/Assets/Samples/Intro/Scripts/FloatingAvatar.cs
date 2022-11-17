using Ubiq.Avatars;
using UnityEngine;
using System.Collections.Generic;
using Ubiq.Messaging;

namespace Ubiq.Samples
{
    /// <summary>
    /// Recroom/rayman style avatar with hands, torso and head
    /// </summary>
    [RequireComponent(typeof(Avatars.Avatar))]
    [RequireComponent(typeof(ThreePointTrackedAvatar))]
    public class FloatingAvatar : MonoBehaviour
    {
        public Transform head;
        public Transform torso;
        public Transform leftHand;
        public Transform rightHand;

        public Renderer headRenderer;
        public Renderer torsoRenderer;
        public Renderer leftHandRenderer;
        public Renderer rightHandRenderer;

        public Transform baseOfNeckHint;

        // public float torsoFacingHandsWeight;
        public AnimationCurve torsoFootCurve;

        public AnimationCurve torsoFacingCurve;

        public TexturedAvatar texturedAvatar;

        private Avatars.Avatar avatar;
        private ThreePointTrackedAvatar trackedAvatar;
        private Vector3 footPosition;
        private Quaternion torsoFacing;

        // for record and replay to prevent having the torso dragged after the avatars when spawning for replay
        private NetworkScene scene;
        private RecorderReplayer recRep;
        private bool firstFrame = true;

        public class Message
        {
            public Vector3 position;
            public Quaternion rotation;
            public int frame;
            public float time;
        }


        private Queue<Message> headMessages;
        private Queue<Message> leftHandMessages;
        private Queue<Message> rightHandMessages;
        private float headStartTime = 0.0f;
        private float leftHandStartTime = 0.0f;
        private float rightHandStartTime = 0.0f;
        private Message headStart, headEnd, lefHandStart, leftHandEnd, rightHandStart, rightHandEnd;
        private float headTimeDiff, leftHandTimeDiff, rightHandTimeDiff = 0.0f;

        private void Awake()
        {
            headMessages = new Queue<Message>();
            leftHandMessages = new Queue<Message>();
            rightHandMessages = new Queue<Message>();

            avatar = GetComponent<Avatars.Avatar>();
            trackedAvatar = GetComponent<ThreePointTrackedAvatar>();
            scene = NetworkScene.FindNetworkScene(this);
            recRep = scene.GetComponentInChildren<RecorderReplayer>();
        }

        private void OnEnable()
        {
            trackedAvatar.OnHeadUpdate.AddListener(ThreePointTrackedAvatar_OnHeadUpdate);
            trackedAvatar.OnLeftHandUpdate.AddListener(ThreePointTrackedAvatar_OnLeftHandUpdate);
            trackedAvatar.OnRightHandUpdate.AddListener(ThreePointTrackedAvatar_OnRightHandUpdate);

            if (texturedAvatar)
            {
                texturedAvatar.OnTextureChanged.AddListener(TexturedAvatar_OnTextureChanged);
            }
        }

        private void OnDisable()
        {
            if (trackedAvatar && trackedAvatar != null)
            {
                trackedAvatar.OnHeadUpdate.RemoveListener(ThreePointTrackedAvatar_OnHeadUpdate);
                trackedAvatar.OnLeftHandUpdate.RemoveListener(ThreePointTrackedAvatar_OnLeftHandUpdate);
                trackedAvatar.OnRightHandUpdate.RemoveListener(ThreePointTrackedAvatar_OnRightHandUpdate);
            }

            if (texturedAvatar && texturedAvatar != null)
            {
                texturedAvatar.OnTextureChanged.RemoveListener(TexturedAvatar_OnTextureChanged);
            }
        }

        private int inbetween = 0;
        private float increment = 0.0f;
        private float t = 0.0f;
        private bool init = false;
        // smoothen during update
        private void SmoothenMovements()
        {
            // check if we have already two points between which we can interpolate
            if (inbetween > 1)
            {

                head.position = Vector3.Lerp(headStart.position, headEnd.position, t);
                head.rotation = Quaternion.Lerp(headStart.rotation, headEnd.rotation, t); // or should I use Slerp (slower but looks nicer?)
                inbetween--;
                t = t + increment;
            }
            else // no frames inbetween 
            {
                if (headMessages.Count >= 1)
                {
                    if (!init)
                    {
                        if (headMessages.Count >= 2)
                        {
                            headStart = headMessages.Dequeue();
                            init = true;
                        }
                    }
                    else
                    {
                        headStart = headEnd;
                    }

                    if (init)
                    {
                        headEnd = headMessages.Dequeue();
                        inbetween = headEnd.frame - headStart.frame;
                        increment = 1 / inbetween;
                        t = increment;

                        // set start transform
                        head.position = headStart.position;
                        head.rotation = headStart.rotation;
                        inbetween--;
                    }
                }
            }

            //if(headStart == null && headMessages.Count >= 2)
            //{
            //    // get two points to start interpolating
            //    if (!init)
            //    {
            //        headStart = headMessages.Dequeue();
            //        init = true;
            //    }
            //    else
            //    {
            //        headStart = headEnd;
            //    }
            //    headEnd = headMessages.Dequeue();
            //    headStartTime = Time.unscaledTime;
            //    headTimeDiff = headEnd.time -  headStart.time;
                
            //}
            //if (headStart != null && headEnd != null)
            //{
            //    // time advances since start point
            //    var t = (Time.unscaledTime - headStartTime) / headTimeDiff;
            //    Debug.Log("time : " + t + " " + (Time.unscaledTime - headStartTime) +  " head start time: " + headStartTime + " diff: " + headTimeDiff);
            //    if (t <= 1)
            //    {
            //        head.position = Vector3.Lerp(headStart.position, headEnd.position, t);
            //        head.rotation = Quaternion.Lerp(headStart.rotation, headEnd.rotation, t); // or should I use Slerp (slower but looks nicer?)
            //    }
            //    else // t is already in the next interval so get the next point and interpolate between previous end and next point
            //    {
            //        if (headMessages.Count > 0)
            //        {
            //            headStart = headEnd;
            //            headEnd = headMessages.Dequeue();
            //            // calculate where next interval would have started: current time
            //            headStartTime = headStartTime + headTimeDiff;
            //            headTimeDiff = headEnd.time - headStart.time;

            //            // with updated startTime and timeDiff
            //            t = (Time.unscaledTime - headStartTime) / headTimeDiff;
            //            head.position = Vector3.Lerp(headStart.position, headEnd.position, t);
            //            head.rotation = Quaternion.Lerp(headStart.rotation, headEnd.rotation, t);
            //        }
            //    }
            //}
        }

        private float startTime = 0.0f;
        Message firstMsg;
        private bool saveFirst = false;
        private void ThreePointTrackedAvatar_OnHeadUpdate(Vector3 pos, Quaternion rot)
        {
            // if multiple messages arrive in one frame take the last one that came
            Message msg = new Message() { position = pos, rotation = rot, frame = Time.frameCount, time = Time.unscaledTime };
            //Debug.Log(avatar.IsLocal + " " + avatar.Id + " " + lastFrameHead + " " + Time.frameCount);

            if (!avatar.IsLocal)
            {
                if (!saveFirst)
                {
                    firstMsg = msg;
                    startTime = Time.unscaledTime;
                    saveFirst = true;
                }

                if ((Time.unscaledTime - startTime) >= 0.1)
                {
                    head.position = Vector3.Lerp(firstMsg.position, pos, 0.5f);
                    head.rotation = Quaternion.Lerp(firstMsg.rotation, rot, 0.05f);

                    if (recRep.replaying && recRep.play && firstFrame)
                    {
                        Debug.Log("call: " + baseOfNeckHint.position.ToString());
                        footPosition = baseOfNeckHint.position;
                        torsoFacing = Quaternion.LookRotation(head.forward, Vector3.up);
                        firstFrame = false;
                    }

                    saveFirst = false;
                }
            }
            else
            {
                head.position = pos;
                head.rotation = rot;
            }

            //if (lastFrameHead != Time.frameCount)
            //{
            //    if (prevHeadMsg != null)
            //    {
            //        headMessages.Enqueue(prevHeadMsg);
            //        //Debug.Log(avatar.IsLocal + " " + avatar.Id + " queue: " + headMessages.Count + " frame: " + Time.frameCount);
            //    }
            //}

            //lastFrameHead = Time.frameCount;
            //prevHeadMsg = msg;

            //if (headMessages.Count == 2)
            //{
            //    var pos1 = headMessages.Dequeue();
            //    var pos2 = headMessages.Dequeue();

            //    head.position = Vector3.Lerp(pos1.position, pos2.position, 0.5f);
            //    head.rotation = Quaternion.Lerp(pos1.rotation, pos2.rotation, 0.05f);
            //}
            //if (!avatar.IsLocal)
            //{
            //    Debug.Log(pos.x + " " + avatar.Id + " " + Time.frameCount);

            //}

            //head.position = pos;
            //head.rotation = rot;
        }

        private float startTimeLeft = 0.0f;
        Message firstMsgLeft;
        private bool saveFirstLeft = false;
        private void ThreePointTrackedAvatar_OnLeftHandUpdate(Vector3 pos, Quaternion rot)
        {
            Message msg = new Message() { position = pos, rotation = rot, frame = Time.frameCount, time = Time.unscaledTime };
            //Debug.Log(avatar.IsLocal + " " + avatar.Id + " " + lastFrameHead + " " + Time.frameCount);

            if (!avatar.IsLocal)
            {
                if (!saveFirstLeft)
                {
                    firstMsgLeft = msg;
                    startTimeLeft = Time.unscaledTime;
                    saveFirstLeft = true;
                }

                if ((Time.unscaledTime - startTime) >= 0.1)
                {
                    leftHand.position = Vector3.Lerp(firstMsgLeft.position, pos, 0.5f);
                    leftHand.rotation = Quaternion.Lerp(firstMsgLeft.rotation, rot, 0.05f);
                    saveFirstLeft = false;
                }
            }
            else
            {
                leftHand.position = pos;
                leftHand.rotation = rot;
            }
        }
        private float startTimeRight = 0.0f;
        Message firstMsgRight;
        private bool saveFirstRight = false;
        private void ThreePointTrackedAvatar_OnRightHandUpdate(Vector3 pos, Quaternion rot)
        {
            Message msg = new Message() { position = pos, rotation = rot, frame = Time.frameCount, time = Time.unscaledTime };
            //Debug.Log(avatar.IsLocal + " " + avatar.Id + " " + lastFrameHead + " " + Time.frameCount);

            if (!avatar.IsLocal)
            {
                if (!saveFirstRight)
                {
                    firstMsgRight = msg;
                    startTimeRight = Time.unscaledTime;
                    saveFirstRight = true;
                }

                if ((Time.unscaledTime - startTime) >= 0.1)
                {
                    rightHand.position = Vector3.Lerp(firstMsgRight.position, pos, 0.5f);
                    rightHand.rotation = Quaternion.Lerp(firstMsgRight.rotation, rot, 0.05f);
                    saveFirstRight = false;
                }
            }
            else
            {
                rightHand.position = pos;
                rightHand.rotation = rot;
            }
        }

        private void TexturedAvatar_OnTextureChanged(Texture2D tex)
        {
            headRenderer.material.mainTexture = tex;
            torsoRenderer.material = headRenderer.material;
            leftHandRenderer.material = headRenderer.material;
            rightHandRenderer.material = headRenderer.material;
        }

        private void Update()
        {
            //SmoothenMovements();

            UpdateTorso();

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            headRenderer.enabled = true;
            torsoRenderer.enabled = true;
            leftHandRenderer.enabled = true;
            rightHandRenderer.enabled = true;

            //if (avatar.IsLocal)
            //{
            //    //if(renderToggle != null && renderToggle.rendering)
            //    {
            //        headRenderer.enabled = false;
            //        torsoRenderer.enabled = true;
            //        leftHandRenderer.enabled = true;
            //        rightHandRenderer.enabled = true;
            //    }
            //    //else
            //    //{
            //    //    headRenderer.enabled = false;
            //    //    torsoRenderer.enabled = false;
            //    //    leftHandRenderer.enabled = false;
            //    //    rightHandRenderer.enabled = false;
            //    //}

            //    //renderToggle.Send();
            //}
            //else
            //{
            //    //if (renderToggle != null && renderToggle.rendering)
            //    {
            //        headRenderer.enabled = true;
            //        torsoRenderer.enabled = true;
            //        leftHandRenderer.enabled = true;
            //        rightHandRenderer.enabled = true;

            //    }
            //    //else
            //    //{
            //    //    headRenderer.enabled = false;
            //    //    torsoRenderer.enabled = false;
            //    //    leftHandRenderer.enabled = false;
            //    //    rightHandRenderer.enabled = false;
            //    //}
            //}
            ////renderToggle.Send();

        }

        private int frameNr = 0;
        private void UpdateTorso()
        {
            // Give torso a bit of dynamic movement to make it expressive

            // Update virtual 'foot' position, just for animation, wildly inaccurate :)
            var neckPosition = baseOfNeckHint.position;
            footPosition.x += (neckPosition.x - footPosition.x) * Time.deltaTime * torsoFootCurve.Evaluate(Mathf.Abs(neckPosition.x - footPosition.x));
            footPosition.z += (neckPosition.z - footPosition.z) * Time.deltaTime * torsoFootCurve.Evaluate(Mathf.Abs(neckPosition.z - footPosition.z));
            footPosition.y = 0;

            // Forward direction of torso is vector in the transverse plane
            // Determined by head direction primarily, hint provided by hands
            var torsoRotation = Quaternion.identity;

            // Head: Just use head direction
            var headFwd = head.forward;
            headFwd.y = 0;

            // Hands: TODO (this breaks too much currently)
            // Hands: Imagine line between hands, take normal (in transverse plane)
            // Use head orientation as a hint to give us which normal to use
            // var handsLine = rightHand.position - leftHand.position;
            // var handsFwd = new Vector3(-handsLine.z,0,handsLine.x);
            // if (Vector3.Dot(handsFwd,headFwd) < 0)
            // {
            //     handsFwd = new Vector3(handsLine.z,0,-handsLine.x);
            // }
            // handsFwdStore = handsFwd;

            // var headRot = Quaternion.LookRotation(headFwd,Vector3.up);
            // var handsRot = Quaternion.LookRotation(handsFwd,Vector3.up);

            // // Rotation is handsRotation capped to a distance from headRotation
            // var headToHandsAngle = Quaternion.Angle(headRot,handsRot);
            // Debug.Log(headToHandsAngle);
            // var rot = Quaternion.RotateTowards(headRot,handsRot,Mathf.Clamp(headToHandsAngle,-torsoFacingHandsWeight,torsoFacingHandsWeight));

            // // var rot = Quaternion.SlerpUnclamped(handsRot,headRot,torsoFacingHeadToHandsWeightRatio);

            var rot = Quaternion.LookRotation(headFwd, Vector3.up);
            var angle = Quaternion.Angle(torsoFacing, rot);
            var rotateAngle = Mathf.Clamp(Time.deltaTime * torsoFacingCurve.Evaluate(Mathf.Abs(angle)), 0, angle);
            torsoFacing = Quaternion.RotateTowards(torsoFacing, rot, rotateAngle);

            // Place torso so it makes a straight line between neck and feet
            torso.position = neckPosition;
            torso.rotation = Quaternion.FromToRotation(Vector3.down, footPosition - neckPosition) * torsoFacing;
        }

        // private Vector3 handsFwdStore;

        // private void OnDrawGizmos()
        // {
        //     Gizmos.color = Color.blue;
        //     Gizmos.DrawLine(head.position, footPosition);
        //     // Gizmos.DrawLine(head.position,head.position + handsFwdStore);
        // }
    }
}