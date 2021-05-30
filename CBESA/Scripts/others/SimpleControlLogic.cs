using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VPRAssets.Scripts
{
    public class SimpleControlLogic : MonoBehaviour
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

        class RayHitComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                return ((RaycastHit)x).distance.CompareTo(((RaycastHit)y).distance);
            }
        }

        private void Awake()
        {
            // Set up a reference to the capsule collider.
            capsule = GetComponent<Collider>() as CapsuleCollider;
            rayHitComparer = new RayHitComparer();
            originalRotation = transform.rotation;
        }

        private void Update()
        {
            input =
            new Quaternion(leftController.GetTouchPosition.x * 0.8f, // movement
                        leftController.GetTouchPosition.y * 0.8f,
                        rightController.GetTouchPosition.x * 0.5f, // rotation
                        rightController.GetTouchPosition.x * 0.5f);

            transform.position = calculatePosition(input.x, input.y);
            transform.rotation = calculateRotation(input.z, input.w);

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
