using System;
using UnityEngine;
using SmartSpace.Character;
using SmartSpace.Network;

namespace SmartSpace.UI
{
    public class EmoteWheelUI : MonoBehaviour
    {
        public static EmoteWheelUI Instance { get; private set; }

        [Header("Wheel Settings")]
        [SerializeField] private KeyCode wheelKey = KeyCode.T;
        [SerializeField] private float wheelRadius = 175f;
        [SerializeField] private float itemSize = 75f;

        private bool _isOpen = false;
        private int _selectedSector = -1;

        private readonly EmoteType[] _emotes = new EmoteType[]
        {
            EmoteType.Wave,     // 0: Top (90 deg)
            EmoteType.Heart,    // 1: Top-Right (30 deg)
            EmoteType.Clap,     // 2: Bottom-Right (-30 deg)
            EmoteType.Bow,      // 3: Bottom (-90 deg)
            EmoteType.Dance,    // 4: Bottom-Left (-150 deg)
            EmoteType.HighFive  // 5: Top-Left (150 deg)
        };

        private GUIStyle _centerStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _itemHoverStyle;
        private GUIStyle _subStyle;
        private bool _stylesReady = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Hold T key to open wheel, release to trigger
            if (Input.GetKeyDown(wheelKey))
            {
                OpenWheel();
            }
            else if (Input.GetKeyUp(wheelKey) && _isOpen)
            {
                ExecuteAndClose();
            }

            // Number key shortcuts 1~6 for quick emotes
            if (!_isOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) TriggerEmote(EmoteType.Wave);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) TriggerEmote(EmoteType.Heart);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) TriggerEmote(EmoteType.Clap);
                else if (Input.GetKeyDown(KeyCode.Alpha4)) TriggerEmote(EmoteType.Bow);
                else if (Input.GetKeyDown(KeyCode.Alpha5)) TriggerEmote(EmoteType.Dance);
                else if (Input.GetKeyDown(KeyCode.Alpha6)) TriggerEmote(EmoteType.HighFive);
            }
        }

        public void OpenWheel()
        {
            _isOpen = true;
            _selectedSector = -1;
        }

        public void CloseWheel()
        {
            _isOpen = false;
            _selectedSector = -1;
        }

        private void ExecuteAndClose()
        {
            if (_selectedSector >= 0 && _selectedSector < _emotes.Length)
            {
                TriggerEmote(_emotes[_selectedSector]);
            }
            CloseWheel();
        }

        public void TriggerEmote(EmoteType type)
        {
            var localPlayer = FindObjectOfType<LocalPlayerController>();
            if (localPlayer != null)
            {
                localPlayer.TriggerEmote((sbyte)type);
            }

            // Send chat notification
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                string chatText = EmoteHelper.GetChatText(type);
                NetworkManager.Instance.SendChat(chatText);
            }
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _centerStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            _itemStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            _itemHoverStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                richText = true
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            InitStyles();

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 delta = mousePos - center;
            float dist = delta.magnitude;

            // Compute hovered sector
            if (dist > 30f)
            {
                // Invert Y because GUI Y goes downwards
                float angleRad = Mathf.Atan2(-delta.y, delta.x); // [-PI, PI], 0 = right
                float angleDeg = angleRad * Mathf.Rad2Deg;
                if (angleDeg < 0) angleDeg += 360f; // [0, 360]

                // Sectors arranged clockwise:
                // 0: Wave (90 +/- 30) => [60, 120]
                // 1: Heart (30 +/- 30) => [0, 60]
                // 2: Clap (-30 => 330 +/- 30) => [300, 360]
                // 3: Bow (-90 => 270 +/- 30) => [240, 300]
                // 4: Dance (-150 => 210 +/- 30) => [180, 240]
                // 5: HighFive (150 +/- 30) => [120, 180]

                if (angleDeg >= 60f && angleDeg < 120f) _selectedSector = 0;
                else if (angleDeg >= 0f && angleDeg < 60f) _selectedSector = 1;
                else if (angleDeg >= 300f && angleDeg < 360f) _selectedSector = 2;
                else if (angleDeg >= 240f && angleDeg < 300f) _selectedSector = 3;
                else if (angleDeg >= 180f && angleDeg < 240f) _selectedSector = 4;
                else if (angleDeg >= 120f && angleDeg < 180f) _selectedSector = 5;
            }
            else
            {
                _selectedSector = -1;
            }

            // Draw Dark Modal Background Backdrop
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Draw Radial Emote Items
            float[] sectorAngles = new float[] { 90f, 30f, -30f, -90f, -150f, 150f };

            for (int i = 0; i < _emotes.Length; i++)
            {
                float rad = sectorAngles[i] * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(rad) * wheelRadius - itemSize * 0.5f;
                float y = center.y - Mathf.Sin(rad) * wheelRadius - itemSize * 0.5f; // Invert Y

                bool isSelected = (_selectedSector == i);
                Rect itemRect = new Rect(x, y, itemSize, itemSize);

                string emoji = EmoteHelper.GetEmoji(_emotes[i]);
                string name = EmoteHelper.GetName(_emotes[i]);

                if (isSelected)
                {
                    // Scaled up highlight box
                    Rect highlightRect = new Rect(x - 6, y - 6, itemSize + 12, itemSize + 12);
                    GUI.backgroundColor = new Color(0f, 0.9f, 1f, 1f);
                    GUI.Box(highlightRect, $"<size=22>{emoji}</size>\n<b>{name}</b>", _itemHoverStyle);
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Box(itemRect, $"<size=18>{emoji}</size>\n<color=#ECEFF1>{name}</color>", _itemStyle);
                }
            }

            // Draw Center Card
            float centerW = 160f;
            float centerH = 90f;
            Rect centerRect = new Rect(center.x - centerW * 0.5f, center.y - centerH * 0.5f, centerW, centerH);

            if (_selectedSector >= 0 && _selectedSector < _emotes.Length)
            {
                EmoteType sel = _emotes[_selectedSector];
                string emoji = EmoteHelper.GetEmoji(sel);
                string name = EmoteHelper.GetName(sel);
                GUI.Box(centerRect, $"<size=24>{emoji}</size>\n<color=#00E5FF>{name}</color>", _centerStyle);
            }
            else
            {
                GUI.Box(centerRect, "<color=#90A4AE>动作轮盘</color>\n<size=11><color=#CFD8DC>滑动鼠标选择\n松开 [T] 执行</color></size>", _centerStyle);
            }
        }
    }
}
