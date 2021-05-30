using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using SPINACH.Networking;
using SPINACH.Media;
using UnityEngine;

namespace VPRAssets.Scripts
{
    public class PlayerControlLogic : MonoBehaviour
    {
        // Joystick
        public SimpleTouchController leftController;
	    public SimpleTouchController rightController;

        // Player control related variables and classes 
        private float moveDistance = 0.1f;
        Vector3 targetAngles;
        Vector3 followAngles;
        Vector3 followVelocity;
        Quaternion originalRotation;
        Vector3 originalPosition;
        private CapsuleCollider capsule;                                                    // The capsule collider for the first person character
        private IComparer rayHitComparer;
        private Quaternion input;
        public Vector2 rotationRange = new Vector3(70, 70);
        public float rotationSpeed = 10.0f;
        public float dampingTime = 0.2f;

        public GameObject VPRCameraPrefab;
        
        private ConcurrentQueue<Vector3> tmpPositionBuffer;
        private ConcurrentQueue<Quaternion> tmpRotationBuffer;
        private Vector3 tmpPosition;
        private Quaternion tmpRotation;
        
        private ConcurrentQueue<Vector3> receivedPositionBuffer;
        private ConcurrentQueue<Quaternion> receivedRotationBuffer;
        private Vector3 receivedPosition;
        private Quaternion receivedRotation;
        public static bool bufferDequeueSuccess = false; // receivedUserInputBuffer Dequeue success
        public static bool clientMessageIsReceived = false;

        private NetworkObjectMessenger _nom;
        public const byte NTYPE = 2;
        class TransformInfoPacket : IRoutablePacketContent
        {
            public Vector3 position;
            public Quaternion rotation;

            public TransformInfoPacket(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }

            public TransformInfoPacket(byte[] bytes)
            {
                position = Utils.DecodeVector3(bytes, 0);
                rotation = Utils.DecodeQuaternion(bytes, 12);
            }

            public byte GetNType()
            {
                return NTYPE;
            }

            public byte GetRevision()
            {
                return 0;
            }

            public int GetByteLength()
            {
                return 12 + 16;
            }

            public byte[] GetByteStream()
            {
                var buf = new byte[GetByteLength()];
                Utils.EncodeVector3(position, buf, 0);
                Utils.EncodeQuaternion(rotation, buf, 12);
                return buf;
            }
        }

        class RayHitComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                return ((RaycastHit)x).distance.CompareTo(((RaycastHit)y).distance);
            }
        }

        private void Awake()
        {
            Instantiate(VPRCameraPrefab, transform.Find("Head"));

            _nom = GetComponent<NetworkObjectMessenger>();
            if (NetworkDispatch.Default().isServer)
            {
                _nom.RegisterMethod(NTYPE, WhenClientMessageIsReceived);
            }

            // Set up a reference to the capsule collider.
            capsule = GetComponent<Collider>() as CapsuleCollider;
            rayHitComparer = new RayHitComparer();
            originalRotation = transform.rotation;
        }

        private void Update()
        {
            if (NetworkDispatch.Default().isServer)
            {
                WaitForClientMessage();
            }

            if (!NetworkDispatch.Default().isServer)
            {
                // capture user input
                input =
                new Quaternion(leftController.GetTouchPosition.x * 0.8f, // movement
                            leftController.GetTouchPosition.y * 0.8f,
                            rightController.GetTouchPosition.x * 0.5f, // rotation
                            rightController.GetTouchPosition.x * 0.5f);

                transform.position = calculatePosition(input.x, input.y);
                transform.rotation = calculateRotation(input.z, input.w);
                _nom.SendMessage(new TransformInfoPacket(transform.position, transform.rotation));
                // Debug.Log(string.Format("Client sent Time is {0}.{1}", DateTime.Now, DateTime.Now.Millisecond));
            }
        }
        
        void WhenClientMessageIsReceived(byte rev, byte[] content)
        {
            if (rev != 0) return;
            var p = new TransformInfoPacket(content);

            transform.position = p.position;
            transform.rotation = p.rotation;

            clientMessageIsReceived = true;
        }

        void WaitForClientMessage()
        {
            // while(true)
            // {
            //     Debug.Log("Server waiting for Client message");
            //     if(clientMessageIsReceived) 
            //     {
            //         Debug.Log("Server successfully receive Client message");
            //         clientMessageIsReceived = false;
            //         break;
            //     }
            // }
        }

        Quaternion calculateRotation(float inputH, float inputV)
        {
            // wrap values to avoid springing quickly the wrong way from positive to negative
            if (targetAngles.y > 180) { targetAngles.y -= 360; followAngles.y -= 360; }
            if (targetAngles.x > 180) { targetAngles.x -= 360; followAngles.x -= 360; }
            if (targetAngles.y < -180) { targetAngles.y += 360; followAngles.y += 360; }
            if (targetAngles.x < -180) { targetAngles.x += 360; followAngles.x += 360; }

            // with mouse input, we have direct control with no springback required.
            targetAngles.y += inputH * rotationSpeed;
            targetAngles.x += inputV * rotationSpeed;

            // clamp values to allowed range
            targetAngles.y = Mathf.Clamp(targetAngles.y, -rotationRange.y * 0.5f, rotationRange.y * 0.5f);
            targetAngles.x = Mathf.Clamp(targetAngles.x, -rotationRange.x * 0.5f, rotationRange.x * 0.5f);

            // smoothly interpolate current values to target angles
            followAngles = Vector3.SmoothDamp(followAngles, targetAngles, ref followVelocity, dampingTime);

            Quaternion rotation = originalRotation * Quaternion.Euler(-followAngles.x, followAngles.y, 0);

            return rotation;
        }

        Vector3 calculatePosition(float inputX, float inputY)
        {
            originalPosition = transform.position;
            if (inputX > 0) inputX = 1;
            else if (inputX < 0) inputX = -1;
            if (inputY > 0) inputY = 1;
            else if (inputY < 0) inputY = -1;
            Vector3 position = originalPosition + transform.forward * inputY * moveDistance + transform.right * inputX * moveDistance;

            return position;
        }
    }
}