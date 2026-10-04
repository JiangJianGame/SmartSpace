using System;
using System.Collections.Generic;
using UnityEngine;
using SmartSpace.Network;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    public enum ChatChannel
    {
        World = 0,    // 世界 (全服广场)
        Nearby = 1,   // 附近 (区域同屏)
        System = 2,   // 系统 (原队伍)
        Whisper = 3   // 私聊 (原战队)
    }

    public class ChatBubbleItem
    {
        public ChatChannel Channel;
        public string SenderId;
        public string SenderName;
        public string TargetId;
        public string TargetName;
        public string Content;
        public string TimeStr;
        public bool IsSelf;
        public int Level;
        public int AvatarIndex;
    }

    /// <summary>
    /// 奥拉星手游 1:1 风格空间社交聊天界面
    /// - 左侧垂直频道导航栏：[世界] [附近] [系统] [私聊] (动作分类已移除)
    /// - 经典气泡聊天布局：左侧显示其他玩家头像与发言，右侧显示本地玩家头像与发言
    /// - 顶部系统通知跑马灯横幅
    /// - 底部输入栏：语音图标、白色圆角输入框、黄色表情按钮、亮黄[发送]按钮
    /// - 占屏 2/3 高度，支持一键收起/展开 (含迷你浮条预览与 [C] 快捷键)
    /// </summary>
    public class DemoHUD : MonoBehaviour
    {
        [Header("Chat Settings")]
        [SerializeField] private bool defaultExpanded = true;
        [SerializeField] private int maxHistoryCount = 80;

        private bool _isExpanded = true;
        private ChatChannel _currentChannel = ChatChannel.World;
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

        private readonly List<ChatBubbleItem> _messages = new List<ChatBubbleItem>();

        // Custom UI Styles & Textures
        private bool _stylesInitialized = false;
        private GUIStyle _mainPanelStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _tabInactiveStyle;
        private GUIStyle _marqueeStyle;
        private GUIStyle _senderNameOtherStyle;
        private GUIStyle _senderNameSelfStyle;
        private GUIStyle _bubbleOtherStyle;
        private GUIStyle _bubbleSelfStyle;
        private GUIStyle _avatarLevelStyle;
        private GUIStyle _systemNoticeBoxStyle;
        private GUIStyle _systemTagStyle;
        private GUIStyle _systemContentStyle;
        private GUIStyle _inputFieldStyle;
        private GUIStyle _sendBtnStyle;
        private GUIStyle _emojiRoundBtnStyle;
        private GUIStyle _iconBtnStyle;
        private GUIStyle _miniBarStyle;
        private GUIStyle _drawerBoxStyle;
        private GUIStyle _drawerItemStyle;
        private GUIStyle _sideActionBtnStyle;
        private GUIStyle _statusPanelStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _placeholderStyle;

        private Texture2D _texMainBg;
        private Texture2D _texTabActive;
        private Texture2D _texTabInactive;
        private Texture2D _texBubbleBlue;
        private Texture2D _texSendBtnYellow;
        private Texture2D _texInputWhite;
        private Texture2D _texEmojiDark;
        private Texture2D _texDrawerBg;
        private Texture2D _texMiniBarBg;
        private Texture2D _texAvatarBorder;
        private Texture2D _texSmileyIcon;

        // Procedural Avatars
        private Texture2D _texAvatarSelf;
        private Texture2D[] _texAvatarOthers;

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
                NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
            }

            // Initial Welcome System Message
            AddSystemMessage("成功连接至奥拉通讯网络。当前频道已就绪！");
            AddSystemMessage("操作提示: WASD移动 | T动作轮盘 | C收放聊天窗口");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
                NetworkManager.Instance.OnWhisperMessageReceived -= HandleWhisperMessage;
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

        #region Network Handlers

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
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMe = (senderId == myId);

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.World,
                SenderId = senderId,
                SenderName = username,
                Content = message,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = isMe,
                Level = isMe ? 40 : (Math.Abs(senderId.GetHashCode() % 30) + 15),
                AvatarIndex = Math.Abs(senderId.GetHashCode() % 3)
            });
        }

        private void HandleWhisperMessage(WhisperMessageBroadcast msg)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMeSender = (msg.senderId == myId);

            if (msg.isError)
            {
                AddSystemMessage($"[私聊提示] {msg.message}");
                return;
            }

            if (!isMeSender)
            {
                _whisperTargetId = msg.senderId;
                _whisperTargetName = msg.senderName;
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Whisper,
                SenderId = msg.senderId,
                SenderName = msg.senderName,
                TargetId = msg.targetId,
                TargetName = msg.targetName,
                Content = msg.message,
                TimeStr = timeStr,
                IsSelf = isMeSender,
                Level = isMeSender ? 40 : (Math.Abs(msg.senderId.GetHashCode() % 30) + 15),
                AvatarIndex = Math.Abs(msg.senderId.GetHashCode() % 3)
            });
        }

        private void HandlePlayerJoined(string sessionId, string username)
        {
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            if (sessionId != myId)
            {
                AddSystemMessage($"玩家 <color=#FFD54F><b>{username}</b></color> 成功进入了智慧空间广场！✨");
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
            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.System,
                SenderId = "system",
                SenderName = "系统通知",
                Content = content,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                Level = 99
            });
        }

        private void AddBubbleMessage(ChatBubbleItem item)
        {
            _messages.Add(item);
            if (_messages.Count > maxHistoryCount)
            {
                _messages.RemoveAt(0);
            }
            _shouldScrollToBottom = true;
        }

        #endregion

        #region Textures & Styles Initialization

        private Texture2D MakeSolidTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeBorderedTex(int width, int height, Color fillCol, Color borderCol, int borderWidth)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pix = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth);
                    pix[y * width + x] = isBorder ? borderCol : fillCol;
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeAvatarTex(Color baseCol, Color accentCol)
        {
            int s = 40;
            Texture2D tex = new Texture2D(s, s);
            Color[] pix = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float r = s * 0.44f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    if (d > r)
                    {
                        pix[y * s + x] = new Color(0, 0, 0, 0); // Transparent outside circle
                    }
                    else if (d >= r - 2f)
                    {
                        pix[y * s + x] = accentCol; // Outer Border
                    }
                    else if (d >= r * 0.5f)
                    {
                        pix[y * s + x] = baseCol; // Middle gradient
                    }
                    else
                    {
                        pix[y * s + x] = Color.white; // Cute bright center / eyes
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeSmileyTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pix = new Color[size * size];
            float r = size * 0.5f;
            Vector2 center = new Vector2(r, r);
            Color yellow = new Color(1.0f, 0.86f, 0.16f, 1f);
            Color black = new Color(0.12f, 0.12f, 0.12f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d > r)
                    {
                        pix[y * size + x] = Color.clear;
                    }
                    else if (d >= r - 1.5f)
                    {
                        pix[y * size + x] = new Color(0.85f, 0.70f, 0.10f, 1f);
                    }
                    else
                    {
                        float leftEye = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.35f, size * 0.60f));
                        float rightEye = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.65f, size * 0.60f));
                        float mouthCenter = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.45f));
                        bool isMouth = (mouthCenter >= size * 0.18f && mouthCenter <= size * 0.27f && y < size * 0.42f && x >= size * 0.26f && x <= size * 0.74f);

                        if (leftEye < size * 0.09f || rightEye < size * 0.09f || isMouth)
                        {
                            pix[y * size + x] = black;
                        }
                        else
                        {
                            pix[y * size + x] = yellow;
                        }
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void CleanupTextures()
        {
            if (_texMainBg != null) Destroy(_texMainBg);
            if (_texTabActive != null) Destroy(_texTabActive);
            if (_texTabInactive != null) Destroy(_texTabInactive);
            if (_texBubbleBlue != null) Destroy(_texBubbleBlue);
            if (_texSendBtnYellow != null) Destroy(_texSendBtnYellow);
            if (_texInputWhite != null) Destroy(_texInputWhite);
            if (_texEmojiDark != null) Destroy(_texEmojiDark);
            if (_texDrawerBg != null) Destroy(_texDrawerBg);
            if (_texMiniBarBg != null) Destroy(_texMiniBarBg);
            if (_texAvatarBorder != null) Destroy(_texAvatarBorder);
            if (_texAvatarSelf != null) Destroy(_texAvatarSelf);
            if (_texSmileyIcon != null) Destroy(_texSmileyIcon);
            if (_texAvatarOthers != null)
            {
                foreach (var t in _texAvatarOthers) if (t != null) Destroy(t);
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 1. Textures
            _texMainBg = MakeSolidTex(2, 2, new Color(0.04f, 0.08f, 0.16f, 0.90f));
            _texTabActive = MakeSolidTex(2, 2, new Color(0.12f, 0.48f, 0.88f, 1.0f)); // Bright Blue from screenshot
            _texTabInactive = MakeSolidTex(2, 2, new Color(0.08f, 0.14f, 0.24f, 0.85f)); // Dark Slate Blue
            _texBubbleBlue = MakeBorderedTex(32, 32, new Color(0.08f, 0.44f, 0.82f, 0.96f), new Color(0.12f, 0.54f, 0.95f, 1f), 1);
            _texSendBtnYellow = MakeSolidTex(2, 2, new Color(1.0f, 0.86f, 0.18f, 1.0f)); // Bright Golden-Yellow from screenshot
            _texInputWhite = MakeSolidTex(2, 2, new Color(0.93f, 0.95f, 0.97f, 1.0f)); // White Input box from screenshot
            _texEmojiDark = MakeSolidTex(2, 2, new Color(0.10f, 0.15f, 0.22f, 0.95f));
            _texDrawerBg = MakeSolidTex(2, 2, new Color(0.07f, 0.12f, 0.20f, 0.98f));
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.09f, 0.16f, 0.90f));
            _texAvatarBorder = MakeBorderedTex(42, 42, new Color(0.10f, 0.16f, 0.26f, 1f), new Color(0.20f, 0.55f, 0.95f, 1f), 2);
            _texSmileyIcon = MakeSmileyTex(34);

            // Avatars
            _texAvatarSelf = MakeAvatarTex(new Color(0.0f, 0.65f, 0.95f), new Color(0.4f, 0.85f, 1f));
            _texAvatarOthers = new Texture2D[]
            {
                MakeAvatarTex(new Color(0.95f, 0.55f, 0.15f), new Color(1f, 0.75f, 0.3f)), // Fox/Pet Orange
                MakeAvatarTex(new Color(0.85f, 0.25f, 0.35f), new Color(1f, 0.45f, 0.55f)), // Dragon Red
                MakeAvatarTex(new Color(0.25f, 0.75f, 0.45f), new Color(0.5f, 0.95f, 0.65f))  // Nature Green
            };

            // 2. GUIStyles
            _mainPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMainBg },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // Vertical Tabs (Left Column)
            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texTabActive }
            };

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.48f, 0.68f, 0.88f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            // Subtitle / Hint Label Style
            _marqueeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.85f, 0.95f) },
                padding = new RectOffset(6, 6, 2, 2)
            };

            // Player Names
            _senderNameOtherStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                normal = { textColor = new Color(1.0f, 0.85f, 0.35f) } // Golden-Yellow from screenshot
            };

            _senderNameSelfStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                richText = true,
                normal = { textColor = new Color(1.0f, 0.85f, 0.35f) }
            };

            // Speech Bubbles
            _bubbleOtherStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                richText = true,
                normal = { textColor = Color.white, background = _texBubbleBlue },
                padding = new RectOffset(10, 10, 6, 6)
            };

            _bubbleSelfStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                wordWrap = true,
                richText = true,
                normal = { textColor = Color.white, background = _texBubbleBlue },
                padding = new RectOffset(10, 10, 6, 6)
            };

            _avatarLevelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = Color.white }
            };

            _systemNoticeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texTabInactive },
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            _systemTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.46f, 1.0f, 0.01f) }, // Vibrant Green #76FF03
                padding = new RectOffset(0, 0, 0, 0)
            };

            _systemContentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.90f, 0.95f, 1.0f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // Input Bar
            _inputFieldStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.12f, 0.12f, 0.12f), background = _texInputWhite }, // Dark text on white
                padding = new RectOffset(8, 8, 6, 6)
            };

            _placeholderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                richText = true,
                normal = { textColor = new Color(0.58f, 0.62f, 0.68f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _sendBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.18f, 0.14f, 0.04f), background = _texSendBtnYellow }, // Dark text on Yellow
                hover = { textColor = Color.black, background = _texSendBtnYellow }
            };

            _emojiRoundBtnStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
                normal = { background = _texEmojiDark },
                hover = { background = _texTabActive }
            };

            _iconBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 0.85f, 1f), background = _texEmojiDark },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            // Drawer & Side Buttons
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
                normal = { textColor = new Color(0.9f, 0.95f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            _sideActionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.65f, 0.85f, 1f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            _miniBarStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMiniBarBg },
                padding = new RectOffset(10, 10, 4, 4)
            };

            // Status Panel (Top Left)
            _statusPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMainBg },
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
                DrawAolaStarChatWindow();
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

            GUILayout.Label("<size=14><b><color=#00E5FF>◆</color> 智慧空间 · 社交漫游系统</b></size>", _senderNameOtherStyle);
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

        private void DrawAolaStarChatWindow()
        {
            float panelHeight = Mathf.Max(420f, Screen.height * (2f / 3f));
            float panelWidth = Mathf.Clamp(Screen.width * 0.35f, 450f, 520f);
            float panelX = 16f;
            float panelY = Screen.height - panelHeight - 16f;

            float tabColWidth = 72f;
            float chatAreaWidth = panelWidth - tabColWidth;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _mainPanelStyle);
            GUILayout.BeginHorizontal();

            // -------------------------------------------------------------
            // A. LEFT VERTICAL CHANNEL TAB BAR (奥拉星左侧频道导航栏)
            // -------------------------------------------------------------
            GUILayout.BeginVertical(GUILayout.Width(tabColWidth));

            DrawVerticalChannelTab("世界", ChatChannel.World);
            DrawVerticalChannelTab("附近", ChatChannel.Nearby);
            DrawVerticalChannelTab("系统", ChatChannel.System);
            DrawVerticalChannelTab("私聊", ChatChannel.Whisper);

            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();

            // -------------------------------------------------------------
            // B. RIGHT CHAT CONTENT COLUMN (右侧主聊天显示与输入区)
            // -------------------------------------------------------------
            GUILayout.BeginVertical(GUILayout.Width(chatAreaWidth));

            // B1. Top Header Bar (右上角操作按钮: 轮盘 / 访客 / 收起)
            DrawTopHeaderBar();

            // B2. Whisper Target Selector (仅私聊频道显示)
            if (_currentChannel == ChatChannel.Whisper)
            {
                DrawWhisperTargetBar();
            }

            // B3. Scrollable Bubble Messages Area
            float currentInputH = GetInputBarHeight(chatAreaWidth);
            float topHeaderH = 32f + (_currentChannel == ChatChannel.Whisper ? 28f : 0f);
            float bottomReservedH = currentInputH + 16f + (_showQuickEmojiDrawer ? 100f : 0f);
            float scrollH = panelHeight - topHeaderH - bottomReservedH;
            if (scrollH < 120f) scrollH = 120f;

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.Height(scrollH)
            );

            int displayedCount = 0;
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";

            foreach (var msg in _messages)
            {
                bool shouldShow = false;
                if (_currentChannel == ChatChannel.World || _currentChannel == ChatChannel.Nearby)
                {
                    shouldShow = (msg.Channel == ChatChannel.World || msg.Channel == ChatChannel.Nearby);
                }
                else if (_currentChannel == ChatChannel.System)
                {
                    shouldShow = (msg.Channel == ChatChannel.System);
                }
                else if (_currentChannel == ChatChannel.Whisper)
                {
                    shouldShow = (msg.Channel == ChatChannel.Whisper);
                }

                if (shouldShow)
                {
                    if (msg.Channel == ChatChannel.System)
                    {
                        DrawSystemNoticeBubble(msg, chatAreaWidth);
                    }
                    else
                    {
                        DrawPlayerChatBubble(msg, chatAreaWidth);
                    }
                    displayedCount++;
                }
            }

            if (displayedCount == 0)
            {
                string emptyHint = _currentChannel switch
                {
                    ChatChannel.Whisper => "<color=#78909C>暂无私聊消息。点击上方选择玩家发起私聊...</color>",
                    ChatChannel.System => "<color=#78909C>暂无系统公告记录。</color>",
                    _ => "<color=#78909C>暂无发言记录，快在下方输入与大家打招呼吧！</color>"
                };
                GUILayout.Label(emptyHint, _marqueeStyle);
            }

            if (_shouldScrollToBottom)
            {
                _scrollPosition.y = float.MaxValue;
                _shouldScrollToBottom = false;
            }

            GUILayout.EndScrollView();

            // B4. Quick Emoji & Phrases Drawer (if opened)
            if (_showQuickEmojiDrawer)
            {
                DrawQuickEmojiDrawer();
            }

            // B5. Bottom Input Bar (自适应动态高度无截断完整显示长文本)
            DrawBottomInputBar(chatAreaWidth, currentInputH);

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Player Selection Dropdown Overlay (if open)
            if (_showPlayerSelectDropdown)
            {
                DrawPlayerSelectDropdown(panelX + tabColWidth, panelY, chatAreaWidth);
            }
        }

        private void DrawVerticalChannelTab(string label, ChatChannel channel)
        {
            bool isActive = (_currentChannel == channel);
            GUIStyle style = isActive ? _tabActiveStyle : _tabInactiveStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(52)))
            {
                _currentChannel = channel;
                _shouldScrollToBottom = true;
            }
        }

        private void DrawTopHeaderBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Space(2);

            string channelTitle = _currentChannel switch
            {
                ChatChannel.World => "<color=#1E88E5><b>● 世界频道</b></color>",
                ChatChannel.Nearby => "<color=#00E5FF><b>● 附近频道</b></color>",
                ChatChannel.System => "<color=#76FF03><b>● 系统公告</b></color>",
                ChatChannel.Whisper => "<color=#FF4081><b>● 私聊密语</b></color>",
                _ => "<color=#80D8FF><b>● 综合</b></color>"
            };
            GUILayout.Label(channelTitle, _senderNameOtherStyle, GUILayout.Height(24));

            GUILayout.FlexibleSpace();

            // 右上角操作按钮: 轮盘 / 访客 / 收起
            if (GUILayout.Button("🎡 轮盘", _sideActionBtnStyle, GUILayout.Width(62), GUILayout.Height(24)))
            {
                if (EmoteWheelUI.Instance != null) EmoteWheelUI.Instance.OpenWheel();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("🤖 访客", _sideActionBtnStyle, GUILayout.Width(62), GUILayout.Height(24)))
            {
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                {
                    NetworkManager.Instance.SpawnNetworkBot();
                }
            }

            GUILayout.Space(4);

            if (GUILayout.Button("◀ 收起", _sideActionBtnStyle, GUILayout.Width(58), GUILayout.Height(24)))
            {
                ToggleExpand();
            }

            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }

        private void DrawPlayerChatBubble(ChatBubbleItem msg, float contentWidth)
        {
            GUILayout.Space(6);

            Texture2D avatarTex = msg.IsSelf ? _texAvatarSelf : _texAvatarOthers[msg.AvatarIndex % _texAvatarOthers.Length];
            float maxBubbleW = Mathf.Clamp(contentWidth - 110f, 180f, 260f);

            if (!msg.IsSelf)
            {
                // ---------------------------------------------------------
                // OTHER PLAYERS: Left Avatar + Golden Name + Blue Bubble
                // ---------------------------------------------------------
                GUILayout.BeginHorizontal();

                // Avatar Box with Level
                Rect avatarRect = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                GUI.DrawTexture(avatarRect, _texAvatarBorder);
                GUI.DrawTexture(new Rect(avatarRect.x + 2, avatarRect.y + 2, 38, 38), avatarTex);
                GUI.Label(new Rect(avatarRect.x + 4, avatarRect.y + 24, 20, 16), $"{msg.Level}", _avatarLevelStyle);

                GUILayout.Space(6);

                // Content Column (Name above Bubble)
                GUILayout.BeginVertical();
                GUILayout.Label(msg.SenderName, _senderNameOtherStyle);
                GUILayout.Space(2);
                GUILayout.Label(msg.Content, _bubbleOtherStyle, GUILayout.MaxWidth(maxBubbleW));
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                // ---------------------------------------------------------
                // LOCAL PLAYER (SELF): Blue Bubble + Golden Name + Right Avatar
                // ---------------------------------------------------------
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Content Column (Name above Bubble, Right-aligned)
                GUILayout.BeginVertical();
                string targetPrefix = (!string.IsNullOrEmpty(msg.TargetName)) ? $"对 <color=#80D8FF>{msg.TargetName}</color> 说" : msg.SenderName;
                GUILayout.Label(targetPrefix, _senderNameSelfStyle);
                GUILayout.Space(2);
                GUILayout.Label(msg.Content, _bubbleSelfStyle, GUILayout.MaxWidth(maxBubbleW));
                GUILayout.EndVertical();

                GUILayout.Space(6);

                // Avatar Box with Level
                Rect avatarRect = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                GUI.DrawTexture(avatarRect, _texAvatarBorder);
                GUI.DrawTexture(new Rect(avatarRect.x + 2, avatarRect.y + 2, 38, 38), avatarTex);
                GUI.Label(new Rect(avatarRect.x + 4, avatarRect.y + 24, 20, 16), $"{msg.Level}", _avatarLevelStyle);

                GUILayout.EndHorizontal();
            }
        }

        private void DrawSystemNoticeBubble(ChatBubbleItem msg, float contentWidth)
        {
            GUILayout.Space(3);
            float cardWidth = Mathf.Max(240f, contentWidth - 28f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);

            GUILayout.BeginVertical(_systemNoticeBoxStyle, GUILayout.Width(cardWidth));

            // Top Row: System Tag + Timestamp
            GUILayout.BeginHorizontal();
            GUILayout.Label("📢 <b>[系统通知]</b>", _systemTagStyle, GUILayout.Height(18));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<color=#90A4AE><size=10>{msg.TimeStr}</size></color>", _avatarLevelStyle, GUILayout.Height(18));
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Content body: neatly word-wrapped with fixed width
            GUILayout.Label(msg.Content, _systemContentStyle, GUILayout.Width(cardWidth - 20f));

            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }

        private void DrawWhisperTargetBar()
        {
            GUILayout.BeginHorizontal(_marqueeStyle, GUILayout.Height(24));
            if (!string.IsNullOrEmpty(_whisperTargetId))
            {
                GUILayout.Label($"<color=#FF4081><b>[密语]</b></color> 目标: <b><color=#80D8FF>{_whisperTargetName}</color></b>", _marqueeStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("更换", _sideActionBtnStyle, GUILayout.Width(44), GUILayout.Height(20)))
                {
                    _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                }
                if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(22), GUILayout.Height(20)))
                {
                    _whisperTargetId = "";
                    _whisperTargetName = "";
                    _showPlayerSelectDropdown = false;
                }
            }
            else
            {
                GUILayout.Label("<color=#FF4081><b>[密语]</b></color> <color=#90A4AE>未指定目标，点击选择玩家:</color>", _marqueeStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("选择玩家 ▼", _sideActionBtnStyle, GUILayout.Width(80), GUILayout.Height(20)))
                {
                    _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                }
            }
            GUILayout.EndHorizontal();
        }

        private float GetInputBarHeight(float contentWidth)
        {
            float rightBtnsW = 34f + 4f + 66f + 8f; // 112f
            float inputW = Mathf.Max(180f, contentWidth - rightBtnsW);

            if (string.IsNullOrEmpty(_inputChat) || _inputFieldStyle == null)
            {
                return 34f;
            }

            float textH = _inputFieldStyle.CalcHeight(new GUIContent(_inputChat), inputW);
            return Mathf.Clamp(textH + 4f, 34f, 76f);
        }

        private void DrawBottomInputBar(float contentWidth, float inputH)
        {
            float rightBtnsW = 34f + 4f + 66f + 8f; // Emoji(34) + Sp(4) + Send(66) + Margins(8) = 112f
            float inputW = Mathf.Max(180f, contentWidth - rightBtnsW);

            GUILayout.BeginHorizontal(GUILayout.Height(inputH));

            // Intercept Enter key for sending before TextArea inserts a newline (Shift+Enter to add newline)
            Event e = Event.current;
            bool isFocused = (GUI.GetNameOfFocusedControl() == "AolaChatInputField");
            bool pressEnter = (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && isFocused && !e.shift);

            if (pressEnter)
            {
                e.Use();
                if (!string.IsNullOrEmpty(_inputChat.Trim()))
                {
                    SendMessageContent(_inputChat.Trim());
                    _inputChat = "";
                    GUI.FocusControl(null);
                }
            }

            // 1. White Rounded Input Box (Auto-expanding height + wordWrap to show FULL text without ANY truncation)
            GUI.SetNextControlName("AolaChatInputField");
            Rect inputRect = GUILayoutUtility.GetRect(inputW, inputH, GUILayout.Width(inputW), GUILayout.Height(inputH));
            _inputChat = GUI.TextArea(inputRect, _inputChat, 120, _inputFieldStyle);

            if (string.IsNullOrEmpty(_inputChat) && !isFocused)
            {
                GUI.Label(new Rect(inputRect.x + 8, inputRect.y + 7, inputRect.width - 16, 20), "点击输入...", _placeholderStyle);
            }

            GUILayout.Space(4);

            // Right side buttons container (vertically aligned to bottom of input area)
            GUILayout.BeginVertical(GUILayout.Height(inputH));
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            // 2. Yellow Smiley Emoji Button (1:1 Procedural Icon matching reference image)
            if (GUILayout.Button(_texSmileyIcon, _emojiRoundBtnStyle, GUILayout.Width(34), GUILayout.Height(34)))
            {
                _showQuickEmojiDrawer = !_showQuickEmojiDrawer;
            }

            GUILayout.Space(4);

            // 3. Vibrant Golden-Yellow Send Button
            if (GUILayout.Button("发 送", _sendBtnStyle, GUILayout.Width(66), GUILayout.Height(34))
                && !string.IsNullOrEmpty(_inputChat.Trim()))
            {
                SendMessageContent(_inputChat.Trim());
                _inputChat = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawQuickEmojiDrawer()
        {
            GUILayout.BeginVertical(_drawerBoxStyle, GUILayout.Height(96));

            // Drawer Header with Sub-tabs
            GUILayout.BeginHorizontal(GUILayout.Height(20));
            GUIStyle tab0Style = (_quickDrawerTab == 0) ? _tabActiveStyle : _tabInactiveStyle;
            GUIStyle tab1Style = (_quickDrawerTab == 1) ? _tabActiveStyle : _tabInactiveStyle;

            if (GUILayout.Button("😀 趣味表情", tab0Style, GUILayout.Width(85), GUILayout.Height(20))) _quickDrawerTab = 0;
            if (GUILayout.Button("💬 常用短语", tab1Style, GUILayout.Width(85), GUILayout.Height(20))) _quickDrawerTab = 1;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(22), GUILayout.Height(18)))
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

        private void DrawPlayerSelectDropdown(float panelX, float panelY, float panelWidth)
        {
            float dropW = 210f;
            float dropH = 150f;
            float dropX = panelX + 30f;
            float dropY = panelY + 60f;

            Rect dropRect = new Rect(dropX, dropY, dropW, dropH);
            GUILayout.BeginArea(dropRect, _drawerBoxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#00E5FF><b>在线玩家列表</b></color>", _senderNameOtherStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(22), GUILayout.Height(20)))
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
                    if (kvp.Key == myId) continue;

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
                GUILayout.Label("<color=#90A4AE>暂无其他在线玩家\n可点击左侧[访客]测试</color>", _marqueeStyle);
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.MouseDown && !dropRect.Contains(Event.current.mousePosition))
            {
                _showPlayerSelectDropdown = false;
            }
        }

        private void SendMessageContent(string content)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

            if (_currentChannel == ChatChannel.Whisper)
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
                NetworkManager.Instance.SendChat(content);
            }
        }

        private void DrawCollapsedMiniBar()
        {
            float barW = Mathf.Clamp(Screen.width * 0.32f, 420f, 480f);
            float barH = 42f;
            float barX = 16f;
            float barY = Screen.height - barH - 16f;

            string previewText = "<color=#90A4AE>暂无新消息，点击展开交流...</color>";
            if (_messages.Count > 0)
            {
                var latest = _messages[_messages.Count - 1];
                string chTag = latest.Channel switch
                {
                    ChatChannel.World => "<color=#1E88E5>[世界]</color>",
                    ChatChannel.Nearby => "<color=#00E5FF>[附近]</color>",
                    ChatChannel.Whisper => "<color=#FF4081>[私聊]</color>",
                    ChatChannel.System => "<color=#76FF03>[系统]</color>",
                    _ => "<color=#00E5FF>[世界]</color>"
                };
                string sender = !string.IsNullOrEmpty(latest.SenderName) ? $"<b>{latest.SenderName}</b>: " : "";
                previewText = $"{chTag} {sender}{latest.Content}";
            }

            Rect barRect = new Rect(barX, barY, barW, barH);
            GUILayout.BeginArea(barRect, _miniBarStyle);
            GUILayout.BeginHorizontal();

            GUILayout.Label($"<size=12>💬 {previewText}</size>", _marqueeStyle, GUILayout.Height(28), GUILayout.MaxWidth(barW - 85));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("展开 ▶", _tabActiveStyle, GUILayout.Width(66), GUILayout.Height(24)))
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
    }
}
