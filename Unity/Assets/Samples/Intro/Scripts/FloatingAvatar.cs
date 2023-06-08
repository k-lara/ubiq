using Ubiq.Avatars;
using UnityEngine;
using System.Collections;
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

        private Message firstPoseHead, secondPoseHead, firstPoseLeft, secondPoseLeft, firstPoseRight, secondPoseRight;
        private Message[] currentPosesHead;
        private Message[] currentPosesLeft;
        private Message[] currentPosesRight;
        private float intervalStartTime;
        private float currentTime;
        private float intervalLength = 0.1f; // 100 ms (same as in WebGL study) 
        private bool startSmoothing = false;

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
        public bool firstMessage = false;
        private bool firstMessageLeft = false;
        private bool firstMessageRight = false;

        public class Message
        {
            public Vector3 position;
            public Quaternion rotation;
            public float time;
        }

        private void Awake()
        {
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

        private void ThreePointTrackedAvatar_OnHeadUpdate(Vector3 pos, Quaternion rot)
        {
            head.position = pos;
            head.rotation = rot;
        }

        private void ThreePointTrackedAvatar_OnLeftHandUpdate(Vector3 pos, Quaternion rot)
        {
            leftHand.position = pos;
            leftHand.rotation = rot;
        }
        private void ThreePointTrackedAvatar_OnRightHandUpdate(Vector3 pos, Quaternion rot)
        {
            rightHand.position = pos;
            rightHand.rotation = rot;
        }

        private void TexturedAvatar_OnTextureChanged(Texture2D tex)
        {
            headRenderer.material.mainTexture = tex;
            torsoRenderer.material = headRenderer.material;
            leftHandRenderer.material = headRenderer.material;
            rightHandRenderer.material = headRenderer.material;
        }

        private void Start()
        {
            intervalStartTime = Time.unscaledTime;
            StartCoroutine(Wait(intervalLength));
        }

        private IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            startSmoothing = true;
        }

        private void Update()
        {
            // fixed smoothing interval (e.g. 50 ms intervalLenght)
            if (startSmoothing)
            {
                // make sure we already have a second pose for interpolation
                if (secondPoseHead != null)
                {
                    if (currentPosesHead == null) // this is only try for the very first interval
                    {
                        currentPosesHead = new Message[] { firstPoseHead, secondPoseHead };
                        currentPosesLeft = new Message[] { firstPoseLeft, secondPoseLeft };
                        currentPosesRight = new Message[] { firstPoseRight, secondPoseRight };
                        
                        intervalStartTime = Time.unscaledTime;
                    }

                    currentTime = Time.unscaledTime - intervalStartTime;
                    // if we are over the interval, we take the next one
                    if (currentTime > intervalLength)
                    {
                        // calculate when next interval would have started
                        intervalStartTime = Time.unscaledTime - (currentTime - intervalLength);
                        currentPosesHead[0] = currentPosesHead[1]; currentPosesHead[1] = secondPoseHead;
                        currentPosesLeft[0] = currentPosesLeft[1]; currentPosesLeft[1] = secondPoseLeft;
                        currentPosesRight[0] = currentPosesRight[1]; currentPosesRight[1] = secondPoseRight;
                        
                        var t = (currentTime - intervalLength) / intervalLength;
                       
                        head.position = Vector3.Lerp(currentPosesHead[0].position, currentPosesHead[1].position, t);
                        head.rotation = Quaternion.Lerp(currentPosesHead[0].rotation, currentPosesHead[1].rotation, t);
                        leftHand.position = Vector3.Lerp(currentPosesLeft[0].position, currentPosesLeft[1].position, t);
                        leftHand.rotation = Quaternion.Lerp(currentPosesLeft[0].rotation, currentPosesLeft[1].rotation, t);
                        rightHand.position = Vector3.Lerp(currentPosesRight[0].position, currentPosesRight[1].position, t);
                        rightHand.rotation = Quaternion.Lerp(currentPosesRight[0].rotation, currentPosesRight[1].rotation, t);
                    }
                    else
                    {
                        var t = currentTime / intervalLength;
                        
                        head.position = Vector3.Lerp(currentPosesHead[0].position, currentPosesHead[1].position, t);
                        head.rotation = Quaternion.Lerp(currentPosesHead[0].rotation, currentPosesHead[1].rotation, t);
                        leftHand.position = Vector3.Lerp(currentPosesLeft[0].position, currentPosesLeft[1].position, t);
                        leftHand.rotation = Quaternion.Lerp(currentPosesLeft[0].rotation, currentPosesLeft[1].rotation, t);
                        rightHand.position = Vector3.Lerp(currentPosesRight[0].position, currentPosesRight[1].position, t);
                        rightHand.rotation = Quaternion.Lerp(currentPosesRight[0].rotation, currentPosesRight[1].rotation, t);
                    }

                }
            }

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