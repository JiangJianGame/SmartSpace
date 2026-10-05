using System.Collections;
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
        [SerializeField] private Transform visualTransform;

        private CharacterController _controller;
        private Camera _mainCamera;
        private Vector3 _velocity;
        private bool _isGrounded;

        private Vector3 _lastSentPosition;
        private float _lastSentRotationY;
        private sbyte _lastSentAnimState;
        private float _syncTimer;
        private float _heartbeatTimer;

        // Animation States: 0=Idle, 1=Walk, 2=Run, 3=Jump, 10+=Emotes
        private sbyte _currentAnimState = 0;
        private EmoteEffects _emoteEffects;
        private Coroutine _emoteAnimCoroutine;

        public string SessionId { get; set; }
        public string Username { get; set; }
        public int AvatarId { get; set; }

        public void SetProfile(string newUsername, int newAvatarId)
        {
            Username = newUsername;
            AvatarId = newAvatarId;
            if (overheadUI != null)
            {
                overheadUI.SetUsername(Username, true);
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;

            if (visualTransform == null)
            {
                Transform v = transform.Find("Visual");
                visualTransform = v != null ? v : transform;
            }

            _emoteEffects = gameObject.GetComponent<EmoteEffects>();
            if (_emoteEffects == null)
            {
                _emoteEffects = gameObject.AddComponent<EmoteEffects>();
            }
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

                // Cancel emote if moving
                if (_emoteAnimCoroutine != null)
                {
                    StopCoroutine(_emoteAnimCoroutine);
                    _emoteAnimCoroutine = null;
                    ResetVisualTransform();
                }
            }
            else
            {
                // Idle (if not performing emote >= 10)
                if (_currentAnimState < 10)
                {
                    _currentAnimState = 0;
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
            EmoteType type = (EmoteType)emoteId;

            // 1. Overhead 3D Emoji
            if (overheadUI != null)
            {
                overheadUI.ShowEmoji(type);
            }

            // 2. Audio & Particle VFX
            if (_emoteEffects != null)
            {
                _emoteEffects.PlayEmoteFeedback(type);
            }

            // 3. Procedural Body Expression
            if (_emoteAnimCoroutine != null)
            {
                StopCoroutine(_emoteAnimCoroutine);
            }
            _emoteAnimCoroutine = StartCoroutine(PlayEmoteAnimationRoutine(type));

            // 4. Send to server
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                NetworkManager.Instance.SendEmote(emoteId);
            }
        }

        private IEnumerator PlayEmoteAnimationRoutine(EmoteType type)
        {
            if (visualTransform == null) yield break;

            float duration = 2.0f;
            float elapsed = 0f;
            Vector3 origPos = Vector3.zero;
            Quaternion origRot = Quaternion.identity;
            Vector3 origScale = Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                switch (type)
                {
                    case EmoteType.Wave:
                    case EmoteType.HighFive:
                        // Enthusiastic lean right and wave side-to-side
                        float wave = Mathf.Sin(elapsed * 18f) * 16f * (1f - progress * 0.4f);
                        visualTransform.localRotation = Quaternion.Euler(0, 0, -wave);
                        visualTransform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(elapsed * 9f)) * 0.1f, 0);
                        break;

                    case EmoteType.Heart:
                        // Heartbeat pulse animation
                        float beat = 1f + Mathf.Max(0, Mathf.Sin(elapsed * 14f)) * 0.28f * (1f - progress * 0.35f);
                        visualTransform.localScale = new Vector3(beat, beat * 1.05f, beat);
                        visualTransform.localPosition = new Vector3(0, (beat - 1f) * 0.2f, 0);
                        break;

                    case EmoteType.Clap:
                        // Energetic small hops with micro vibration
                        float hop = Mathf.Abs(Mathf.Sin(elapsed * 24f)) * 0.12f;
                        float shudder = Mathf.Sin(elapsed * 48f) * 4f;
                        visualTransform.localPosition = new Vector3(0, hop, 0);
                        visualTransform.localRotation = Quaternion.Euler(0, shudder, 0);
                        break;

                    case EmoteType.Bow:
                        // Respectful forward tilt and hold
                        float bowAngle = Mathf.Sin(Mathf.Clamp01(progress * 1.5f) * Mathf.PI) * 32f;
                        visualTransform.localRotation = Quaternion.Euler(bowAngle, 0, 0);
                        visualTransform.localPosition = new Vector3(0, -Mathf.Sin(bowAngle * Mathf.Deg2Rad) * 0.15f, 0);
                        break;

                    case EmoteType.Dance:
                        // Joyful 360 spin and bounce
                        float spin = elapsed * 420f;
                        float danceHop = Mathf.Abs(Mathf.Sin(elapsed * 16f)) * 0.2f;
                        visualTransform.localRotation = Quaternion.Euler(0, spin, Mathf.Sin(elapsed * 12f) * 10f);
                        visualTransform.localPosition = new Vector3(0, danceHop, 0);
                        break;
                }

                yield return null;
            }

            ResetVisualTransform();
            _currentAnimState = 0; // Return to idle
            _emoteAnimCoroutine = null;
        }

        private void ResetVisualTransform()
        {
            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.zero;
                visualTransform.localRotation = Quaternion.identity;
                visualTransform.localScale = Vector3.one;
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
