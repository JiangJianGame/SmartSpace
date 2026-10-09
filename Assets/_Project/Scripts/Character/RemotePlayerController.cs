using System.Collections;
using UnityEngine;
using SmartSpace.Network;
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

        private EmoteEffects _emoteEffects;
        private Coroutine _emoteAnimCoroutine;

        public string SessionId { get; private set; }
        public string Username { get; private set; }
        public int AvatarId { get; private set; }

        private void Awake()
        {
            _targetPosition = transform.position;
            _targetRotationY = transform.eulerAngles.y;

            if (visualRoot == null)
            {
                Transform v = transform.Find("Visual");
                visualRoot = v != null ? v : transform;
            }

            _emoteEffects = gameObject.GetComponent<EmoteEffects>();
            if (_emoteEffects == null)
            {
                _emoteEffects = gameObject.AddComponent<EmoteEffects>();
            }
        }

        public void Initialize(Player playerSchema)
        {
            SessionId = playerSchema.id;
            Username = playerSchema.username;
            AvatarId = playerSchema.avatarId;

            Vector3 initialPos = new Vector3((float)playerSchema.x, (float)playerSchema.y, (float)playerSchema.z);
            transform.position = initialPos;
            _targetPosition = initialPos;
            _targetRotationY = (float)playerSchema.rotY;
            transform.rotation = Quaternion.Euler(0, _targetRotationY, 0);

            if (overheadUI != null)
            {
                overheadUI.SetUsername(Username, false);
            }

            ApplyAvatarVisual(AvatarId);
        }

        public void ApplyAvatarVisual(int avatarIndex)
        {
            if (visualRoot != null)
            {
                var body = visualRoot.Find("Body");
                if (body != null)
                {
                    var ren = body.GetComponent<MeshRenderer>();
                    if (ren != null)
                    {
                        Color[] costumeColors = new Color[]
                        {
                            new Color(1.0f, 0.85f, 0.2f),  // 0: 虎龙誓印 (Gold)
                            new Color(1.0f, 0.2f, 0.25f),  // 1: 新春风旅人 (Red)
                            new Color(0.95f, 0.95f, 0.98f),// 2: 单身东京狗 (White)
                            new Color(0.0f, 0.9f, 0.85f),  // 3: 森罗灵弓 (Cyan)
                            new Color(1.0f, 0.4f, 0.7f),   // 4: 周年卫衣 (Pink)
                            new Color(0.95f, 0.75f, 0.2f), // 5: 梦中花海 (Yellow)
                            new Color(0.5f, 0.25f, 0.95f), // 6: 周年庆典 (Purple)
                            new Color(0.25f, 0.6f, 0.95f), // 7: 经典蓝灰 (Blue)
                            new Color(1.0f, 0.25f, 0.15f), // 8: 烈焰战甲 (Flame)
                            new Color(0.15f, 0.85f, 0.45f),// 9: 灵溪法袍 (Emerald)
                            new Color(0.45f, 0.15f, 0.95f),// 10: 暗夜行者 (Dark Violet)
                            new Color(1.0f, 0.85f, 0.15f)  // 11: 黄金神圣 (Holy Gold)
                        };
                        int safeIdx = Mathf.Clamp(avatarIndex, 0, costumeColors.Length - 1);
                        ren.material.color = costumeColors[safeIdx];
                    }
                }
            }
        }

        public void UpdateFromSchema(Player playerSchema)
        {
            if (Username != playerSchema.username)
            {
                Username = playerSchema.username;
                if (overheadUI != null)
                {
                    overheadUI.SetUsername(Username, false);
                }
            }
            if (AvatarId != playerSchema.avatarId)
            {
                AvatarId = playerSchema.avatarId;
                ApplyAvatarVisual(AvatarId);
            }

            Vector3 newPos = new Vector3((float)playerSchema.x, (float)playerSchema.y, (float)playerSchema.z);

            // Teleport / snap if distance is too large
            if (Vector3.Distance(transform.position, newPos) > snapDistance)
            {
                transform.position = newPos;
            }

            _targetPosition = newPos;
            _targetRotationY = (float)playerSchema.rotY;

            // Check if remote player triggered an emote (animState >= 10)
            if (playerSchema.animState >= 10 && playerSchema.animState != _currentAnimState)
            {
                TriggerRemoteEmote((EmoteType)playerSchema.animState);
            }

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

        public void TriggerRemoteEmote(EmoteType type)
        {
            if (overheadUI != null)
            {
                overheadUI.ShowEmoji(type);
            }

            if (_emoteEffects != null)
            {
                _emoteEffects.PlayEmoteFeedback(type);
            }

            if (_emoteAnimCoroutine != null)
            {
                StopCoroutine(_emoteAnimCoroutine);
            }
            _emoteAnimCoroutine = StartCoroutine(PlayEmoteAnimationRoutine(type));
        }

        private IEnumerator PlayEmoteAnimationRoutine(EmoteType type)
        {
            if (visualRoot == null) yield break;

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
                        float wave = Mathf.Sin(elapsed * 18f) * 16f * (1f - progress * 0.4f);
                        visualRoot.localRotation = Quaternion.Euler(0, 0, -wave);
                        visualRoot.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(elapsed * 9f)) * 0.1f, 0);
                        break;

                    case EmoteType.Heart:
                        float beat = 1f + Mathf.Max(0, Mathf.Sin(elapsed * 14f)) * 0.28f * (1f - progress * 0.35f);
                        visualRoot.localScale = new Vector3(beat, beat * 1.05f, beat);
                        visualRoot.localPosition = new Vector3(0, (beat - 1f) * 0.2f, 0);
                        break;

                    case EmoteType.Clap:
                        float hop = Mathf.Abs(Mathf.Sin(elapsed * 24f)) * 0.12f;
                        float shudder = Mathf.Sin(elapsed * 48f) * 4f;
                        visualRoot.localPosition = new Vector3(0, hop, 0);
                        visualRoot.localRotation = Quaternion.Euler(0, shudder, 0);
                        break;

                    case EmoteType.Bow:
                        float bowAngle = Mathf.Sin(Mathf.Clamp01(progress * 1.5f) * Mathf.PI) * 32f;
                        visualRoot.localRotation = Quaternion.Euler(bowAngle, 0, 0);
                        visualRoot.localPosition = new Vector3(0, -Mathf.Sin(bowAngle * Mathf.Deg2Rad) * 0.15f, 0);
                        break;

                    case EmoteType.Dance:
                        float spin = elapsed * 420f;
                        float danceHop = Mathf.Abs(Mathf.Sin(elapsed * 16f)) * 0.2f;
                        visualRoot.localRotation = Quaternion.Euler(0, spin, Mathf.Sin(elapsed * 12f) * 10f);
                        visualRoot.localPosition = new Vector3(0, danceHop, 0);
                        break;
                }

                yield return null;
            }

            ResetVisualTransform();
            _emoteAnimCoroutine = null;
        }

        private void ResetVisualTransform()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            // Smoothly interpolate position (Dead reckoning / Lerp)
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * positionLerpSpeed);

            // Smoothly interpolate rotation
            Quaternion targetRot = Quaternion.Euler(0, _targetRotationY, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

            // Update procedural motion for walk/run if not currently performing an emote
            if (_emoteAnimCoroutine == null)
            {
                UpdateProceduralVisuals();
            }
        }

        private void UpdateProceduralVisuals()
        {
            if (visualRoot == null) return;

            // Simple procedural animation depending on state (Walk=1, Run=2)
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
            else if (_currentAnimState < 10)
            {
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, Time.deltaTime * 10f);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one, Time.deltaTime * 10f);
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
