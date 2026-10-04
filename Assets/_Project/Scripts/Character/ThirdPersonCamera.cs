using UnityEngine;

namespace SmartSpace.Character
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 3.5f, -6f);
        [SerializeField] private float smoothSpeed = 8.0f;
        [SerializeField] private float lookAtHeight = 1.5f;

        [Header("Rotation Settings")]
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;

        private float _yaw = 0f;
        private float _pitch = 15f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                // Auto find local player if not assigned
                var localPlayer = FindObjectOfType<LocalPlayerController>();
                if (localPlayer != null)
                {
                    target = localPlayer.transform;
                    _yaw = target.eulerAngles.y;
                }
                return;
            }

            // Mouse orbit when right mouse button held or by default
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 desiredPosition = target.position + (rotation * offset);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}
