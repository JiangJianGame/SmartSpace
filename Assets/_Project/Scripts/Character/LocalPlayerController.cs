using UnityEngine;
using SmartSpace.Network;
using SmartSpace.UI;

namespace SmartSpace.Character
{
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float runSpeed = 8.5f;
        [SerializeField] private float rotationSpeed = 10.0f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -19.62f;

        [Header("Network Sync Settings")]
        [SerializeField] private float syncRate = 20f; // 20 updates/sec
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float rotationThreshold = 0.5f;

        [Header("References")]
        [SerializeField] private PlayerOverheadUI overheadUI;

        private CharacterController _controller;
        private Camera _mainCamera;
        private Vector3 _velocity;
        private bool _isGrounded;

        private Vector3 _lastSentPosition;
        private float _lastSentRotationY;
        private sbyte _lastSentAnimState;
        private float _syncTimer;
        private float _heartbeatTimer;

        // Animation States: 0=Idle, 1=Walk, 2=Run, 3=Jump, 4=Wave
        private sbyte _currentAnimState = 0;

        public string SessionId { get; set; }
        public string Username { get; set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
        }

        private void Start()
        {
            _lastSentPosition = transform.position;
            _lastSentRotationY = transform.eulerAngles.y;

            if (overheadUI != null)
            {
                overheadUI.SetUsername(Username, true);
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleNetworkSync();
        }

        private void HandleMovement()
        {
            _isGrounded = _controller.isGrounded;
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Slight downward push to keep grounded
            }

            // Input
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

            if (inputDirection.magnitude >= 0.1f)
            {
                // Move relative to camera
                Vector3 cameraForward = _mainCamera != null ? _mainCamera.transform.forward : Vector3.forward;
                Vector3 cameraRight = _mainCamera != null ? _mainCamera.transform.right : Vector3.right;
                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();

                Vector3 moveDir = (cameraForward * inputDirection.z + cameraRight * inputDirection.x).normalized;

                // Rotate character toward movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                // Move character
                float currentSpeed = isRunning ? runSpeed : walkSpeed;
                _controller.Move(moveDir * (currentSpeed * Time.deltaTime));

                _currentAnimState = (sbyte)(isRunning ? 2 : 1);
            }
            else
            {
                if (_currentAnimState != 4) // Don't override wave emote immediately
                {
                    _currentAnimState = 0; // Idle
                }
            }

            // Jump
            if (Input.GetButtonDown("Jump") && _isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _currentAnimState = 3; // Jump
            }

            // Apply Gravity
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleNetworkSync()
        {
            _syncTimer += Time.deltaTime;
            _heartbeatTimer += Time.deltaTime;

            float minInterval = 1f / syncRate;
            if (_syncTimer >= minInterval)
            {
                _syncTimer = 0f;

                bool posChanged = Vector3.Distance(transform.position, _lastSentPosition) > positionThreshold;
                bool rotChanged = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _lastSentRotationY)) > rotationThreshold;
                bool animChanged = _currentAnimState != _lastSentAnimState;
                bool heartbeat = _heartbeatTimer >= 1.0f; // Ensure at least 1 update per second

                if (posChanged || rotChanged || animChanged || heartbeat)
                {
                    _lastSentPosition = transform.position;
                    _lastSentRotationY = transform.eulerAngles.y;
                    _lastSentAnimState = _currentAnimState;
                    _heartbeatTimer = 0f;

                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                    {
                        NetworkManager.Instance.SendMove(
                            transform.position.x,
                            transform.position.y,
                            transform.position.z,
                            transform.eulerAngles.y,
                            _currentAnimState
                        );
                    }
                }
            }
        }

        public void TriggerEmote(sbyte emoteId)
        {
            _currentAnimState = emoteId;
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                NetworkManager.Instance.SendEmote(emoteId);
            }
        }

        public void ShowChatBubble(string message)
        {
            if (overheadUI != null)
            {
                overheadUI.ShowChat(message);
            }
        }
    }
}
