using UnityEngine;
using SmartSpace.Network.Schema;
using SmartSpace.UI;

namespace SmartSpace.Character
{
    public class RemotePlayerController : MonoBehaviour
    {
        [Header("Interpolation Settings")]
        [SerializeField] private float positionLerpSpeed = 15.0f;
        [SerializeField] private float rotationLerpSpeed = 15.0f;
        [SerializeField] private float snapDistance = 5.0f;

        [Header("References")]
        [SerializeField] private PlayerOverheadUI overheadUI;
        [SerializeField] private Transform visualRoot;

        private Vector3 _targetPosition;
        private float _targetRotationY;
        private sbyte _currentAnimState;
        private string _lastChatMsg = "";
        private double _lastChatTime = 0;

        public string SessionId { get; private set; }
        public string Username { get; private set; }

        private void Awake()
        {
            _targetPosition = transform.position;
            _targetRotationY = transform.eulerAngles.y;
        }

        public void Initialize(Player playerSchema)
        {
            SessionId = playerSchema.id;
            Username = playerSchema.username;

            Vector3 initialPos = new Vector3((float)playerSchema.x, (float)playerSchema.y, (float)playerSchema.z);
            transform.position = initialPos;
            _targetPosition = initialPos;
            _targetRotationY = (float)playerSchema.rotY;
            transform.rotation = Quaternion.Euler(0, _targetRotationY, 0);

            if (overheadUI != null)
            {
                overheadUI.SetUsername(Username, false);
            }
        }

        public void UpdateFromSchema(Player playerSchema)
        {
            Vector3 newPos = new Vector3((float)playerSchema.x, (float)playerSchema.y, (float)playerSchema.z);

            // Teleport / snap if distance is too large
            if (Vector3.Distance(transform.position, newPos) > snapDistance)
            {
                transform.position = newPos;
            }

            _targetPosition = newPos;
            _targetRotationY = (float)playerSchema.rotY;
            _currentAnimState = playerSchema.animState;

            // Check if there is a new chat message
            if (!string.IsNullOrEmpty(playerSchema.chatMsg) &&
                (playerSchema.chatMsg != _lastChatMsg || playerSchema.chatTime != _lastChatTime))
            {
                _lastChatMsg = playerSchema.chatMsg;
                _lastChatTime = playerSchema.chatTime;
                ShowChatBubble(_lastChatMsg);
            }
        }

        private void Update()
        {
            // Smoothly interpolate position (Dead reckoning / Lerp)
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * positionLerpSpeed);

            // Smoothly interpolate rotation
            Quaternion targetRot = Quaternion.Euler(0, _targetRotationY, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

            // Update procedural motion feedback if visualRoot exists
            UpdateProceduralVisuals();
        }

        private void UpdateProceduralVisuals()
        {
            if (visualRoot == null) return;

            // Simple procedural animation depending on state (Walk=1, Run=2, Jump=3, Wave=4)
            if (_currentAnimState == 1) // Walk
            {
                float bob = Mathf.Sin(Time.time * 10f) * 0.05f;
                visualRoot.localPosition = new Vector3(0, bob, 0);
            }
            else if (_currentAnimState == 2) // Run
            {
                float bob = Mathf.Sin(Time.time * 18f) * 0.1f;
                visualRoot.localPosition = new Vector3(0, bob, 0);
            }
            else if (_currentAnimState == 4) // Wave
            {
                float tilt = Mathf.Sin(Time.time * 12f) * 8f;
                visualRoot.localRotation = Quaternion.Euler(0, 0, tilt);
            }
            else
            {
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, Time.deltaTime * 10f);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
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
