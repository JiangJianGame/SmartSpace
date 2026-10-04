using System;
using System.Collections.Generic;
using UnityEngine;
using SmartSpace.Network;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    public enum ChatChannel
    {
        All = 0,     // 综合
        Plaza = 1,   // 广场 (玩家聊天)
        Emote = 2,   // 动作 (社交动作)
        System = 3   // 系统 (通知公告)
    }

    public class ChatMessageItem
    {
        public ChatChannel Channel;
        public string Sender;
        public string Content;
        public string TimeStr;
        public string FormattedText;
    }

    /// <summary>
    /// 奥拉星手游风格 空间社交与漫游 HUD
    /// - 左侧聊天窗口高度占屏幕 2/3
    /// - 支持一键收起/展开 (含迷你浮条预览与 [C] 快捷键)
    /// - 包含 [综合 / 广场 / 动作 / 系统] 四大频道切换
    /// - 动作快捷栏与轮盘呼出集成
    /// </summary>
    public class DemoHUD : MonoBehaviour
    {
        [Header("Chat Settings")]
        [SerializeField] private bool defaultExpanded = true;
        [SerializeField] private int maxHistoryCount = 60;

        private bool _isExpanded = true;
        private ChatChannel _currentChannel = ChatChannel.All;
        private string _inputChat = "";
        private Vector2 _scrollPosition = Vector2.zero;
        private bool _shouldScrollToBottom = true;

        private readonly List<ChatMessageItem> _allMessages = new List<ChatMessageItem>();

        // Custom UI Styles & Textures
        private bool _stylesInitialized = false;
        private GUIStyle _panelStyle;
        private GUIStyle _headerTitleStyle;
        private GUIStyle _collapseBtnStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _tabInactiveStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _inputFieldStyle;
        private GUIStyle _sendBtnStyle;
        private GUIStyle _emoteBtnStyle;
        private GUIStyle _actionBtnStyle;
        private GUIStyle _miniBarStyle;
        private GUIStyle _statusPanelStyle;
        private GUIStyle _statusLabelStyle;

        private Texture2D _texPanelBg;
        private Texture2D _texHeaderBg;
        private Texture2D _texTabActive;
        private Texture2D _texTabInactive;
        private Texture2D _texSendBtn;
        private Texture2D _texEmoteBtn;
        private Texture2D _texEmoteBtnHover;
        private Texture2D _texInputBg;
        private Texture2D _texMiniBarBg;

        private void Awake()
        {
            _isExpanded = defaultExpanded;
        }

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged += HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived += HandleChatMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived += HandlePlayerEmote;
                NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
            }

            // Initial Welcome System Message
            AddSystemMessage("欢迎来到智慧空间广场！已载入奥拉通讯系统。");
            AddSystemMessage("操作提示: WASD移动 | Shift疾跑 | 空格跳跃 | T动作轮盘 | C收放聊天");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived -= HandlePlayerEmote;
                NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
            }
            CleanupTextures();
        }

        private void Update()
        {
            // Toggle Expand/Collapse with 'C' key when not focused in input
            if (Input.GetKeyDown(KeyCode.C) && GUI.GetNameOfFocusedControl() != "AolaChatInputField")
            {
                ToggleExpand();
            }
        }

        public void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            if (_isExpanded)
            {
                _shouldScrollToBottom = true;
            }
        }

        #region Network Event Handlers

        private void HandleConnectionStateChanged(bool connected)
        {
            if (connected)
            {
                AddSystemMessage("成功连接至空间服务器，随时可与同伴互动！");
            }
            else
            {
                AddSystemMessage("<color=#FF5252>已断开与空间服务器的连接。</color>");
            }
        }

        private void HandleChatMessage(string senderId, string username, string message)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string formatted = $"<color=#78909C>[{timeStr}]</color> <color=#FFD54F><b>[广场]</b></color> <color=#80D8FF><b>{username}</b></color>: {message}";

            AddMessage(new ChatMessageItem
            {
                Channel = ChatChannel.Plaza,
                Sender = username,
                Content = message,
                TimeStr = timeStr,
                FormattedText = formatted
            });
        }

        private void HandlePlayerEmote(string senderId, string username, EmoteType emoteType)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string actionText = EmoteHelper.GetChatText(emoteType);
            string formatted = $"<color=#78909C>[{timeStr}]</color> <color=#00E5FF><b>[动作]</b></color> <color=#80D8FF><b>{username}</b></color> <color=#FFE082>{actionText}</color>";

            AddMessage(new ChatMessageItem
            {
                Channel = ChatChannel.Emote,
                Sender = username,
                Content = actionText,
                TimeStr = timeStr,
                FormattedText = formatted
            });
        }

        private void HandlePlayerJoined(string sessionId, string username)
        {
            AddSystemMessage($"玩家 <color=#80D8FF><b>{username}</b></color> 进入了智慧空间广场！✨");
        }

        private void HandlePlayerLeft(string sessionId)
        {
            AddSystemMessage($"一名访客离开了空间。");
        }

        private void AddSystemMessage(string content)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string formatted = $"<color=#78909C>[{timeStr}]</color> <color=#76FF03><b>[系统]</b></color> <color=#CFD8DC>{content}</color>";

            AddMessage(new ChatMessageItem
            {
                Channel = ChatChannel.System,
                Sender = "系统",
                Content = content,
                TimeStr = timeStr,
                FormattedText = formatted
            });
        }

        private void AddMessage(ChatMessageItem item)
        {
            _allMessages.Add(item);
            if (_allMessages.Count > maxHistoryCount)
            {
                _allMessages.RemoveAt(0);
            }
            _shouldScrollToBottom = true;
        }

        #endregion

        #region Style & Texture Initialization

        private Texture2D MakeSolidTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void CleanupTextures()
        {
            if (_texPanelBg != null) Destroy(_texPanelBg);
            if (_texHeaderBg != null) Destroy(_texHeaderBg);
            if (_texTabActive != null) Destroy(_texTabActive);
            if (_texTabInactive != null) Destroy(_texTabInactive);
            if (_texSendBtn != null) Destroy(_texSendBtn);
            if (_texEmoteBtn != null) Destroy(_texEmoteBtn);
            if (_texEmoteBtnHover != null) Destroy(_texEmoteBtnHover);
            if (_texInputBg != null) Destroy(_texInputBg);
            if (_texMiniBarBg != null) Destroy(_texMiniBarBg);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // Generate textures with sci-fi tech colors
            _texPanelBg = MakeSolidTex(2, 2, new Color(0.05f, 0.08f, 0.14f, 0.88f)); // Deep Cosmic Navy
            _texHeaderBg = MakeSolidTex(2, 2, new Color(0.08f, 0.13f, 0.22f, 0.95f));
            _texTabActive = MakeSolidTex(2, 2, new Color(0.0f, 0.55f, 0.85f, 0.92f)); // Bright Cyan Accent
            _texTabInactive = MakeSolidTex(2, 2, new Color(0.08f, 0.12f, 0.20f, 0.75f));
            _texSendBtn = MakeSolidTex(2, 2, new Color(0.0f, 0.70f, 0.95f, 0.95f));
            _texEmoteBtn = MakeSolidTex(2, 2, new Color(0.09f, 0.15f, 0.25f, 0.85f));
            _texEmoteBtnHover = MakeSolidTex(2, 2, new Color(0.14f, 0.26f, 0.42f, 0.95f));
            _texInputBg = MakeSolidTex(2, 2, new Color(0.03f, 0.05f, 0.09f, 0.90f));
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.08f, 0.14f, 0.88f));

            // Main Panel Style
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texPanelBg },
                padding = new RectOffset(8, 8, 8, 8)
            };

            // Header Title
            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = Color.white }
            };

            // Collapse Button
            _collapseBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.85f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            // Active Tab
            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texTabActive }
            };

            // Inactive Tab
            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.8f, 0.9f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            // Message Label
            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(2, 2, 2, 2)
            };

            // Input Field
            _inputFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                normal = { textColor = Color.white, background = _texInputBg },
                padding = new RectOffset(6, 6, 4, 4)
            };

            // Send Button
            _sendBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texSendBtn }
            };

            // Emote Button
            _emoteBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.95f, 1f), background = _texEmoteBtn },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            // Action Utility Button
            _actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.9f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            // Mini Collapsed Bar
            _miniBarStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMiniBarBg },
                padding = new RectOffset(10, 10, 4, 4)
            };

            // Top Status Panel
            _statusPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texPanelBg },
                padding = new RectOffset(10, 10, 8, 8)
            };

            _statusLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                padding = new RectOffset(0, 0, 1, 1)
            };

            _stylesInitialized = true;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            InitStyles();

            // 1. Top-Left System Status Panel (Compact & High-Tech)
            DrawStatusPanel();

            // 2. Chat Panel (2/3 Height Expanded OR Mini Collapsed Bar)
            if (_isExpanded)
            {
                DrawExpandedChatPanel();
            }
            else
            {
                DrawCollapsedMiniBar();
            }
        }

        private void DrawStatusPanel()
        {
            bool isConnected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            string sessionId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "无";
            int playerCount = NetworkManager.Instance != null ? NetworkManager.Instance.PlayerCount : 0;

            float statusW = 340f;
            float statusH = 148f;
            GUILayout.BeginArea(new Rect(16, 16, statusW, statusH), _statusPanelStyle);

            GUILayout.Label("<size=14><b><color=#00E5FF>◆</color> 智慧空间 · 社交漫游系统</b></size>", _headerTitleStyle);
            GUILayout.Space(2);

            string statusColor = isConnected ? "#00E676" : "#FF5252";
            string statusText = isConnected ? "已连接" : "未连接";
            GUILayout.Label($"服务器: <color={statusColor}><b>● {statusText}</b></color> (ws://localhost:2567)", _statusLabelStyle);
            GUILayout.Label($"会话 ID: <color=#80D8FF>{sessionId}</color>", _statusLabelStyle);
            GUILayout.Label($"广场在线人数: <color=#FFD54F><b>{playerCount}</b> 人</color>", _statusLabelStyle);

            GUILayout.Space(2);
            GUILayout.Label("<color=#90A4AE>WASD移动 | Shift疾跑 | 空格跳跃 | <b>[T]</b>动作轮盘</color>", _statusLabelStyle);

            GUILayout.EndArea();
        }

        private void DrawExpandedChatPanel()
        {
            bool isConnected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;

            // Height is exactly 2/3 of screen height!
            float panelHeight = Mathf.Max(380f, Screen.height * (2f / 3f));
            float panelWidth = Mathf.Clamp(Screen.width * 0.28f, 380f, 430f);
            float panelX = 16f;
            float panelY = Screen.height - panelHeight - 16f;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _panelStyle);

            // A. Top Header Bar (Title & Collapse Button)
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Label("<size=13><b><color=#00E5FF>💬 空间频道</color></b> <color=#546E7A>COMMUNICATION</color></size>", _headerTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("◀ 收起 [C]", _collapseBtnStyle, GUILayout.Width(76), GUILayout.Height(24)))
            {
                ToggleExpand();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // B. Channel Tabs (综合 / 广场 / 动作 / 系统)
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            DrawChannelTab("综合", ChatChannel.All);
            DrawChannelTab("广场", ChatChannel.Plaza);
            DrawChannelTab("动作", ChatChannel.Emote);
            DrawChannelTab("系统", ChatChannel.System);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // C. Scrollable Message List (Fills available space)
            float upperH = 28 + 3 + 28 + 4;
            float lowerH = 68 + 32 + 20; // Emotes + Input + Margins
            float scrollH = panelHeight - upperH - lowerH - 16;

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.Height(scrollH)
            );

            int displayedCount = 0;
            foreach (var item in _allMessages)
            {
                if (_currentChannel == ChatChannel.All || item.Channel == _currentChannel)
                {
                    GUILayout.Label(item.FormattedText, _messageStyle);
                    displayedCount++;
                }
            }

            if (displayedCount == 0)
            {
                GUILayout.Label("<color=#78909C>当前频道暂无消息记录...</color>", _messageStyle);
            }

            if (_shouldScrollToBottom)
            {
                _scrollPosition.y = float.MaxValue;
                _shouldScrollToBottom = false;
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4);

            // D. Quick Emote Bar (奥拉星特色社交动作栏)
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👋 挥手 [1]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.Wave);
            if (GUILayout.Button("💖 比心 [2]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.Heart);
            if (GUILayout.Button("👏 鼓掌 [3]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.Clap);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🙇 鞠躬 [4]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.Bow);
            if (GUILayout.Button("🕺 跳舞 [5]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.Dance);
            if (GUILayout.Button("✋ 击掌 [6]", _emoteBtnStyle, GUILayout.Height(23))) TriggerLocalEmote(EmoteType.HighFive);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎡 动作轮盘 (Hold T)", _actionBtnStyle, GUILayout.Height(22)))
            {
                if (EmoteWheelUI.Instance != null) EmoteWheelUI.Instance.OpenWheel();
            }
            if (GUILayout.Button("🤖 召唤访客 (Bot)", _actionBtnStyle, GUILayout.Height(22)))
            {
                if (NetworkManager.Instance != null && isConnected) NetworkManager.Instance.SpawnNetworkBot();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // E. Input Bar (Text Field & Send Button)
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUI.SetNextControlName("AolaChatInputField");
            _inputChat = GUILayout.TextField(_inputChat, _inputFieldStyle, GUILayout.Height(26));

            bool pressEnter = Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "AolaChatInputField";
            if ((GUILayout.Button("发送", _sendBtnStyle, GUILayout.Width(58), GUILayout.Height(26)) || pressEnter)
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

            GUILayout.EndArea();
        }

        private void DrawChannelTab(string name, ChatChannel channel)
        {
            bool isActive = (_currentChannel == channel);
            GUIStyle style = isActive ? _tabActiveStyle : _tabInactiveStyle;
            string label = isActive ? $"<b><color=#FFFFFF>[ {name} ]</color></b>" : $"<color=#90A4AE>{name}</color>";

            if (GUILayout.Button(label, style, GUILayout.Height(25)))
            {
                _currentChannel = channel;
                _shouldScrollToBottom = true;
            }
        }

        private void DrawCollapsedMiniBar()
        {
            // Compact Bottom-Left Bar (Aola Star Mobile Mini Preview Bar)
            float barW = Mathf.Clamp(Screen.width * 0.28f, 380f, 430f);
            float barH = 42f;
            float barX = 16f;
            float barY = Screen.height - barH - 16f;

            // Get latest message for preview
            string previewText = "<color=#90A4AE>暂无新消息，点击展开交流...</color>";
            if (_allMessages.Count > 0)
            {
                var latest = _allMessages[_allMessages.Count - 1];
                string chTag = latest.Channel switch
                {
                    ChatChannel.Plaza => "<color=#FFD54F>[广场]</color>",
                    ChatChannel.Emote => "<color=#00E5FF>[动作]</color>",
                    ChatChannel.System => "<color=#76FF03>[系统]</color>",
                    _ => "<color=#00E5FF>[综合]</color>"
                };
                string sender = !string.IsNullOrEmpty(latest.Sender) ? $"<b>{latest.Sender}</b>: " : "";
                previewText = $"{chTag} {sender}{latest.Content}";
            }

            Rect barRect = new Rect(barX, barY, barW, barH);
            GUILayout.BeginArea(barRect, _miniBarStyle);
            GUILayout.BeginHorizontal();

            // Left icon & preview
            GUILayout.Label($"<size=12>💬 {previewText}</size>", _messageStyle, GUILayout.Height(26), GUILayout.MaxWidth(barW - 85));
            GUILayout.FlexibleSpace();

            // Expand Button
            if (GUILayout.Button("展开 ▶", _collapseBtnStyle, GUILayout.Width(66), GUILayout.Height(24)))
            {
                ToggleExpand();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Clicking on the mini bar area also expands
            if (Event.current.type == EventType.MouseDown && barRect.Contains(Event.current.mousePosition))
            {
                ToggleExpand();
                Event.current.Use();
            }
        }

        #endregion

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
