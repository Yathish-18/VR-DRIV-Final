using UnityEngine;

namespace PampelGames.RoadConstructor.Demo
{
    public class CameraController : MonoBehaviour
    {
        public float speed = 10f;
        public float increaseSpeed = 1.5f;
        public float panningSpeed = 20f;

        public KeyCode forward = KeyCode.W;
        public KeyCode backwards = KeyCode.S;
        public KeyCode right = KeyCode.D;
        public KeyCode left = KeyCode.A;
        public KeyCode doubleSpeed = KeyCode.LeftShift;

        public float sensitivity = 0.01f;

        public float minAngle = 90f;
        public float maxAngle = 270f;

        private float currentSpeed;
        private bool moving;
        private Vector3 cameraPosition;

        private void Update()
        {
            var lastMoving = moving;
            var deltaPosition = Vector3.zero;

            if (moving) currentSpeed += increaseSpeed * Time.deltaTime;
            moving = false;

            Move(ref deltaPosition, forward, transform.forward, false, false);
            Move(ref deltaPosition, backwards, -transform.forward, false, false);
            Move(ref deltaPosition, right, transform.right, false, false);
            Move(ref deltaPosition, left, -transform.right, false, false);

            if (moving)
            {
                if (moving != lastMoving) currentSpeed = speed;
                var shiftMultiplier = Input.GetKey(doubleSpeed) ? 2.0f : 1.0f;
                transform.position += deltaPosition * currentSpeed * Time.deltaTime * shiftMultiplier;
            }
            else
            {
                currentSpeed = 0f;
            }
            
            if (Input.GetMouseButton(1))
            {
                var eulerAngles = transform.eulerAngles;
                eulerAngles.x += -Input.GetAxis("Mouse Y") * 359f * sensitivity;
                eulerAngles.y += Input.GetAxis("Mouse X") * 359f * sensitivity;
                if (eulerAngles.x < minAngle || eulerAngles.x > maxAngle) transform.eulerAngles = eulerAngles;
            }

            if (Input.GetMouseButtonDown(2)) cameraPosition = gameObject.transform.position;

            if (Input.GetMouseButton(2))
            {
                float newX = cameraPosition.x - Input.GetAxis("Mouse X") * panningSpeed * sensitivity;
                float newY = cameraPosition.y + Input.GetAxis("Mouse Y") * panningSpeed * sensitivity;
                float newZ = cameraPosition.z;

                gameObject.transform.position = new Vector3(newX, newY, newZ);
                cameraPosition = gameObject.transform.position;
            }
        }

        private void Move(ref Vector3 deltaPosition, KeyCode keyCode, Vector3 directionVector, bool forceForward, bool forceBackward)
        {
            if (!Input.GetKey(keyCode) && !forceBackward && !forceForward) return;
            deltaPosition += directionVector;
            moving = true;
        }
    }
}