using System.Collections;
using UnityEngine;
using TMPro;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    public class PlayerOverheadUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text chatBubbleText;
        [SerializeField] private GameObject chatBubbleRoot;

        [Header("Settings")]
        [SerializeField] private float chatDisplayDuration = 4.0f;
        [SerializeField] private float emojiDisplayDuration = 2.5f;

        private Coroutine _hideChatCoroutine;
        private Coroutine _emojiAnimCoroutine;
        private Transform _cameraTransform;

        private GameObject _emojiRoot;
        private TMP_Text _emojiText;

        private void Start()
        {
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (chatBubbleRoot != null)
            {
                chatBubbleRoot.SetActive(false);
            }

            EnsureEmojiRoot();
        }

        private void EnsureEmojiRoot()
        {
            if (_emojiRoot != null) return;

            Transform existing = transform.Find("EmojiBadge");
            if (existing != null)
            {
                _emojiRoot = existing.gameObject;
                _emojiText = _emojiRoot.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                _emojiRoot = new GameObject("EmojiBadge");
                _emojiRoot.transform.SetParent(transform, false);
                _emojiRoot.transform.localPosition = new Vector3(0, 0.55f, 0);

                var tmp = _emojiRoot.AddComponent<TextMeshPro>();
                tmp.fontSize = 2.8f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.text = "[ HELLO ]";
                tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
                _emojiText = tmp;
            }

            _emojiRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                // Face the camera directly (Billboard effect)
                transform.rotation = _cameraTransform.rotation;
            }
        }

        public void SetUsername(string username, bool isLocalPlayer)
        {
            if (nameText != null)
            {
                nameText.text = isLocalPlayer ? $"<color=#00E5FF>[YOU] {username}</color>" : $"<color=#FFD54F>{username}</color>";
            }
        }

        public void ShowChat(string message)
        {
            if (string.IsNullOrEmpty(message)) return;


            if (chatBubbleText != null)
            {
                chatBubbleText.text = message;
            }

            if (chatBubbleRoot != null)
            {
                chatBubbleRoot.SetActive(true);
            }

            if (_hideChatCoroutine != null)
            {
                StopCoroutine(_hideChatCoroutine);
            }
            _hideChatCoroutine = StartCoroutine(HideChatRoutine());
        }

        public void ShowEmoji(EmoteType type)
        {
            EnsureEmojiRoot();
            if (_emojiRoot == null || _emojiText == null) return;

            _emojiText.text = EmoteHelper.GetBadgeText(type);

            if (_emojiAnimCoroutine != null)
            {
                StopCoroutine(_emojiAnimCoroutine);
            }
            _emojiAnimCoroutine = StartCoroutine(AnimateEmojiBadge());
        }

        private IEnumerator AnimateEmojiBadge()
        {
            _emojiRoot.SetActive(true);
            Vector3 basePos = new Vector3(0, 0.55f, 0);
            _emojiRoot.transform.localPosition = basePos;
            _emojiRoot.transform.localScale = Vector3.zero;

            // 1. Elastic pop in (0 to 1.3 to 1.0)
            float popDuration = 0.25f;
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                // Overshoot bounce
                float s = Mathf.Sin(t * Mathf.PI * 0.75f) * 1.35f;
                _emojiRoot.transform.localScale = Vector3.one * s;
                yield return null;
            }
            _emojiRoot.transform.localScale = Vector3.one;

            // 2. Gentle float up
            float holdTime = emojiDisplayDuration - 0.5f;
            elapsed = 0f;
            while (elapsed < holdTime)
            {
                elapsed += Time.deltaTime;
                float wobble = Mathf.Sin(elapsed * 10f) * 0.08f;
                _emojiRoot.transform.localPosition = basePos + new Vector3(wobble, (elapsed / holdTime) * 0.4f, 0);
                yield return null;
            }

            // 3. Shrink & fade out
            float fadeTime = 0.25f;
            elapsed = 0f;
            Vector3 startScale = _emojiRoot.transform.localScale;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / fadeTime);
                _emojiRoot.transform.localScale = startScale * t;
                yield return null;
            }

            _emojiRoot.SetActive(false);
            _emojiAnimCoroutine = null;
        }

        private IEnumerator HideChatRoutine()
        {
            yield return new WaitForSeconds(chatDisplayDuration);
            if (chatBubbleRoot != null)
            {
                chatBubbleRoot.SetActive(false);
            }
            _hideChatCoroutine = null;
        }
    }
}
