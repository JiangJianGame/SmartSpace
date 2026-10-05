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

        // User Profile System State
        private bool _showInitialSetupModal = false;
        private bool _showSelfProfileModal = false;
        private int _selfProfileTab = 0; // 0: 名片查看, 1: 编辑资料
        private UserProfile _editingProfile = new UserProfile();
        private string _editingAgeStr = "";

        // Target Player Profile Modal (右键头像查看他人名片)
        private bool _showTargetProfileModal = false;
        private UserProfile _targetProfile = null;
        private string _targetProfileSessionId = "";

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

        // Profile & Modal Styles
        private GUIStyle _modalOverlayStyle;
        private GUIStyle _modalCardStyle;
        private GUIStyle _modalTitleStyle;
        private GUIStyle _modalLabelStyle;
        private GUIStyle _modalSubLabelStyle;
        private GUIStyle _modalInputStyle;
        private GUIStyle _modalPrimaryBtnStyle;
        private GUIStyle _modalSecondaryBtnStyle;
        private GUIStyle _playerBarBoxStyle;
        private GUIStyle _playerBarNameStyle;
        private GUIStyle _playerBarTagStyle;

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

        // Modal & Profile Textures
        private Texture2D _texModalOverlay;
        private Texture2D _texModalCardBg;
        private Texture2D _texInputDark;
        private Texture2D _texPlayerBarBg;

        // Procedural Avatars (预设 6 款个性化头像)
        private Texture2D[] _avatarTextures;
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
            // Initialize local profile
            var localProf = (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
                ? NetworkManager.Instance.LocalProfile
                : UserProfile.LoadFromPrefs();
            _editingProfile = localProf.Clone();
            _editingAgeStr = _editingProfile.age > 0 ? _editingProfile.age.ToString() : "";

            // If not saved previously, prompt the setup modal before joining space
            if (!UserProfile.HasSavedProfile())
            {
                _showInitialSetupModal = true;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged += HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived += HandleChatMessage;
                NetworkManager.Instance.OnChatMessageWithAvatarReceived += HandleChatMessageWithAvatar;
                NetworkManager.Instance.OnWhisperMessageReceived += HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
                NetworkManager.Instance.OnPlayerProfileChanged += HandlePlayerProfileChanged;
            }

            // Initial Welcome System Message
            AddSystemMessage("成功连接至奥拉通讯网络。当前频道已就绪！");
            AddSystemMessage("提示: 左上角可查看/修改个人信息 | 聊天界面右键头像可查看名片！");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
                NetworkManager.Instance.OnChatMessageWithAvatarReceived -= HandleChatMessageWithAvatar;
                NetworkManager.Instance.OnWhisperMessageReceived -= HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
                NetworkManager.Instance.OnPlayerProfileChanged -= HandlePlayerProfileChanged;
            }
            CleanupTextures();
        }

        private void Update()
        {
            // Toggle Expand/Collapse with 'C' key when not focused in any input
            bool isTyping = GUI.GetNameOfFocusedControl() == "AolaChatInputField" || _showInitialSetupModal || _showSelfProfileModal;
            if (Input.GetKeyDown(KeyCode.C) && !isTyping)
            {
                ToggleExpand();
            }

            // ESC to close open modals
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_showTargetProfileModal) _showTargetProfileModal = false;
                else if (_showSelfProfileModal) _showSelfProfileModal = false;
                else if (_showPlayerSelectDropdown) _showPlayerSelectDropdown = false;
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
            // Fallback if avatar id not dispatched
            HandleChatMessageWithAvatar(senderId, username, 0, message);
        }

        private void HandleChatMessageWithAvatar(string senderId, string username, int avatarId, string message)
        {
            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMe = (senderId == myId);

            int finalAvatar = isMe
                ? (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : avatarId)
                : avatarId;

            if (!isMe && NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(senderId, out var prof))
            {
                finalAvatar = prof.avatarId;
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.World,
                SenderId = senderId,
                SenderName = username,
                Content = message,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = isMe,
                Level = isMe ? 40 : (Math.Abs(senderId.GetHashCode() % 30) + 15),
                AvatarIndex = finalAvatar
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

            int avatarIdx = isMeSender
                ? (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : msg.senderAvatarId)
                : msg.senderAvatarId;

            if (!isMeSender && NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(msg.senderId, out var prof))
            {
                avatarIdx = prof.avatarId;
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
                AvatarIndex = avatarIdx
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

        private void HandlePlayerProfileChanged(string sessionId, UserProfile profile)
        {
            if (profile != null)
            {
                AddSystemMessage($"玩家 <color=#00E5FF><b>{profile.username}</b></color> 更新了个人资料。");
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
            if (_texModalOverlay != null) Destroy(_texModalOverlay);
            if (_texModalCardBg != null) Destroy(_texModalCardBg);
            if (_texInputDark != null) Destroy(_texInputDark);
            if (_texPlayerBarBg != null) Destroy(_texPlayerBarBg);

            if (_avatarTextures != null)
            {
                foreach (var t in _avatarTextures) if (t != null) Destroy(t);
            }
            if (_texAvatarOthers != null)
            {
                foreach (var t in _texAvatarOthers) if (t != null) Destroy(t);
            }
        }

        public Texture2D GetAvatarTex(int index)
        {
            if (_avatarTextures != null && _avatarTextures.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, _avatarTextures.Length - 1);
                if (_avatarTextures[safeIdx] != null) return _avatarTextures[safeIdx];
            }
            return _texAvatarSelf;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 1. Textures
            _texMainBg = MakeSolidTex(2, 2, new Color(0.04f, 0.08f, 0.16f, 0.90f));
            _texTabActive = MakeSolidTex(2, 2, new Color(0.12f, 0.48f, 0.88f, 1.0f)); // Bright Blue
            _texTabInactive = MakeSolidTex(2, 2, new Color(0.08f, 0.14f, 0.24f, 0.85f)); // Dark Slate Blue
            _texBubbleBlue = MakeBorderedTex(32, 32, new Color(0.08f, 0.44f, 0.82f, 0.96f), new Color(0.12f, 0.54f, 0.95f, 1f), 1);
            _texSendBtnYellow = MakeSolidTex(2, 2, new Color(1.0f, 0.86f, 0.18f, 1.0f)); // Golden-Yellow
            _texInputWhite = MakeSolidTex(2, 2, new Color(0.93f, 0.95f, 0.97f, 1.0f));
            _texEmojiDark = MakeSolidTex(2, 2, new Color(0.10f, 0.15f, 0.22f, 0.95f));
            _texDrawerBg = MakeSolidTex(2, 2, new Color(0.07f, 0.12f, 0.20f, 0.98f));
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.09f, 0.16f, 0.90f));
            _texAvatarBorder = MakeBorderedTex(42, 42, new Color(0.10f, 0.16f, 0.26f, 1f), new Color(0.20f, 0.55f, 0.95f, 1f), 2);
            _texSmileyIcon = MakeSmileyTex(34);

            // Modal & Profile Textures
            _texModalOverlay = MakeSolidTex(2, 2, new Color(0.02f, 0.04f, 0.08f, 0.78f)); // Dark backdrop
            _texModalCardBg = MakeBorderedTex(64, 64, new Color(0.06f, 0.10f, 0.19f, 0.98f), new Color(0.15f, 0.45f, 0.85f, 1.0f), 2);
            _texInputDark = MakeBorderedTex(32, 32, new Color(0.04f, 0.07f, 0.14f, 0.95f), new Color(0.22f, 0.38f, 0.62f, 1f), 1);
            _texPlayerBarBg = MakeBorderedTex(48, 48, new Color(0.05f, 0.09f, 0.18f, 0.92f), new Color(0.18f, 0.40f, 0.70f, 0.85f), 1);

            // 6 Distinct Procedural Avatars
            _avatarTextures = new Texture2D[UserProfile.AvatarNames.Length];
            for (int i = 0; i < _avatarTextures.Length; i++)
            {
                _avatarTextures[i] = MakeAvatarTex(UserProfile.AvatarPrimaryColors[i], UserProfile.AvatarSecondaryColors[i]);
            }

            _texAvatarSelf = _avatarTextures[0];
            _texAvatarOthers = new Texture2D[] { _avatarTextures[1], _avatarTextures[2], _avatarTextures[3] };

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
                normal = { textColor = new Color(1.0f, 0.85f, 0.35f) } // Golden-Yellow
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
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(1f, 0.88f, 0.25f) }
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
                normal = { textColor = new Color(0.46f, 1.0f, 0.01f) },
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
                normal = { textColor = new Color(0.12f, 0.12f, 0.12f), background = _texInputWhite },
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
                normal = { textColor = new Color(0.18f, 0.14f, 0.04f), background = _texSendBtnYellow },
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

            // Player Bar (Top Left)
            _playerBarBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texPlayerBarBg },
                padding = new RectOffset(8, 8, 8, 8)
            };

            _playerBarNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _playerBarTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                normal = { textColor = new Color(0.65f, 0.80f, 0.95f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

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

            // Modal Dialog Styles
            _modalOverlayStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texModalOverlay },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _modalCardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texModalCardBg },
                padding = new RectOffset(20, 20, 16, 16)
            };

            _modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(1.0f, 0.88f, 0.35f) }
            };

            _modalLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(0.75f, 0.90f, 1.0f) },
                padding = new RectOffset(0, 0, 2, 2)
            };

            _modalSubLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                normal = { textColor = new Color(0.60f, 0.70f, 0.82f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _modalInputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white, background = _texInputDark },
                focused = { textColor = Color.white, background = _texInputDark },
                padding = new RectOffset(8, 8, 4, 4)
            };

            _modalPrimaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.12f, 0.10f, 0.02f), background = _texSendBtnYellow },
                hover = { textColor = Color.black, background = _texSendBtnYellow }
            };

            _modalSecondaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.92f, 1.0f), background = _texTabInactive },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            _stylesInitialized = true;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            InitStyles();

            // 1. Top-Left Player Profile Bar (可查看与点击修改个人资料)
            DrawPlayerProfileBar();

            // 2. Chat Panel (2/3 Height Expanded OR Mini Collapsed Bar)
            if (_isExpanded)
            {
                DrawAolaStarChatWindow();
            }
            else
            {
                DrawCollapsedMiniBar();
            }

            // 3. Top-Level Modal Dialogs
            if (_showInitialSetupModal)
            {
                DrawInitialSetupModal();
            }
            else if (_showSelfProfileModal)
            {
                DrawSelfProfileModal();
            }
            else if (_showTargetProfileModal)
            {
                DrawTargetProfileModal();
            }
        }

        private void DrawPlayerProfileBar()
        {
            var localProf = (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
                ? NetworkManager.Instance.LocalProfile
                : _editingProfile;

            bool isConnected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            int playerCount = NetworkManager.Instance != null ? NetworkManager.Instance.PlayerCount : 0;
            Texture2D myAvatar = GetAvatarTex(localProf.avatarId);

            float barW = 320f;
            float barH = 72f;
            Rect barRect = new Rect(16, 16, barW, barH);

            GUILayout.BeginArea(barRect, _playerBarBoxStyle);
            GUILayout.BeginHorizontal();

            // Left Avatar (54x54) with level badge
            Rect avRect = GUILayoutUtility.GetRect(54, 54, GUILayout.Width(54), GUILayout.Height(54));
            GUI.DrawTexture(avRect, _texAvatarBorder);
            GUI.DrawTexture(new Rect(avRect.x + 3, avRect.y + 3, 48, 48), myAvatar);
            Rect lvlBarBadge = new Rect(avRect.x + 2, avRect.y + 36, 32, 15);
            GUI.DrawTexture(lvlBarBadge, _texMiniBarBg);
            GUI.Label(lvlBarBadge, $"Lv.{localProf.level}", _avatarLevelStyle);

            GUILayout.Space(8);

            // Right Info Column
            GUILayout.BeginVertical();

            // Row 1: Nickname + Gender Symbol + Edit Button
            GUILayout.BeginHorizontal();
            string gSymbol = UserProfile.GetGenderSymbol(localProf.gender);
            string gColor = UserProfile.GetGenderColor(localProf.gender);
            GUILayout.Label($"<b>{localProf.username}</b> <color={gColor}>{gSymbol}</color>", _playerBarNameStyle, GUILayout.Height(20));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✏️ 资料", _sideActionBtnStyle, GUILayout.Width(52), GUILayout.Height(20)))
            {
                OpenSelfProfileModal();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(1);

            // Row 2: Connection Status + Online Players
            string statusDot = isConnected ? "<color=#00E676>● 已连接</color>" : "<color=#FF5252>● 离线</color>";
            string pingText = isConnected ? "50ms" : "--";
            GUILayout.Label($"{statusDot} ({pingText}) | 广场: <color=#FFD54F><b>{playerCount}</b></color> 人", _playerBarTagStyle, GUILayout.Height(16));

            // Row 3: Bio snippet (clickable hint)
            string bioSnippet = string.IsNullOrEmpty(localProf.bio) ? "暂无签名，点击编辑名片" : localProf.bio;
            if (bioSnippet.Length > 16) bioSnippet = bioSnippet.Substring(0, 15) + "...";
            GUILayout.Label($"<color=#90A4AE><i>“{bioSnippet}”</i></color>", _playerBarTagStyle, GUILayout.Height(16));

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Clicking avatar in the profile bar also opens self profile center
            if (Event.current.type == EventType.MouseDown && new Rect(16, 16, 70, 72).Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                OpenSelfProfileModal();
            }
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

            // -------------------------------------------------------------
            // A. LEFT VERTICAL CHANNEL TAB BAR (奥拉星左侧频道导航栏)
            // -------------------------------------------------------------
            GUILayout.BeginArea(new Rect(0, 0, tabColWidth, panelHeight));
            GUILayout.BeginVertical();

            DrawVerticalChannelTab("世界", ChatChannel.World);
            DrawVerticalChannelTab("附近", ChatChannel.Nearby);
            DrawVerticalChannelTab("系统", ChatChannel.System);
            DrawVerticalChannelTab("私聊", ChatChannel.Whisper);

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // -------------------------------------------------------------
            // B. RIGHT CHAT CONTENT AREAS (顶部栏 / 消息滚动区 / 底部输入栏)
            // 采用绝对分层锚定，彻底杜绝流式布局累积误差，确保输入框在任何频道下位置绝对一致！
            // -------------------------------------------------------------
            float topHeaderH = 32f;
            float drawerH = _showQuickEmojiDrawer ? 96f : 0f;
            float inputBarH = 54f;
            float bottomReservedH = inputBarH + drawerH + 6f;
            float scrollH = panelHeight - topHeaderH - bottomReservedH;
            if (scrollH < 100f) scrollH = 100f;

            // B1. Top Header Bar (高度严格固定 32px，私聊目标与快捷操作同层并列)
            GUILayout.BeginArea(new Rect(tabColWidth, 0, chatAreaWidth, topHeaderH));
            DrawTopHeaderBar();
            GUILayout.EndArea();

            // B2. Scrollable Bubble Messages Area (高度严格限定在顶部栏与底部栏之间)
            GUILayout.BeginArea(new Rect(tabColWidth, topHeaderH, chatAreaWidth, scrollH));
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.Width(chatAreaWidth),
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
                    ChatChannel.Whisper => "<color=#78909C>暂无私聊消息。点击上方[选择目标]发起私聊...</color>",
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
            GUILayout.EndArea();

            // B3. Bottom Input Area (严格锚定在面板底部，输入框在任何频道下 Y 坐标绝对锁定！)
            float bottomAreaY = panelHeight - bottomReservedH;
            GUILayout.BeginArea(new Rect(tabColWidth, bottomAreaY, chatAreaWidth, bottomReservedH));
            GUILayout.BeginVertical();

            // Quick Emoji & Phrases Drawer (if opened)
            if (_showQuickEmojiDrawer)
            {
                DrawQuickEmojiDrawer();
                GUILayout.Space(2);
            }

            // Bottom Input Bar
            DrawBottomInputBar(chatAreaWidth);

            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUILayout.EndArea(); // Close panel main area

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
            GUILayout.Space(4);

            if (_currentChannel == ChatChannel.Whisper)
            {
                GUILayout.Label("<color=#FF4081><b>● 私聊</b></color>", _senderNameOtherStyle, GUILayout.Height(24));
                GUILayout.Space(4);

                if (!string.IsNullOrEmpty(_whisperTargetId))
                {
                    string targetBtnText = $"对: <color=#80D8FF><b>{_whisperTargetName}</b></color> ▼";
                    if (GUILayout.Button(targetBtnText, _sideActionBtnStyle, GUILayout.Height(22)))
                    {
                        _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                    }
                    if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(20), GUILayout.Height(22)))
                    {
                        _whisperTargetId = "";
                        _whisperTargetName = "";
                        _showPlayerSelectDropdown = false;
                    }
                }
                else
                {
                    if (GUILayout.Button("选择目标 ▼", _sideActionBtnStyle, GUILayout.Height(22)))
                    {
                        _showPlayerSelectDropdown = !_showPlayerSelectDropdown;
                    }
                }
            }
            else
            {
                string channelTitle = _currentChannel switch
                {
                    ChatChannel.World => "<color=#1E88E5><b>● 世界频道</b></color>",
                    ChatChannel.Nearby => "<color=#00E5FF><b>● 附近频道</b></color>",
                    ChatChannel.System => "<color=#76FF03><b>● 系统公告</b></color>",
                    _ => "<color=#80D8FF><b>● 综合</b></color>"
                };
                GUILayout.Label(channelTitle, _senderNameOtherStyle, GUILayout.Height(24));
            }

            GUILayout.FlexibleSpace();

            // 右上角操作按钮: 轮盘 / 访客 / 收起
            if (GUILayout.Button("🎡 轮盘", _sideActionBtnStyle, GUILayout.Width(58), GUILayout.Height(24)))
            {
                if (EmoteWheelUI.Instance != null) EmoteWheelUI.Instance.OpenWheel();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("🤖 访客", _sideActionBtnStyle, GUILayout.Width(58), GUILayout.Height(24)))
            {
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                {
                    NetworkManager.Instance.SpawnNetworkBot();
                }
            }

            GUILayout.Space(4);

            if (GUILayout.Button("◀ 收起", _sideActionBtnStyle, GUILayout.Width(54), GUILayout.Height(24)))
            {
                ToggleExpand();
            }

            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }

        private void DrawPlayerChatBubble(ChatBubbleItem msg, float contentWidth)
        {
            GUILayout.Space(6);

            Texture2D avatarTex = msg.IsSelf
                ? GetAvatarTex(NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : 0)
                : GetAvatarTex(msg.AvatarIndex);
            float maxBubbleW = Mathf.Clamp(contentWidth - 110f, 180f, 260f);

            Event curEvent = Event.current;

            if (!msg.IsSelf)
            {
                // ---------------------------------------------------------
                // OTHER PLAYERS: Left Avatar + Golden Name + Blue Bubble
                // ---------------------------------------------------------
                GUILayout.BeginHorizontal();

                // Avatar Box with Level (右键/左键点击查看该玩家名片)
                Rect avatarRect = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                bool isHover = avatarRect.Contains(curEvent.mousePosition);
                GUI.DrawTexture(avatarRect, _texAvatarBorder);
                GUI.DrawTexture(new Rect(avatarRect.x + 2, avatarRect.y + 2, 38, 38), avatarTex);
                Rect lvlBadge = new Rect(avatarRect.x + 1, avatarRect.y + 27, 24, 13);
                GUI.DrawTexture(lvlBadge, _texMiniBarBg);
                GUI.Label(lvlBadge, $"{msg.Level}", _avatarLevelStyle);

                if (curEvent.type == EventType.MouseDown && isHover)
                {
                    curEvent.Use();
                    OpenTargetProfileModal(msg.SenderId, msg.SenderName, msg.AvatarIndex);
                }

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

                // Avatar Box with Level (点击自己的头像打开个人资料中心)
                Rect avatarRect = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                bool isHover = avatarRect.Contains(curEvent.mousePosition);
                GUI.DrawTexture(avatarRect, _texAvatarBorder);
                GUI.DrawTexture(new Rect(avatarRect.x + 2, avatarRect.y + 2, 38, 38), avatarTex);
                Rect lvlBadge = new Rect(avatarRect.x + 17, avatarRect.y + 27, 24, 13);
                GUI.DrawTexture(lvlBadge, _texMiniBarBg);
                GUI.Label(lvlBadge, $"{msg.Level}", _avatarLevelStyle);

                if (curEvent.type == EventType.MouseDown && isHover)
                {
                    curEvent.Use();
                    OpenSelfProfileModal();
                }

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

        private void DrawBottomInputBar(float contentWidth)
        {
            float inputBarH = 50f;
            float rightBtnsW = 36f + 4f + 68f + 8f; // Emoji(36) + Sp(4) + Send(68) + Margins(8) = 116f
            float inputW = Mathf.Max(180f, contentWidth - rightBtnsW);

            GUILayout.BeginHorizontal(GUILayout.Height(inputBarH));

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

            // 1. White Rounded Input Box (固定舒适高度，绝对锁定位置，绝不抖动)
            GUI.SetNextControlName("AolaChatInputField");
            Rect inputRect = GUILayoutUtility.GetRect(inputW, inputBarH, GUILayout.Width(inputW), GUILayout.Height(inputBarH));
            _inputChat = GUI.TextArea(inputRect, _inputChat, 120, _inputFieldStyle);

            if (string.IsNullOrEmpty(_inputChat) && !isFocused)
            {
                string hint = "点击输入...";
                if (_currentChannel == ChatChannel.Whisper)
                {
                    hint = string.IsNullOrEmpty(_whisperTargetId) ? "点击输入 (请先在顶部选择目标)..." : $"对 [{_whisperTargetName}] 说...";
                }
                GUI.Label(new Rect(inputRect.x + 8, inputRect.y + 7, inputRect.width - 16, 20), hint, _placeholderStyle);
            }

            GUILayout.Space(4);

            // Right side buttons container (vertically centered with the tall input box)
            GUILayout.BeginVertical(GUILayout.Height(inputBarH));
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            // 2. Yellow Smiley Emoji Button (1:1 Procedural Icon matching reference image)
            if (GUILayout.Button(_texSmileyIcon, _emojiRoundBtnStyle, GUILayout.Width(36), GUILayout.Height(36)))
            {
                _showQuickEmojiDrawer = !_showQuickEmojiDrawer;
            }

            GUILayout.Space(4);

            // 3. Vibrant Golden-Yellow Send Button
            if (GUILayout.Button("发 送", _sendBtnStyle, GUILayout.Width(68), GUILayout.Height(36))
                && !string.IsNullOrEmpty(_inputChat.Trim()))
            {
                SendMessageContent(_inputChat.Trim());
                _inputChat = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
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
            float dropY = panelY + 34f;

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

        private void OpenSelfProfileModal()
        {
            var localProf = (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
                ? NetworkManager.Instance.LocalProfile
                : UserProfile.LoadFromPrefs();
            _editingProfile = localProf.Clone();
            _editingAgeStr = _editingProfile.age > 0 ? _editingProfile.age.ToString() : "";
            _selfProfileTab = 0;
            _showSelfProfileModal = true;
            _showTargetProfileModal = false;
        }

        private void OpenTargetProfileModal(string senderId, string senderName, int avatarIndex)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(senderId, out var prof))
            {
                _targetProfile = prof.Clone();
            }
            else
            {
                _targetProfile = new UserProfile
                {
                    username = senderName,
                    avatarId = avatarIndex,
                    gender = "secret",
                    age = 0,
                    bio = "这个玩家很神秘，还没有写个性签名~",
                    level = Math.Abs(senderId.GetHashCode() % 30) + 15
                };
            }
            _targetProfileSessionId = senderId;
            _showTargetProfileModal = true;
            _showSelfProfileModal = false;
        }

        private void DrawAvatarSelectorGrid(ref int selectedId)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < UserProfile.AvatarNames.Length; i++)
            {
                bool isSelected = (selectedId == i);
                Texture2D avTex = GetAvatarTex(i);

                GUILayout.BeginVertical(GUILayout.Width(56));

                // Avatar Icon Button
                Rect r = GUILayoutUtility.GetRect(50, 50, GUILayout.Width(50), GUILayout.Height(50));
                if (isSelected)
                {
                    GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, 54, 54), _texTabActive);
                }
                GUI.DrawTexture(r, _texAvatarBorder);
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 44, 44), avTex);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    selectedId = i;
                    Event.current.Use();
                }

                // Name label
                string colorTag = isSelected ? "#00E5FF" : "#90A4AE";
                GUILayout.Label($"<size=10><color={colorTag}><b>{UserProfile.AvatarNames[i]}</b></color></size>", _marqueeStyle, GUILayout.Width(56));

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInitialSetupModal()
        {
            // Full-screen backdrop overlay
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texModalOverlay);

            float cardW = 440f;
            float cardH = 490f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            GUILayout.BeginArea(new Rect(cardX, cardY, cardW, cardH), _modalCardStyle);
            GUILayout.BeginVertical();

            // Title
            GUILayout.Label("<size=16><b><color=#00E5FF>◆</color> 欢迎来到智慧空间 · 设置个人名片 <color=#00E5FF>◆</color></b></size>", _modalTitleStyle);
            GUILayout.Label("<color=#90A4AE>设置您的个性形象，让空间好友更好认识你！</color>", _marqueeStyle);
            GUILayout.Space(8);

            // 1. Avatar Selection
            GUILayout.Label("<b>1. 选择专属形象:</b>", _modalLabelStyle);
            int selectedAv = _editingProfile.avatarId;
            DrawAvatarSelectorGrid(ref selectedAv);
            _editingProfile.avatarId = selectedAv;
            GUILayout.Space(8);

            // 2. Nickname (Required)
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>2. 空间昵称 (公开展示):</b>", _modalLabelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🎲 随机", _sideActionBtnStyle, GUILayout.Width(52), GUILayout.Height(18)))
            {
                _editingProfile.username = UserProfile.GenerateRandomNickname();
            }
            GUILayout.EndHorizontal();

            _editingProfile.username = GUILayout.TextField(_editingProfile.username, 14, _modalInputStyle, GUILayout.Height(28));
            GUILayout.Space(8);

            // 3. Gender & Age
            GUILayout.BeginHorizontal();

            // Gender
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label("<b>3. 性别:</b>", _modalLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("♂ 男生", _editingProfile.gender == "male" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "male";
            if (GUILayout.Button("♀ 女生", _editingProfile.gender == "female" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "female";
            if (GUILayout.Button("✦ 保密", _editingProfile.gender == "secret" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "secret";
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Age (Optional/Private)
            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label("<b>4. 年龄 <color=#90A4AE>(选填/保密)</color>:</b>", _modalLabelStyle);
            _editingAgeStr = GUILayout.TextField(_editingAgeStr, 3, _modalInputStyle, GUILayout.Height(26));
            if (int.TryParse(_editingAgeStr, out int parsedAge))
            {
                _editingProfile.age = Mathf.Clamp(parsedAge, 0, 120);
            }
            else
            {
                _editingProfile.age = 0;
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // 5. Bio (Optional)
            GUILayout.Label("<b>5. 个性签名 <color=#90A4AE>(选填，介绍一下自己吧~)</color>:</b>", _modalLabelStyle);
            _editingProfile.bio = GUILayout.TextField(_editingProfile.bio, 50, _modalInputStyle, GUILayout.Height(28));
            GUILayout.Space(16);

            // Confirm Join Button
            if (GUILayout.Button("🚀  开启智慧空间之旅（进入大空间）", _modalPrimaryBtnStyle, GUILayout.Height(42)))
            {
                if (string.IsNullOrEmpty(_editingProfile.username.Trim()))
                {
                    _editingProfile.username = UserProfile.GenerateRandomNickname();
                }

                _editingProfile.SaveToPrefs();
                _showInitialSetupModal = false;

                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.Connect(_editingProfile);
                }
                AddSystemMessage($"欢迎 <color=#FFD54F><b>{_editingProfile.username}</b></color> 加入智慧空间！✨");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawSelfProfileModal()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texModalOverlay);

            float cardW = 440f;
            float cardH = 470f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            GUILayout.BeginArea(new Rect(cardX, cardY, cardW, cardH), _modalCardStyle);
            GUILayout.BeginVertical();

            // Modal Header with Tabs
            GUILayout.BeginHorizontal();
            GUIStyle tab0Style = (_selfProfileTab == 0) ? _tabActiveStyle : _tabInactiveStyle;
            GUIStyle tab1Style = (_selfProfileTab == 1) ? _tabActiveStyle : _tabInactiveStyle;

            if (GUILayout.Button("🪪 个人名片", tab0Style, GUILayout.Width(110), GUILayout.Height(28))) _selfProfileTab = 0;
            if (GUILayout.Button("✏️ 修改资料", tab1Style, GUILayout.Width(110), GUILayout.Height(28))) _selfProfileTab = 1;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(26), GUILayout.Height(24)))
            {
                _showSelfProfileModal = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            var localProf = (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
                ? NetworkManager.Instance.LocalProfile
                : _editingProfile;

            if (_selfProfileTab == 0)
            {
                // -------------------------------------------------------------
                // VIEW MODE (个人名片查看)
                // -------------------------------------------------------------
                GUILayout.BeginHorizontal();

                // Large Avatar (64x64)
                Rect avRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                GUI.DrawTexture(avRect, _texAvatarBorder);
                GUI.DrawTexture(new Rect(avRect.x + 3, avRect.y + 3, 58, 58), GetAvatarTex(localProf.avatarId));
                Rect lvlModalBadge = new Rect(avRect.x + 3, avRect.y + 45, 34, 15);
                GUI.DrawTexture(lvlModalBadge, _texMiniBarBg);
                GUI.Label(lvlModalBadge, $"Lv.{localProf.level}", _avatarLevelStyle);

                GUILayout.Space(14);

                // Right Info Column
                GUILayout.BeginVertical();
                string gSymbol = UserProfile.GetGenderSymbol(localProf.gender);
                string gColor = UserProfile.GetGenderColor(localProf.gender);
                GUILayout.Label($"<size=17><b>{localProf.username}</b></size> <size=15><color={gColor}>{gSymbol}</color></size>", _playerBarNameStyle);
                GUILayout.Space(2);

                string mySessionId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "未连接";
                GUILayout.Label($"<color=#90A4AE>UID / 会话: </color><color=#80D8FF>{mySessionId}</color>", _playerBarTagStyle);
                GUILayout.Label($"<color=#90A4AE>形象预设: </color><color=#FFD54F>{UserProfile.AvatarNames[Mathf.Clamp(localProf.avatarId, 0, UserProfile.AvatarNames.Length - 1)]}</color>", _playerBarTagStyle);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                GUILayout.Space(14);

                // Detail Attributes Card
                GUILayout.BeginVertical(_systemNoticeBoxStyle);
                GUILayout.Label($"<b>性别:</b>  <color={gColor}>{UserProfile.GetGenderLabel(localProf.gender)} {gSymbol}</color>", _modalLabelStyle);
                GUILayout.Label($"<b>年龄:</b>  <color=#80D8FF>{UserProfile.GetAgeDisplay(localProf.age)}</color>", _modalLabelStyle);
                GUILayout.Label($"<b>常驻频道:</b>  <color=#00E5FF>智慧空间·漫游广场</color>", _modalLabelStyle);
                GUILayout.EndVertical();

                GUILayout.Space(10);

                // Bio Quote Card
                GUILayout.Label("<b>个性签名:</b>", _modalLabelStyle);
                string bioText = string.IsNullOrEmpty(localProf.bio) ? "暂无个性签名，快去完善吧~" : localProf.bio;
                GUILayout.BeginVertical(_drawerBoxStyle);
                GUILayout.Label($"<color=#E0F7FA><i>“{bioText}”</i></color>", _systemContentStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Bottom Action Buttons
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("✏️  修改个人资料", _modalPrimaryBtnStyle, GUILayout.Height(38)))
                {
                    _editingProfile = localProf.Clone();
                    _editingAgeStr = _editingProfile.age > 0 ? _editingProfile.age.ToString() : "";
                    _selfProfileTab = 1;
                }
                GUILayout.Space(8);
                if (GUILayout.Button("关 闭", _modalSecondaryBtnStyle, GUILayout.Width(90), GUILayout.Height(38)))
                {
                    _showSelfProfileModal = false;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // -------------------------------------------------------------
                // EDIT MODE (资料修改编辑)
                // -------------------------------------------------------------
                GUILayout.Label("<b>更换专属头像:</b>", _modalLabelStyle);
                int selAv = _editingProfile.avatarId;
                DrawAvatarSelectorGrid(ref selAv);
                _editingProfile.avatarId = selAv;
                GUILayout.Space(6);

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>修改昵称:</b>", _modalLabelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("🎲 随机", _sideActionBtnStyle, GUILayout.Width(52), GUILayout.Height(18)))
                {
                    _editingProfile.username = UserProfile.GenerateRandomNickname();
                }
                GUILayout.EndHorizontal();
                _editingProfile.username = GUILayout.TextField(_editingProfile.username, 14, _modalInputStyle, GUILayout.Height(28));
                GUILayout.Space(6);

                // Gender & Age Row
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(220));
                GUILayout.Label("<b>性别:</b>", _modalLabelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("♂ 男生", _editingProfile.gender == "male" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "male";
                if (GUILayout.Button("♀ 女生", _editingProfile.gender == "female" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "female";
                if (GUILayout.Button("✦ 保密", _editingProfile.gender == "secret" ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26))) _editingProfile.gender = "secret";
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.Space(12);

                GUILayout.BeginVertical(GUILayout.Width(160));
                GUILayout.Label("<b>年龄 <color=#90A4AE>(选填/保密)</color>:</b>", _modalLabelStyle);
                _editingAgeStr = GUILayout.TextField(_editingAgeStr, 3, _modalInputStyle, GUILayout.Height(26));
                if (int.TryParse(_editingAgeStr, out int parsedAge))
                {
                    _editingProfile.age = Mathf.Clamp(parsedAge, 0, 120);
                }
                else
                {
                    _editingProfile.age = 0;
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(6);

                GUILayout.Label("<b>个性签名:</b>", _modalLabelStyle);
                _editingProfile.bio = GUILayout.TextField(_editingProfile.bio, 50, _modalInputStyle, GUILayout.Height(28));

                GUILayout.FlexibleSpace();

                // Save or Cancel Buttons
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("💾  保存修改并同步", _modalPrimaryBtnStyle, GUILayout.Height(38)))
                {
                    if (string.IsNullOrEmpty(_editingProfile.username.Trim()))
                    {
                        _editingProfile.username = UserProfile.GenerateRandomNickname();
                    }

                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateProfile(_editingProfile);
                    }
                    _selfProfileTab = 0;
                }
                GUILayout.Space(8);
                if (GUILayout.Button("取 消", _modalSecondaryBtnStyle, GUILayout.Width(90), GUILayout.Height(38)))
                {
                    _selfProfileTab = 0;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawTargetProfileModal()
        {
            if (_targetProfile == null)
            {
                _showTargetProfileModal = false;
                return;
            }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texModalOverlay);

            float cardW = 420f;
            float cardH = 430f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            GUILayout.BeginArea(new Rect(cardX, cardY, cardW, cardH), _modalCardStyle);
            GUILayout.BeginVertical();

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=15><b><color=#00E5FF>◆</color> 玩家个人名片 <color=#00E5FF>◆</color></b></size>", _senderNameOtherStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(26), GUILayout.Height(24)))
            {
                _showTargetProfileModal = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Target Player Avatar & Basic Info
            GUILayout.BeginHorizontal();

            Rect avRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
            GUI.DrawTexture(avRect, _texAvatarBorder);
            GUI.DrawTexture(new Rect(avRect.x + 3, avRect.y + 3, 58, 58), GetAvatarTex(_targetProfile.avatarId));
            Rect lvlModalBadge = new Rect(avRect.x + 3, avRect.y + 45, 34, 15);
            GUI.DrawTexture(lvlModalBadge, _texMiniBarBg);
            GUI.Label(lvlModalBadge, $"Lv.{_targetProfile.level}", _avatarLevelStyle);

            GUILayout.Space(14);

            GUILayout.BeginVertical();
            string gSymbol = UserProfile.GetGenderSymbol(_targetProfile.gender);
            string gColor = UserProfile.GetGenderColor(_targetProfile.gender);
            GUILayout.Label($"<size=17><b>{_targetProfile.username}</b></size> <size=15><color={gColor}>{gSymbol}</color></size>", _playerBarNameStyle);
            GUILayout.Space(2);

            GUILayout.Label($"<color=#90A4AE>会话 ID: </color><color=#80D8FF>{_targetProfileSessionId}</color>", _playerBarTagStyle);
            GUILayout.Label($"<color=#90A4AE>形象预设: </color><color=#FFD54F>{UserProfile.AvatarNames[Mathf.Clamp(_targetProfile.avatarId, 0, UserProfile.AvatarNames.Length - 1)]}</color>", _playerBarTagStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Attributes Card
            GUILayout.BeginVertical(_systemNoticeBoxStyle);
            GUILayout.Label($"<b>性别:</b>  <color={gColor}>{UserProfile.GetGenderLabel(_targetProfile.gender)} {gSymbol}</color>", _modalLabelStyle);
            GUILayout.Label($"<b>年龄:</b>  <color=#80D8FF>{UserProfile.GetAgeDisplay(_targetProfile.age)}</color>", _modalLabelStyle);
            GUILayout.Label($"<b>状态:</b>  <color=#00E676>● 正在智慧空间广场漫游</color>", _modalLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Bio
            GUILayout.Label("<b>个性签名:</b>", _modalLabelStyle);
            string bioText = string.IsNullOrEmpty(_targetProfile.bio) ? "这个玩家很神秘，还没有写个性签名~" : _targetProfile.bio;
            GUILayout.BeginVertical(_drawerBoxStyle);
            GUILayout.Label($"<color=#E0F7FA><i>“{bioText}”</i></color>", _systemContentStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Bottom Actions: Whisper or Close
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💬  发起私聊", _modalPrimaryBtnStyle, GUILayout.Height(38)))
            {
                _whisperTargetId = _targetProfileSessionId;
                _whisperTargetName = _targetProfile.username;
                _currentChannel = ChatChannel.Whisper;
                _showTargetProfileModal = false;
                _isExpanded = true;
                _shouldScrollToBottom = true;
            }

            GUILayout.Space(8);

            if (GUILayout.Button("关 闭", _modalSecondaryBtnStyle, GUILayout.Width(90), GUILayout.Height(38)))
            {
                _showTargetProfileModal = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
}

