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
        Plaza = 1,   // 广场 (公屏)
        Whisper = 2, // 私聊 (密语)
        Emote = 3,   // 动作 (社交)
        System = 4   // 系统 (通知)
    }

    public class ChatMessageItem
    {
        public ChatChannel Channel;
        public string Sender;
        public string TargetName;
        public string Content;
        public string TimeStr;
        public string FormattedText;
        public bool IsSelfWhisper;
    }

    /// <summary>
    /// 奥拉星手游风格 空间社交与漫游 HUD
    /// - 左侧聊天窗口高度占屏幕 2/3
    /// - 支持一键收放折叠 (含迷你浮条预览与 [C] 快捷键)
    /// - 包含 [综合 / 广场 / 私聊 / 动作 / 系统] 五大频道切换
    /// - 支持私聊目标选择与私信双向通信
    /// - 内置快捷表情 (趣味表情 + 常用社交短语) 抽屉面板
    /// - 动作快捷栏与轮盘呼出集成
    /// </summary>
    public class DemoHUD : MonoBehaviour
    {
        [Header("Chat Settings")]
        [SerializeField] private bool defaultExpanded = true;
        [SerializeField] private int maxHistoryCount = 80;

        private bool _isExpanded = true;
        private ChatChannel _currentChannel = ChatChannel.All;
        private string _inputChat = "";
        private Vector2 _scrollPosition = Vector2.zero;
        private bool _shouldScrollToBottom = true;

        // Whisper (私聊) State
        private string _whisperTargetId = "";
        private string _whisperTargetName = "";
        private bool _showPlayerSelectDropdown = false;

        // Quick Emoji & Phrases Drawer State
        private bool _showQuickEmojiDrawer = false;
        private int _quickDrawerTab = 0; // 0: 表情, 1: 常用语

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
        private GUIStyle _emojiToggleBtnStyle;
        private GUIStyle _emoteBtnStyle;
        private GUIStyle _actionBtnStyle;
        private GUIStyle _miniBarStyle;
        private GUIStyle _drawerBoxStyle;
        private GUIStyle _drawerItemStyle;
        private GUIStyle _statusPanelStyle;
        private GUIStyle _statusLabelStyle;

        private Texture2D _texPanelBg;
        private Texture2D _texHeaderBg;
        private Texture2D _texTabActive;
        private Texture2D _texTabInactive;
        private Texture2D _texSendBtn;
        private Texture2D _texEmojiBtn;
        private Texture2D _texEmoteBtn;
        private Texture2D _texEmoteBtnHover;
        private Texture2D _texInputBg;
        private Texture2D _texMiniBarBg;
        private Texture2D _texDrawerBg;

        // Quick Emojis
        private readonly (string label, string text)[] _quickEmojis = new (string, string)[]
        {
            ("😆 大笑", "😆 哈哈哈哈！"),
            ("😍 喜欢", "😍 太喜欢这里了！"),
            ("😎 酷帅", "😎 帅气登场～"),
            ("🥳 欢呼", "🥳 耶！太棒啦！"),
            ("😭 哭泣", "😭 呜呜呜～"),
            ("🤔 思考", "🤔 容我想想..."),
            ("👍 点赞", "👍 给你点个赞！"),
            ("🙏 拜托", "🙏 拜托拜托啦～"),
            ("💪 加油", "💪 一起加油冲呀！"),
            ("✨ 闪耀", "✨ 闪亮登场！"),
            ("☕ 惬意", "☕ 喝杯咖啡休息下~"),
            ("🚀 起飞", "🚀 芜湖起飞！")
        };

        // Quick Phrases
        private readonly string[] _quickPhrases = new string[]
        {
            "你好呀！很高兴在智慧广场遇见你！👋",
            "有人一起在广场拍照漫游吗？📸",
            "这个智慧空间交互太有趣了！🚀",
            "加个好友常联系呀～🤝",
            "稍等我一下下，马上回来！☕",
            "今天先聊到这，下次再见啦！🌟"
        };

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
                NetworkManager.Instance.OnWhisperMessageReceived += HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived += HandlePlayerEmote;
                NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
            }

            AddSystemMessage("欢迎来到智慧空间广场！已载入奥拉通讯系统。");
            AddSystemMessage("提示: WASD移动 | T动作轮盘 | C收放窗口 | 支持私聊与快捷表情");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
                NetworkManager.Instance.OnWhisperMessageReceived -= HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerEmoteReceived -= HandlePlayerEmote;
                NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
            }
            CleanupTextures();
        }

        private void Update()
        {
            // Toggle Expand/Collapse with 'C' key when not typing
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
                AddSystemMessage("成功连接至空间服务器，随时可与同伴交流！");
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

        private void HandleWhisperMessage(WhisperMessageBroadcast msg)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMeSender = (msg.senderId == myId);

            if (msg.isError)
            {
                // System notification of whisper delivery error
                string errFormatted = $"<color=#78909C>[{timeStr}]</color> <color=#FF5252><b>[私聊提示]</b></color> {msg.message}";
                AddMessage(new ChatMessageItem
                {
                    Channel = ChatChannel.Whisper,
                    Sender = "系统",
                    Content = msg.message,
                    TimeStr = timeStr,
                    FormattedText = errFormatted
                });
                return;
            }

            string formatted;
            if (isMeSender)
            {
                // Sent by me
                formatted = $"<color=#78909C>[{timeStr}]</color> <color=#FF4081><b>[私聊]</b></color> 你对 <b><color=#80D8FF>{msg.targetName}</color></b> 说: {msg.message}";
            }
            else
            {
                // Sent by someone to me
                formatted = $"<color=#78909C>[{timeStr}]</color> <color=#FF4081><b>[私聊]</b></color> <b><color=#80D8FF>{msg.senderName}</color></b> 对你说: {msg.message}";

                // Auto memorize whisper partner for quick reply
                _whisperTargetId = msg.senderId;
                _whisperTargetName = msg.senderName;
            }

            AddMessage(new ChatMessageItem
            {
                Channel = ChatChannel.Whisper,
                Sender = msg.senderName,
                TargetName = msg.targetName,
                Content = msg.message,
                TimeStr = timeStr,
                FormattedText = formatted,
                IsSelfWhisper = isMeSender
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
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            if (sessionId != myId)
            {
                AddSystemMessage($"玩家 <color=#80D8FF><b>{username}</b></color> 进入了智慧空间广场！✨");
            }
        }

        private void HandlePlayerLeft(string sessionId)
        {
            AddSystemMessage("一名访客离开了空间。");
            if (_whisperTargetId == sessionId)
            {
                _whisperTargetId = "";
                _whisperTargetName = "";
            }
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
            if (_texEmojiBtn != null) Destroy(_texEmojiBtn);
            if (_texEmoteBtn != null) Destroy(_texEmoteBtn);
            if (_texEmoteBtnHover != null) Destroy(_texEmoteBtnHover);
            if (_texInputBg != null) Destroy(_texInputBg);
            if (_texMiniBarBg != null) Destroy(_texMiniBarBg);
            if (_texDrawerBg != null) Destroy(_texDrawerBg);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texPanelBg = MakeSolidTex(2, 2, new Color(0.05f, 0.08f, 0.14f, 0.90f));
            _texHeaderBg = MakeSolidTex(2, 2, new Color(0.08f, 0.13f, 0.22f, 0.95f));
            _texTabActive = MakeSolidTex(2, 2, new Color(0.0f, 0.55f, 0.85f, 0.92f));
            _texTabInactive = MakeSolidTex(2, 2, new Color(0.08f, 0.12f, 0.20f, 0.75f));
            _texSendBtn = MakeSolidTex(2, 2, new Color(0.0f, 0.70f, 0.95f, 0.95f));
            _texEmojiBtn = MakeSolidTex(2, 2, new Color(0.12f, 0.18f, 0.28f, 0.90f));
            _texEmoteBtn = MakeSolidTex(2, 2, new Color(0.09f, 0.15f, 0.25f, 0.85f));
            _texEmoteBtnHover = MakeSolidTex(2, 2, new Color(0.14f, 0.26f, 0.42f, 0.95f));
            _texInputBg = MakeSolidTex(2, 2, new Color(0.03f, 0.05f, 0.09f, 0.90f));
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.08f, 0.14f, 0.88f));
            _texDrawerBg = MakeSolidTex(2, 2, new Color(0.07f, 0.11f, 0.18f, 0.98f));

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texPanelBg },
                padding = new RectOffset(8, 8, 8, 8)
            };

            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = Color.white }
            };

            _collapseBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.85f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texTabActive }
            };

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.8f, 0.9f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(2, 2, 2, 2)
            };

            _inputFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                normal = { textColor = Color.white, background = _texInputBg },
                padding = new RectOffset(6, 6, 4, 4)
            };

            _sendBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texSendBtn }
            };

            _emojiToggleBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.4f), background = _texEmojiBtn },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            _emoteBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.95f, 1f), background = _texEmoteBtn },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            _actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.9f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

            _miniBarStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMiniBarBg },
                padding = new RectOffset(10, 10, 4, 4)
            };

            _drawerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texDrawerBg },
                padding = new RectOffset(6, 6, 6, 6)
            };

            _drawerItemStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = new Color(0.9f, 0.95f, 1f), background = _texEmoteBtn },
                hover = { textColor = Color.white, background = _texEmoteBtnHover }
            };

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

            // 1. Top-Left System Status Panel
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

            float panelHeight = Mathf.Max(400f, Screen.height * (2f / 3f));
            float panelWidth = Mathf.Clamp(Screen.width * 0.28f, 380f, 430f);
            float panelX = 16f;
            float panelY = Screen.height - panelHeight - 16f;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _panelStyle);

            // A. Top Header Bar
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Label("<size=13><b><color=#00E5FF>💬 空间通讯</color></b> <color=#546E7A>COMMUNICATION</color></size>", _headerTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("◀ 收起 [C]", _collapseBtnStyle, GUILayout.Width(76), GUILayout.Height(24)))
            {
                ToggleExpand();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // B. Channel Tabs (综合 / 广场 / 私聊 / 动作 / 系统)
            GUILayout.BeginHorizontal(GUILayout.Height(26));
            DrawChannelTab("综合", ChatChannel.All);
            DrawChannelTab("广场", ChatChannel.Plaza);
            DrawChannelTab("私聊", ChatChannel.Whisper);
            DrawChannelTab("动作", ChatChannel.Emote);
            DrawChannelTab("系统", ChatChannel.System);
            GUILayout.EndHorizontal();

            // C. Whisper Target Bar (Visible when in Whisper tab or target is selected)
            if (_currentChannel == ChatChannel.Whisper || !string.IsNullOrEmpty(_whisperTargetId))
            {
                DrawWhisperTargetBar();
            }

            GUILayout.Space(3);

            // D. Scrollable Message List
            float reservedBottomHeight = 78f + 32f + 16f;
            float scrollH = panelHeight - 90f - reservedBottomHeight;
            if (scrollH < 100f) scrollH = 100f;

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.Height(scrollH)
            );

            int displayedCount = 0;
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";

            foreach (var item in _allMessages)
            {
                bool show = false;
                if (_currentChannel == ChatChannel.All)
                {
                    show = true;
                }
                else if (_currentChannel == ChatChannel.Whisper)
                {
                    show = (item.Channel == ChatChannel.Whisper);
                }
                else
                {
                    show = (item.Channel == _currentChannel);
                }

                if (show)
                {
                    GUILayout.Label(item.FormattedText, _messageStyle);
                    displayedCount++;
                }
            }

            if (displayedCount == 0)
            {
                string emptyHint = _currentChannel switch
                {
                    ChatChannel.Whisper => "<color=#78909C>暂无私聊消息。点击下方选择玩家发起私聊...</color>",
                    ChatChannel.Plaza => "<color=#78909C>暂无公屏聊天，发一条和大家问好吧~</color>",
                    ChatChannel.Emote => "<color=#78909C>暂无动作记录，试试快捷动作打招呼！</color>",
                    _ => "<color=#78909C>当前频道暂无消息记录...</color>"
                };
                GUILayout.Label(emptyHint, _messageStyle);
            }

            if (_shouldScrollToBottom)
            {
                _scrollPosition.y = float.MaxValue;
                _shouldScrollToBottom = false;
            }

            GUILayout.EndScrollView();

            GUILayout.Space(3);

            // E. Interactive Bottom Bar: Quick Emoji Drawer OR Quick Body Emote Buttons
            if (_showQuickEmojiDrawer)
            {
                DrawQuickEmojiDrawer();
            }
            else
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("👋 挥手 [1]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.Wave);
                if (GUILayout.Button("💖 比心 [2]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.Heart);
                if (GUILayout.Button("👏 鼓掌 [3]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.Clap);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🙇 鞠躬 [4]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.Bow);
                if (GUILayout.Button("🕺 跳舞 [5]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.Dance);
                if (GUILayout.Button("✋ 击掌 [6]", _emoteBtnStyle, GUILayout.Height(22))) TriggerLocalEmote(EmoteType.HighFive);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🎡 动作轮盘 (Hold T)", _actionBtnStyle, GUILayout.Height(21)))
                {
                    if (EmoteWheelUI.Instance != null) EmoteWheelUI.Instance.OpenWheel();
                }
                if (GUILayout.Button("🤖 召唤访客 (Bot)", _actionBtnStyle, GUILayout.Height(21)))
                {
                    if (NetworkManager.Instance != null && isConnected) NetworkManager.Instance.SpawnNetworkBot();
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.Space(3);

            // G. Input Bar with Emoji Toggle & Send Button
            GUILayout.BeginHorizontal(GUILayout.Height(28));

            // Emoji Drawer Toggle Button
            string emojiToggleText = _showQuickEmojiDrawer ? "▲" : "😀";
            if (GUILayout.Button(emojiToggleText, _emojiToggleBtnStyle, GUILayout.Width(30), GUILayout.Height(26)))
            {
                _showQuickEmojiDrawer = !_showQuickEmojiDrawer;
            }

            // Input Field
            GUI.SetNextControlName("AolaChatInputField");
            _inputChat = GUILayout.TextField(_inputChat, _inputFieldStyle, GUILayout.Height(26));

            bool pressEnter = Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "AolaChatInputField";
            if ((GUILayout.Button("发送", _sendBtnStyle, GUILayout.Width(52), GUILayout.Height(26)) || pressEnter)
                && !string.IsNullOrEmpty(_inputChat.Trim()))
            {
                SendMessageContent(_inputChat.Trim());
                _inputChat = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Player Selection Dropdown Overlay (if open)
            if (_showPlayerSelectDropdown)
            {
                DrawPlayerSelectDropdown(panelX, panelY, panelWidth);
            }
        }

        private void DrawChannelTab(string name, ChatChannel channel)
        {
            bool isActive = (_currentChannel == channel);
            GUIStyle style = isActive ? _tabActiveStyle : _tabInactiveStyle;
            string label = isActive ? $"<b><color=#FFFFFF>[{name}]</color></b>" : $"<color=#90A4AE>{name}</color>";

            if (GUILayout.Button(label, style, GUILayout.Height(24)))
            {
                _currentChannel = channel;
                _shouldScrollToBottom = true;
            }
        }

        private void DrawWhisperTargetBar()
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(24));
            if (!string.IsNullOrEmpty(_whisperTargetId))
            {
                GUILayout.Label($"<color=#FF4081><b>[密语]</b></color> 目标: <b><color=#80D8FF>{_whisperTargetName}</color></b>", _messageStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("更换", _actionBtnStyle, GUILayout.Width(44), GUILayout.Height(20)))
                {
                    _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                }
                if (GUILayout.Button("✕", _actionBtnStyle, GUILayout.Width(22), GUILayout.Height(20)))
                {
                    _whisperTargetId = "";
                    _whisperTargetName = "";
                    _showPlayerSelectDropdown = false;
                }
            }
            else
            {
                GUILayout.Label("<color=#FF4081><b>[密语]</b></color> <color=#90A4AE>未指定目标，点击右侧选择玩家:</color>", _messageStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("选择玩家 ▼", _actionBtnStyle, GUILayout.Width(80), GUILayout.Height(20)))
                {
                    _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPlayerSelectDropdown(float panelX, float panelY, float panelWidth)
        {
            float dropW = 220f;
            float dropH = 160f;
            float dropX = panelX + 70f;
            float dropY = panelY + 60f;

            Rect dropRect = new Rect(dropX, dropY, dropW, dropH);
            GUILayout.BeginArea(dropRect, _drawerBoxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#00E5FF><b>在线玩家列表</b></color>", _headerTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _actionBtnStyle, GUILayout.Width(22), GUILayout.Height(20)))
            {
                _showPlayerSelectDropdown = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            var onlineMap = NetworkManager.Instance != null ? NetworkManager.Instance.OnlinePlayers : null;

            int count = 0;
            if (onlineMap != null)
            {
                foreach (var kvp in onlineMap)
                {
                    if (kvp.Key == myId) continue; // Skip self

                    if (GUILayout.Button($"👤 {kvp.Value}", _drawerItemStyle, GUILayout.Height(24)))
                    {
                        _whisperTargetId = kvp.Key;
                        _whisperTargetName = kvp.Value;
                        _showPlayerSelectDropdown = false;
                        _currentChannel = ChatChannel.Whisper;
                    }
                    count++;
                }
            }

            if (count == 0)
            {
                GUILayout.Label("<color=#90A4AE>暂无其他在线玩家\n可点击[召唤访客]测试</color>", _messageStyle);
            }

            GUILayout.EndArea();

            // Click outside closes dropdown
            if (Event.current.type == EventType.MouseDown && !dropRect.Contains(Event.current.mousePosition))
            {
                _showPlayerSelectDropdown = false;
            }
        }

        private void DrawQuickEmojiDrawer()
        {
            GUILayout.BeginVertical(_drawerBoxStyle, GUILayout.Height(100));

            // Drawer Header with Sub-tabs
            GUILayout.BeginHorizontal(GUILayout.Height(20));
            GUIStyle tab0Style = (_quickDrawerTab == 0) ? _tabActiveStyle : _tabInactiveStyle;
            GUIStyle tab1Style = (_quickDrawerTab == 1) ? _tabActiveStyle : _tabInactiveStyle;

            if (GUILayout.Button("😀 趣味表情", tab0Style, GUILayout.Width(85), GUILayout.Height(20))) _quickDrawerTab = 0;
            if (GUILayout.Button("💬 常用短语", tab1Style, GUILayout.Width(85), GUILayout.Height(20))) _quickDrawerTab = 1;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _actionBtnStyle, GUILayout.Width(22), GUILayout.Height(18)))
            {
                _showQuickEmojiDrawer = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            if (_quickDrawerTab == 0)
            {
                // 3 rows x 4 emojis = 12 Emojis
                for (int row = 0; row < 3; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int col = 0; col < 4; col++)
                    {
                        int idx = row * 4 + col;
                        if (idx < _quickEmojis.Length)
                        {
                            var item = _quickEmojis[idx];
                            if (GUILayout.Button(item.label, _drawerItemStyle, GUILayout.Height(21)))
                            {
                                SendMessageContent(item.text);
                                _showQuickEmojiDrawer = false;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                // Quick Social Phrases - 2 Columns x 3 Rows
                for (int i = 0; i < _quickPhrases.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(_quickPhrases[i], _drawerItemStyle, GUILayout.Height(21)))
                    {
                        SendMessageContent(_quickPhrases[i]);
                        _showQuickEmojiDrawer = false;
                    }
                    if (i + 1 < _quickPhrases.Length)
                    {
                        if (GUILayout.Button(_quickPhrases[i + 1], _drawerItemStyle, GUILayout.Height(21)))
                        {
                            SendMessageContent(_quickPhrases[i + 1]);
                            _showQuickEmojiDrawer = false;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
        }

        private void SendMessageContent(string content)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

            // If in Whisper channel and a target is selected, send as Whisper
            if (_currentChannel == ChatChannel.Whisper || !string.IsNullOrEmpty(_whisperTargetId))
            {
                if (string.IsNullOrEmpty(_whisperTargetId))
                {
                    AddSystemMessage("<color=#FF5252>请先选择私聊目标玩家！</color>");
                    _showPlayerSelectDropdown = true;
                    return;
                }

                NetworkManager.Instance.SendWhisper(_whisperTargetId, content);
            }
            else
            {
                // Send as public Plaza chat
                NetworkManager.Instance.SendChat(content);
            }
        }

        private void DrawCollapsedMiniBar()
        {
            float barW = Mathf.Clamp(Screen.width * 0.28f, 380f, 430f);
            float barH = 42f;
            float barX = 16f;
            float barY = Screen.height - barH - 16f;

            string previewText = "<color=#90A4AE>暂无新消息，点击展开交流...</color>";
            if (_allMessages.Count > 0)
            {
                var latest = _allMessages[_allMessages.Count - 1];
                string chTag = latest.Channel switch
                {
                    ChatChannel.Plaza => "<color=#FFD54F>[广场]</color>",
                    ChatChannel.Whisper => "<color=#FF4081>[私聊]</color>",
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

            GUILayout.Label($"<size=12>💬 {previewText}</size>", _messageStyle, GUILayout.Height(28), GUILayout.MaxWidth(barW - 85));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("展开 ▶", _collapseBtnStyle, GUILayout.Width(66), GUILayout.Height(24)))
            {
                ToggleExpand();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

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
