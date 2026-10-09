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
        private Light _studioLight;

        public static ThirdPersonCamera Instance { get; private set; }

        public bool IsProfileViewMode { get; set; } = false;
        public Transform ProfileTarget { get; set; }

        private void Awake()
        {
            Instance = this;

            _studioLight = GetComponent<Light>();
            if (_studioLight == null)
            {
                _studioLight = gameObject.AddComponent<Light>();
                _studioLight.type = LightType.Directional;
                _studioLight.intensity = 0.9f;
                _studioLight.color = new Color(1.0f, 0.96f, 0.92f);
                _studioLight.enabled = false;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }
        }

        private bool _wasInProfileView = false;
        private Vector3 _profileLockedCamPos;
        private Quaternion _profileLockedCamRot;

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

            if (IsProfileViewMode)
            {
                Transform viewTarget = ProfileTarget != null ? ProfileTarget : target;
                if (viewTarget != null)
                {
                    if (!_wasInProfileView)
                    {
                        _wasInProfileView = true;
                        Vector3 fwd = viewTarget.forward;
                        Vector3 right = viewTarget.right;
                        Vector3 up = Vector3.up;

                        // Camera positioned in front of character, with lookPoint shifted to screen right
                        // so the character is framed centered horizontally at 27.4% from screen left
                        _profileLockedCamPos = viewTarget.position + fwd * 2.7f + up * 0.95f;
                        Vector3 lookPoint = viewTarget.position - right * 1.25f + up * 0.95f;
                        _profileLockedCamRot = Quaternion.LookRotation(lookPoint - _profileLockedCamPos);
                    }

                    transform.position = Vector3.Lerp(transform.position, _profileLockedCamPos, Time.deltaTime * smoothSpeed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, _profileLockedCamRot, Time.deltaTime * smoothSpeed);
                }
                if (_studioLight != null) _studioLight.enabled = true;
                return;
            }
            else
            {
                _wasInProfileView = false;
                if (_studioLight != null) _studioLight.enabled = false;
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
