using System;
using System.Linq;
using UnityEngine;

namespace Ubiq.XR
{
    /// <summary>
    /// This VR Player Controller supports a typical Head and Two Hand tracked rig.
    /// </summary>
    public class XRPlayerController : MonoBehaviour
    {
        public bool dontDestroyOnLoad = true;
        public bool joystickMovementEnabled;

        private static GameObject singleton;

        [NonSerialized]
        public HandController[] handControllers;

        private Vector3 velocity;
        private Vector3 userLocalPosition;

        public float joystickDeadzone = 0.1f;
        public float joystickFlySpeed = 1.2f;

        public Camera headCamera;
        public Transform cameraContainer;
        public AnimationCurve cameraRubberBand;

        public CharacterController characterController;

        private void Awake()
        {
            if(dontDestroyOnLoad)
            {
                if(singleton != null)
                {
                    gameObject.SetActive(false);
                    DestroyImmediate(gameObject);
                    return;
                }

                singleton = gameObject;
                DontDestroyOnLoad(gameObject);
                Extensions.MonoBehaviourExtensions.DontDestroyOnLoadGameObjects.Add(gameObject);
            }

            handControllers = GetComponentsInChildren<HandController>();
            characterController = GetComponent<CharacterController>();
        }

        private void RotateCamera(bool pressed)
        {
            if (pressed)
            {
                transform.RotateAround(headCamera.transform.position, Vector3.up, 90f);
            }
        }

        private void Start()
        {
                // set size and initial position of character controller based on camera position
            characterController.height = headCamera.transform.position.y - 0.1f;
            characterController.center = new Vector3(headCamera.transform.position.x, characterController.height / 2f, headCamera.transform.position.z);

            foreach (var item in GetComponentsInChildren<TeleportRay>())
            {
                item.OnTeleport.AddListener(OnTeleport);
            }

            foreach (var item in handControllers)
            {
                if (item.Left)
                {
                    //item.JoystickPress.AddListener(RotateCamera);
                }
            }
        }

        public void OnTeleport(Vector3 position)
        {
            userLocalPosition = transform.InverseTransformPoint(headCamera.transform.position);
            userLocalPosition.y = 0;

            var movement = position - transform.TransformPoint(userLocalPosition);  // move so the foot position is over the new teleport location
            transform.position += movement;
        }

        private void OnGround()
        {
            var height = Mathf.Clamp(transform.InverseTransformPoint(headCamera.transform.position).y, 0.1f, float.PositiveInfinity);
            var origin = transform.position + userLocalPosition + Vector3.up * height;
            var direction = Vector3.down;

            RaycastHit hitInfo;
            if(Physics.Raycast(new Ray(origin, direction), out hitInfo))
            {
                var virtualFloorHeight = hitInfo.point.y;

                if (transform.position.y < virtualFloorHeight)
                {
                    transform.position += Vector3.up * (virtualFloorHeight - transform.position.y) * Time.deltaTime * 3f;
                    velocity = Vector3.zero;
                }
                else
                {
                    velocity += Physics.gravity * Time.deltaTime;
                }
            }
            else
            {
                velocity = Vector3.zero; // if there is no 'ground' in the scene, then do nothing
            }

            transform.position += velocity * Time.deltaTime;
        }


        private Vector3 playerVelocity = Vector3.zero;
        private void FixedUpdate()
        {
            // Update the foot position. This is done by pulling the feet using a rubber band.
            // Decoupling the feet in this way allows the user to do things like lean over edges, when the ground check is enabled.
            // This can be effectively disabled by setting the animation curve to a constant high value.
            if (characterController != null)
            {
                if (characterController.isGrounded)
                {
                    playerVelocity.y = 0f;
                    characterController.height = headCamera.transform.localPosition.y - 0.01f;
                    characterController.center = new Vector3(headCamera.transform.localPosition.x, characterController.height / 2f, headCamera.transform.localPosition.z);
                }
                else
                {
                    playerVelocity.y += -9.81f * Time.deltaTime;
                    characterController.Move(playerVelocity * Time.deltaTime);
                }
            }
            foreach (var item in handControllers)
            {
                if (item.Right)
                {
                    if (item.JoystickSwipe.Trigger)
                    {
                        // disable swipe for now
                        //transform.RotateAround(headCamera.transform.position, Vector3.up, 45f * Mathf.Sign(item.JoystickSwipe.Value));
                    }
                }
                else if (item.Left && joystickMovementEnabled)
                {
                    var dir = item.Joystick.normalized;
                    var mag = item.Joystick.magnitude;
                    if (mag > joystickDeadzone)
                    {
                        var speedMultiplier = Mathf.InverseLerp(joystickDeadzone, 1.0f, mag);
                        var worldDir = headCamera.transform.TransformDirection(dir.x, 0, dir.y);
                        worldDir.y = 0;
                        var distance = (joystickFlySpeed * Time.deltaTime);
                        
                        if (characterController != null)
                        {
                            // var move = distance * new Vector3(-dir.x, 0, dir.y);
                            // Debug.Log(dir.ToString() + " " + Input.GetAxis("Vertical") + " " + Input.GetAxis("Horizontal"));
                            // var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                            characterController.Move(distance * worldDir.normalized);   

                            // if (move != Vector3.zero)
                            // {
                            //     transform.forward = move;
                            // } 
                        }
                        else
                        {
                            transform.position += distance * worldDir.normalized;
                        }
                    }
                }
                else
                {
                    
                }
            }


            var headProjectionXZ = transform.InverseTransformPoint(headCamera.transform.position);
            headProjectionXZ.y = 0;
            userLocalPosition.x += (headProjectionXZ.x - userLocalPosition.x) * Time.deltaTime * cameraRubberBand.Evaluate(Mathf.Abs(headProjectionXZ.x - userLocalPosition.x));
            userLocalPosition.z += (headProjectionXZ.z - userLocalPosition.z) * Time.deltaTime * cameraRubberBand.Evaluate(Mathf.Abs(headProjectionXZ.z - userLocalPosition.z));
            userLocalPosition.y = 0;
        }

        private void OnDrawGizmos()
        {
            if (!headCamera) {
                return;
            }

            Gizmos.color = Color.blue;
            Gizmos.matrix = transform.localToWorldMatrix;
           //Gizmos.DrawWireCube(Vector3.zero, new Vector3(Radius, 0.1f, Radius));
            Gizmos.DrawLine(userLocalPosition, transform.InverseTransformPoint(headCamera.transform.position));
        }
    }
}
