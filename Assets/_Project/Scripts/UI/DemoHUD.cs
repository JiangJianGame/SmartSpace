using System.Collections.Generic;
using UnityEngine;
using SmartSpace.Network;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    public class DemoHUD : MonoBehaviour
    {
        private string _inputChat = "";
        private readonly List<string> _chatLogs = new List<string>();
        private const int MaxLogs = 6;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized = false;

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnChatMessageReceived += HandleChatMessage;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
            }
        }

        private void HandleChatMessage(string senderId, string username, string message)
        {
            string log = $"<b>[{username}]</b>: {message}";
            _chatLogs.Add(log);
            if (_chatLogs.Count > MaxLogs)
            {
                _chatLogs.RemoveAt(0);
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            bool isConnected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            string sessionId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "无";
            int playerCount = NetworkManager.Instance != null ? NetworkManager.Instance.PlayerCount : 0;

            // 1. Top-Left Status Panel
            GUILayout.BeginArea(new Rect(20, 20, 320, 160), GUI.skin.box);
            GUILayout.Label("<size=15><b>智慧空间 · 网络同步测试 Demo</b></size>", _labelStyle);
            GUILayout.Space(4);

            string statusColor = isConnected ? "#00E676" : "#FF5252";
            string statusText = isConnected ? "已连接" : "未连接";
            GUILayout.Label($"服务器状态: <color={statusColor}><b>{statusText}</b></color> (ws://localhost:2567)", _labelStyle);
            GUILayout.Label($"会话 ID: <color=#80D8FF>{sessionId}</color>", _labelStyle);
            GUILayout.Label($"空间在线人数: <color=#FFD54F><b>{playerCount}</b> 人</color>", _labelStyle);

            GUILayout.Space(4);
            GUILayout.Label("<color=#B0BEC5>操作提示: WASD移动 | Shift疾跑 | 空格跳跃\n鼠标右键按住旋转视角 | 1键打招呼</color>", _labelStyle);
            GUILayout.EndArea();

            // 2. Bottom-Left Chat & Interaction Panel
            float panelWidth = 380;
            float panelHeight = 255;
            float panelY = Screen.height - panelHeight - 20;

            GUILayout.BeginArea(new Rect(20, panelY, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label("<b>空间交流 & 动作</b>", _labelStyle);

            // Chat Log display
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(100));
            if (_chatLogs.Count == 0)
            {
                GUILayout.Label("<color=#78909C>暂无发言记录，可在下方输入发送...</color>", _labelStyle);
            }
            else
            {
                foreach (var log in _chatLogs)
                {
                    GUILayout.Label(log, _labelStyle);
                }
            }
            GUILayout.EndVertical();

            // Chat input row
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("ChatInputField");
            _inputChat = GUILayout.TextField(_inputChat, _inputStyle, GUILayout.Height(28));

            if ((GUILayout.Button("发送", _btnStyle, GUILayout.Width(60), GUILayout.Height(28)) ||
                (Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "ChatInputField"))
                && !string.IsNullOrEmpty(_inputChat.Trim()))
            {
                if (NetworkManager.Instance != null && isConnected)
                {
                    NetworkManager.Instance.SendChat(_inputChat.Trim());
                    _inputChat = "";
                    GUI.FocusControl(null); // Unfocus
                }
            }
            GUILayout.EndHorizontal();

            // Emote action buttons
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👋 打招呼 (Wave)", _btnStyle, GUILayout.Height(26)))
            {
                TriggerLocalEmote(4);
            }
            if (GUILayout.Button("🎉 欢呼 (Cheer)", _btnStyle, GUILayout.Height(26)))
            {
                TriggerLocalEmote(3);
            }
            GUILayout.EndHorizontal();

            // Bot spawn button
            GUILayout.Space(4);
            if (GUILayout.Button("🤖 生成测试访客 (网络Bot联机)", _btnStyle, GUILayout.Height(26)))
            {
                if (NetworkManager.Instance != null && isConnected)
                {
                    NetworkManager.Instance.SpawnNetworkBot();
                }
            }

            GUILayout.EndArea();
        }

        private void Update()
        {
            // Shortcut key 1 for wave emote
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                TriggerLocalEmote(4);
            }
        }

        private void TriggerLocalEmote(sbyte emoteId)
        {
            var local = FindObjectOfType<LocalPlayerController>();
            if (local != null)
            {
                local.TriggerEmote(emoteId);
                string emoteName = emoteId == 4 ? "做了一个打招呼动作 👋" : "欢呼跃起 🎉";
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SendChat(emoteName);
                }
            }
        }
    }
}
