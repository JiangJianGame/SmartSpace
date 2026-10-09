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
        [SerializeField] private float chatDisplayDuration = 6.0f;
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

        public void SetVisible(bool visible)
        {
            if (nameText != null) nameText.gameObject.SetActive(visible);
            if (chatBubbleRoot != null && !visible) chatBubbleRoot.SetActive(false);
            if (_emojiRoot != null && !visible) _emojiRoot.SetActive(false);
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
                _emojiRoot.transform.localPosition = new Vector3(0, 0.75f, 0);

                var tmp = _emojiRoot.AddComponent<TextMeshPro>();
                if (nameText != null)
                {
                    tmp.font = nameText.font;
                    tmp.fontSharedMaterial = nameText.fontSharedMaterial;
                }
                tmp.fontSize = 4.6f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.text = "[ HELLO ]";
                tmp.rectTransform.sizeDelta = new Vector2(8.0f, 3.0f);
                _emojiText = tmp;
            }

            _emojiRoot.SetActive(false);
        }

        public void Show2DEmote(string emoteName, string emoteSymbol)
        {
            EnsureEmojiRoot();
            if (_emojiRoot == null || _emojiText == null) return;

            if (nameText != null)
            {
                _emojiText.font = nameText.font;
                _emojiText.fontSharedMaterial = nameText.fontSharedMaterial;
            }
            _emojiText.fontSize = 4.0f;
            _emojiText.rectTransform.sizeDelta = new Vector2(8.0f, 3.0f);

            string kaomoji = emoteName switch
            {
                "伊乐·微笑" => "(*^▽^*)",
                "伊乐·大哭" => "( ╥﹏╥ )",
                "伊乐·傲娇" => "( ￣^￣ )",
                "伊乐·害羞" => "(*/ω＼*)",
                "伊乐·委屈" => "(っ˘̩╭╮˘̩)っ",
                "伊乐·比心" => "(づ￣ ³￣)づ♥",
                _ => emoteSymbol
            };

            _emojiText.text = $"<size=125%><color=#FFE082><b>{kaomoji}</b></color></size>\n<size=70%><color=#00E5FF><b>【{emoteName}】</b></color></size>";

            if (_emojiAnimCoroutine != null)
            {
                StopCoroutine(_emojiAnimCoroutine);
            }
            _emojiAnimCoroutine = StartCoroutine(AnimateEmojiBadge());
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

        private string _cachedUsername = "";
        private string _cachedTitle = "";
        private bool _isLocalPlayer = false;

        public void SetUsernameAndTitle(string username, string title, bool isLocalPlayer)
        {
            _cachedUsername = username;
            _cachedTitle = title;
            _isLocalPlayer = isLocalPlayer;
            UpdateNameText();
        }

        public void SetUsername(string username, bool isLocalPlayer)
        {
            _cachedUsername = username;
            _isLocalPlayer = isLocalPlayer;
            UpdateNameText();
        }

        public void SetTitle(string title)
        {
            _cachedTitle = title;
            UpdateNameText();
        }

        private static string FormatTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            title = title.Trim();
            if (title.StartsWith("【") && title.EndsWith("】")) return title;
            return $"【{title}】";
        }

        private void UpdateNameText()
        {
            if (nameText == null) return;
            string formattedTitle = FormatTitle(_cachedTitle);
            string titlePrefix = !string.IsNullOrEmpty(formattedTitle) ? $"<size=75%><color=#FFD54F><b>{formattedTitle}</b></color></size>\n" : "";
            string namePart = _isLocalPlayer ? $"<color=#00E5FF>[YOU] {_cachedUsername}</color>" : $"<color=#FFD54F>{_cachedUsername}</color>";
            nameText.text = titlePrefix + namePart;
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
            Vector3 basePos = new Vector3(0, 0.75f, 0);
            _emojiRoot.transform.localPosition = basePos;
            _emojiRoot.transform.localScale = Vector3.one * 0.7f;

            // 1. Elastic pop in (0.7 to 1.25 to 1.0)
            float popDuration = 0.25f;
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                float s = Mathf.Lerp(0.7f, 1.25f, Mathf.Sin(t * Mathf.PI * 0.5f));
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
                _emojiRoot.transform.localPosition = basePos + new Vector3(wobble, (elapsed / holdTime) * 0.08f, 0);
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
