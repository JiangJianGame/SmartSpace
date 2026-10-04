using System.Collections;
using UnityEngine;
using TMPro;

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
        [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

        private Coroutine _hideChatCoroutine;
        private Transform _cameraTransform;

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
