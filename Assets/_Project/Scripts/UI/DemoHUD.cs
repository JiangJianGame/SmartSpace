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
        private const int MaxLogs = 7;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _emoteBtnStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized = false;

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnChatMessageReceived += HandleChatMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived += HandlePlayerEmote;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived -= HandlePlayerEmote;
            }
        }

        private void HandleChatMessage(string senderId, string username, string message)
        {
            string log = $"<b>[{username}]</b>: {message}";
            AddLog(log);
        }

        private void HandlePlayerEmote(string senderId, string username, EmoteType emoteType)
        {
            string log = $"<color=#80D8FF><b>[{username}]</b></color> <color=#FFD54F>{EmoteHelper.GetChatText(emoteType)}</color>";
            AddLog(log);
        }

        private void AddLog(string log)
        {
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

            _emoteBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
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
            GUILayout.BeginArea(new Rect(20, 20, 340, 165), GUI.skin.box);
            GUILayout.Label("<size=15><b>智慧空间 · 社交与漫游系统</b></size>", _labelStyle);
            GUILayout.Space(3);

            string statusColor = isConnected ? "#00E676" : "#FF5252";
            string statusText = isConnected ? "已连接" : "未连接";
            GUILayout.Label($"服务器状态: <color={statusColor}><b>{statusText}</b></color> (ws://localhost:2567)", _labelStyle);
            GUILayout.Label($"会话 ID: <color=#80D8FF>{sessionId}</color>", _labelStyle);
            GUILayout.Label($"空间在线人数: <color=#FFD54F><b>{playerCount}</b> 人</color>", _labelStyle);

            GUILayout.Space(3);
            GUILayout.Label("<color=#B0BEC5>操作提示: WASD移动 | Shift疾跑 | 空格跳跃\n按住 <b>[T]</b> 开启动作轮盘 | 数字键 <b>1~6</b> 快捷表情</color>", _labelStyle);
            GUILayout.EndArea();

            // 2. Bottom-Left Chat & Interaction Panel
            float panelWidth = 410;
            float panelHeight = 295;
            float panelY = Screen.height - panelHeight - 20;

            GUILayout.BeginArea(new Rect(20, panelY, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label("<b>空间交流 & 社交动作</b>", _labelStyle);

            // Chat Log display
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(105));
            if (_chatLogs.Count == 0)
            {
                GUILayout.Label("<color=#78909C>暂无交流动态，可输入文字或使用下方动作打招呼...</color>", _labelStyle);
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

            // Quick Emote Action Buttons - Row 1
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👋 挥手 [1]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.Wave);
            if (GUILayout.Button("💖 比心 [2]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.Heart);
            if (GUILayout.Button("👏 鼓掌 [3]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.Clap);
            GUILayout.EndHorizontal();

            // Quick Emote Action Buttons - Row 2
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🙇 鞠躬 [4]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.Bow);
            if (GUILayout.Button("🕺 跳舞 [5]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.Dance);
            if (GUILayout.Button("✋ 击掌 [6]", _emoteBtnStyle, GUILayout.Height(26))) TriggerLocalEmote(EmoteType.HighFive);
            GUILayout.EndHorizontal();

            // Utility buttons - Row 3
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎡 动作轮盘 (Hold T)", _btnStyle, GUILayout.Height(26)))
            {
                if (EmoteWheelUI.Instance != null)
                {
                    EmoteWheelUI.Instance.OpenWheel();
                }
            }
            if (GUILayout.Button("🤖 生成测试访客 (网络Bot)", _btnStyle, GUILayout.Height(26)))
            {
                if (NetworkManager.Instance != null && isConnected)
                {
                    NetworkManager.Instance.SpawnNetworkBot();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void TriggerLocalEmote(EmoteType type)
        {
            if (EmoteWheelUI.Instance != null)
            {
                EmoteWheelUI.Instance.TriggerEmote(type);
            }
            else
            {
                var local = FindObjectOfType<LocalPlayerController>();
                if (local != null)
                {
                    local.TriggerEmote((sbyte)type);
                }
            }
        }
    }
}
