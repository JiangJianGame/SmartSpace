using System;
using System.Collections.Generic;
using UnityEngine;
using SmartSpace.Network;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    public enum ChatChannel
    {
        World = 0,        // 世界 (玩家发言)
        Nearby = 1,       // 附近 (区域同屏)
        System = 2,       // 系统 (系统文本)
        Whisper = 3,      // 私聊 (私聊记录)
        Horn = 4,         // 大喇叭 (全服喇叭)
        Achievement = 5,  // 成就播报 (荣誉广播)
        Friend = 6        // 好友 (好友聊天)
    }

    [Serializable]
    public class ChatContact
    {
        public string id;
        public string name;
        public int avatarIndex;
        public string gender; // "male", "female", "secret"
        public int level;
        public bool isOnline;
        public string bio;
    }

    [Serializable]
    public class ChatBubbleItem
    {
        public ChatChannel Channel;
        public string SenderId;
        public string SenderName;
        public string TargetId;
        public string TargetName;
        public string Title;       // 成就称号 / 喇叭主题
        public string Content;
        public string TimeStr;
        public bool IsSelf;
        public int Level;
        public int AvatarIndex;
        public long Timestamp;
    }

    [Serializable]
    public class ChatHistoryWrapper
    {
        public List<ChatBubbleItem> items = new List<ChatBubbleItem>();
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
        private const string PREF_CHAT_HISTORY = "AolaStar_ChatHistory_Cache";
        private static readonly List<ChatBubbleItem> s_sharedMessages = new List<ChatBubbleItem>();

        [Header("Chat Settings")]
        [SerializeField] private bool defaultExpanded = true;
        [SerializeField] private int maxHistoryCount = 200;

        public static bool IsTyping { get; private set; }
        private bool _isChatInputFocused = false;

        private bool _isExpanded = true;
        private ChatChannel _currentChannel = ChatChannel.World;
        private string _inputChat = "";
        private Vector2 _scrollPosition = Vector2.zero;
        private bool _shouldScrollToBottom = true;
        private bool _isNetworkEventsSubscribed = false;

        // Whisper & Friend 3-Column State
        private string _whisperTargetId = "";
        private string _whisperTargetName = "";
        private bool _showPlayerSelectDropdown = false;

        private readonly List<ChatContact> _friendsList = new List<ChatContact>();
        private readonly List<ChatContact> _whisperContacts = new List<ChatContact>();
        private string _selectedFriendId = "friend_xiuzhu";
        private string _selectedFriendName = "休竹";
        private Vector2 _contactScrollPosition = Vector2.zero;

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

        // Horn & Achievement Styles
        private GUIStyle _hornBoxStyle;
        private GUIStyle _hornTagStyle;
        private GUIStyle _hornContentStyle;
        private GUIStyle _achievementBoxStyle;
        private GUIStyle _achievementTagStyle;
        private GUIStyle _achievementContentStyle;

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

        // Middle Contact Column Styles (三段式布局联系人列表)
        private GUIStyle _middleColStyle;
        private GUIStyle _contactHeaderStyle;
        private GUIStyle _contactSelectedStyle;
        private GUIStyle _contactNormalStyle;
        private GUIStyle _contactNameStyle;
        private GUIStyle _contactStatusStyle;

        private Texture2D _texMiddleColBg;
        private Texture2D _texContactSelected;
        private Texture2D _texContactNormal;
        private Texture2D _texContactHover;

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
        private Texture2D _texHornBg;
        private Texture2D _texAchievementBg;

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
            InitDefaultFriends();
            InitDefaultWhisperContacts();
        }

        private void InitDefaultFriends()
        {
            if (_friendsList.Count > 0) return;

            // 1:1 matching user screenshot friends list:
            _friendsList.Add(new ChatContact { id = "friend_yantou", name = "占得人间一味愚", avatarIndex = 5, gender = "female", level = 38, isOnline = true, bio = "星际探索进行时" });
            _friendsList.Add(new ChatContact { id = "friend_xiuzhu", name = "休竹", avatarIndex = 1, gender = "male", level = 40, isOnline = true, bio = "光芒万丈，勇往直前！" });
            _friendsList.Add(new ChatContact { id = "friend_liushuohan", name = "刘硕涵", avatarIndex = 4, gender = "male", level = 35, isOnline = true, bio = "战队集结，共赴星空！" });
            _friendsList.Add(new ChatContact { id = "friend_huajianxue", name = "花间雪", avatarIndex = 2, gender = "female", level = 42, isOnline = true, bio = "在奥拉星智慧空间漫游~" });
            _friendsList.Add(new ChatContact { id = "friend_liyuefeng", name = "李岳锋", avatarIndex = 3, gender = "male", level = 33, isOnline = true, bio = "热爱科技与智慧空间" });
            _friendsList.Add(new ChatContact { id = "friend_xiaoaola", name = "小奥拉", avatarIndex = 0, gender = "male", level = 37, isOnline = true, bio = "很高兴与你成为好友！" });
            _friendsList.Add(new ChatContact { id = "friend_linqiaobei", name = "林桥北", avatarIndex = 1, gender = "male", level = 30, isOnline = true, bio = "探索无限可能" });
            _friendsList.Add(new ChatContact { id = "friend_afei", name = "多情的士兵阿飞", avatarIndex = 4, gender = "male", level = 29, isOnline = true, bio = "守护奥拉星和平！" });

            if (string.IsNullOrEmpty(_selectedFriendId))
            {
                _selectedFriendId = "friend_xiuzhu";
                _selectedFriendName = "休竹";
            }

            // Seed initial greeting message from 休竹 if no friend messages exist
            if (!_messages.Exists(m => m.Channel == ChatChannel.Friend))
            {
                AddBubbleMessage(new ChatBubbleItem
                {
                    Channel = ChatChannel.Friend,
                    SenderId = "friend_xiuzhu",
                    SenderName = "休竹",
                    TargetId = "self",
                    TargetName = "我",
                    Content = "你好呀！很高兴在奥拉星智慧空间与你成为好友！🌟",
                    TimeStr = "12:10",
                    IsSelf = false,
                    AvatarIndex = 1,
                    Level = 40,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 600000
                }, false);
            }
        }

        private void InitDefaultWhisperContacts()
        {
            if (_whisperContacts.Count > 0) return;

            _whisperContacts.Add(new ChatContact { id = "bot_xiaoaola", name = "小奥拉", avatarIndex = 0, gender = "male", level = 37, isOnline = true, bio = "智慧空间向导" });
            _whisperContacts.Add(new ChatContact { id = "friend_xiuzhu", name = "休竹", avatarIndex = 1, gender = "male", level = 40, isOnline = true, bio = "光芒万丈，勇往直前！" });

            if (string.IsNullOrEmpty(_whisperTargetId))
            {
                _whisperTargetId = "bot_xiaoaola";
                _whisperTargetName = "小奥拉";
            }

            // Seed initial whisper message if none exist
            if (!_messages.Exists(m => m.Channel == ChatChannel.Whisper))
            {
                AddBubbleMessage(new ChatBubbleItem
                {
                    Channel = ChatChannel.Whisper,
                    SenderId = "bot_xiaoaola",
                    SenderName = "小奥拉",
                    TargetId = "self",
                    TargetName = "我",
                    Content = "Hi！我是智慧空间向导小奥拉，随时可以向我发起私信哦！👋",
                    TimeStr = "12:12",
                    IsSelf = false,
                    AvatarIndex = 0,
                    Level = 37,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 500000
                }, false);
            }
        }

        private void SyncOnlinePlayersToContacts()
        {
            if (NetworkManager.Instance == null) return;
            string myId = NetworkManager.Instance.SessionId;

            foreach (var kvp in NetworkManager.Instance.OnlinePlayers)
            {
                if (kvp.Key == myId) continue;

                if (!_whisperContacts.Exists(c => c.id == kvp.Key))
                {
                    int avId = 1;
                    string gender = "male";
                    int lvl = 1;
                    if (NetworkManager.Instance.OnlineProfiles.TryGetValue(kvp.Key, out var prof))
                    {
                        avId = prof.avatarId;
                        gender = prof.gender;
                        lvl = prof.level;
                    }

                    _whisperContacts.Add(new ChatContact
                    {
                        id = kvp.Key,
                        name = kvp.Value,
                        avatarIndex = avId,
                        gender = gender,
                        level = lvl,
                        isOnline = true,
                        bio = "在线玩家"
                    });
                }
            }
        }

        public void AddFriendFromProfile(string sessionId, UserProfile profile)
        {
            if (profile == null) return;
            if (_friendsList.Exists(f => f.id == sessionId || f.name == profile.username))
            {
                AddSystemMessage($"<color=#FFD54F>玩家 [{profile.username}] 已经在你的好友列表中啦！</color>");
                return;
            }

            _friendsList.Insert(0, new ChatContact
            {
                id = sessionId,
                name = profile.username,
                avatarIndex = profile.avatarId,
                gender = profile.gender,
                level = profile.level,
                isOnline = true,
                bio = profile.bio
            });

            AddSystemMessage($"<color=#00E676>已成功将 [{profile.username}] 添加为好友！</color>");
        }

        private System.Collections.IEnumerator SimulateFriendReplyRoutine(string friendId, string friendName)
        {
            yield return new WaitForSeconds(1.2f);

            string[] replies = friendName switch
            {
                "休竹" => new string[]
                {
                    "收到！今天广场真热闹，要一起去漫游探险吗？🚀",
                    "光芒万丈，勇往直前！在奥拉星智慧空间一起冲！✨",
                    "哈哈，很高兴和你成为好友，随时找我玩呀！🌟"
                },
                "花间雪" => new string[]
                {
                    "哇，收到你的消息啦！今天空间漫游感觉超棒的～🌸",
                    "你好你好！广场风景超赞的，随时来找我聊天哦～😊"
                },
                "刘硕涵" => new string[]
                {
                    "战队集结完毕，随时准备出发！🔥",
                    "收到消息！空间里的互动设施体验过了吗？很赞！👍"
                },
                "占得人间一味愚" => new string[]
                {
                    "星际探索中，收到你的好友问候啦！💫",
                    "愿我们在智慧空间探索更多奇迹！✨"
                },
                _ => new string[]
                {
                    "收到你的好友消息啦！很高兴和你聊天～👋",
                    "在智慧空间漫游真有趣，改天一起打卡拍照呀！📸"
                }
            };
            string reply = replies[UnityEngine.Random.Range(0, replies.Length)];

            int avId = 1;
            int lvl = 40;
            var contact = _friendsList.Find(c => c.id == friendId);
            if (contact != null)
            {
                avId = contact.avatarIndex;
                lvl = contact.level;
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Friend,
                SenderId = friendId,
                SenderName = friendName,
                TargetId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "self",
                TargetName = NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.username : "我",
                Content = reply,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                AvatarIndex = avId,
                Level = lvl,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void OnEnable()
        {
            SubscribeNetworkEvents();
            LoadHistory();

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                NetworkManager.Instance.RequestChatHistory();
            }
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

            SubscribeNetworkEvents();
            LoadHistory();
        }

        private void OnDisable()
        {
            UnsubscribeNetworkEvents();
            SaveHistory();
        }

        private void OnDestroy()
        {
            UnsubscribeNetworkEvents();
            SaveHistory();
            CleanupTextures();
        }

        private void SubscribeNetworkEvents()
        {
            if (_isNetworkEventsSubscribed) return;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged += HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageWithAvatarReceived += HandleChatMessageWithAvatar;
                NetworkManager.Instance.OnHornMessageReceived += HandleHornMessage;
                NetworkManager.Instance.OnAchievementMessageReceived += HandleAchievementMessage;
                NetworkManager.Instance.OnChatHistoryReceived += HandleChatHistoryFromServer;
                NetworkManager.Instance.OnWhisperMessageReceived += HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
                NetworkManager.Instance.OnPlayerProfileChanged += HandlePlayerProfileChanged;
                _isNetworkEventsSubscribed = true;
            }
        }

        private void UnsubscribeNetworkEvents()
        {
            if (!_isNetworkEventsSubscribed) return;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.OnChatMessageWithAvatarReceived -= HandleChatMessageWithAvatar;
                NetworkManager.Instance.OnHornMessageReceived -= HandleHornMessage;
                NetworkManager.Instance.OnAchievementMessageReceived -= HandleAchievementMessage;
                NetworkManager.Instance.OnChatHistoryReceived -= HandleChatHistoryFromServer;
                NetworkManager.Instance.OnWhisperMessageReceived -= HandleWhisperMessage;
                NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
                NetworkManager.Instance.OnPlayerProfileChanged -= HandlePlayerProfileChanged;
            }
            _isNetworkEventsSubscribed = false;
        }

        private void Update()
        {
            // If NetworkManager was created after DemoHUD, subscribe to events once available
            if (!_isNetworkEventsSubscribed && NetworkManager.Instance != null)
            {
                SubscribeNetworkEvents();
            }

            SyncOnlinePlayersToContacts();

            // Toggle Expand/Collapse with 'C' key when not focused in any input
            if (Input.GetKeyDown(KeyCode.C) && !IsTyping)
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
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.RequestChatHistory();
                }
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
                AvatarIndex = finalAvatar,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void HandleHornMessage(HornMessageBroadcast hornMsg)
        {
            if (hornMsg == null || string.IsNullOrEmpty(hornMsg.message)) return;

            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMe = (hornMsg.senderId == myId);

            int finalAvatar = isMe
                ? (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : hornMsg.avatarId)
                : hornMsg.avatarId;

            if (!isMe && NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(hornMsg.senderId, out var prof))
            {
                finalAvatar = prof.avatarId;
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Horn,
                SenderId = hornMsg.senderId,
                SenderName = hornMsg.username,
                Content = hornMsg.message,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = isMe,
                Level = isMe ? 40 : 25,
                AvatarIndex = finalAvatar,
                Timestamp = (long)hornMsg.timestamp
            });
        }

        private void HandleAchievementMessage(AchievementMessageBroadcast achMsg)
        {
            if (achMsg == null || string.IsNullOrEmpty(achMsg.title)) return;

            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";
            bool isMe = (achMsg.senderId == myId);

            int finalAvatar = isMe
                ? (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : achMsg.avatarId)
                : achMsg.avatarId;

            if (!isMe && NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(achMsg.senderId, out var prof))
            {
                finalAvatar = prof.avatarId;
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Achievement,
                SenderId = achMsg.senderId,
                SenderName = achMsg.username,
                Title = achMsg.title,
                Content = string.IsNullOrEmpty(achMsg.desc) ? "在智慧空间广场达成了荣誉挑战！" : achMsg.desc,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = isMe,
                Level = isMe ? 40 : 25,
                AvatarIndex = finalAvatar,
                Timestamp = (long)achMsg.timestamp
            });
        }

        private void HandleChatHistoryFromServer(ChatMessageBroadcast[] serverHistory)
        {
            if (serverHistory == null || serverHistory.Length == 0) return;

            string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "";

            foreach (var sMsg in serverHistory)
            {
                if (sMsg == null || string.IsNullOrEmpty(sMsg.message)) continue;

                bool isMe = (sMsg.senderId == myId);
                int finalAvatar = isMe
                    ? (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.avatarId : sMsg.avatarId)
                    : sMsg.avatarId;

                string time = (sMsg.timestamp > 0)
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)sMsg.timestamp).ToLocalTime().ToString("HH:mm")
                    : DateTime.Now.ToString("HH:mm");

                ChatChannel ch = ChatChannel.World;
                if (sMsg.channel == "horn") ch = ChatChannel.Horn;
                else if (sMsg.channel == "achievement") ch = ChatChannel.Achievement;
                else if (sMsg.channel == "system") ch = ChatChannel.System;

                AddBubbleMessage(new ChatBubbleItem
                {
                    Channel = ch,
                    SenderId = sMsg.senderId,
                    SenderName = sMsg.username,
                    Title = sMsg.title,
                    Content = sMsg.message,
                    TimeStr = time,
                    IsSelf = isMe,
                    Level = isMe ? 40 : (Math.Abs(sMsg.senderId.GetHashCode() % 30) + 15),
                    AvatarIndex = finalAvatar,
                    Timestamp = (long)sMsg.timestamp
                }, saveToDisk: false);
            }

            SaveHistory();
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
                AvatarIndex = avatarIdx,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
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
            // 修改个人资料不需要在世界公屏播报，静默同步即可
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
                Level = 99,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void AddBubbleMessage(ChatBubbleItem item, bool saveToDisk = true)
        {
            if (item == null) return;
            if (string.IsNullOrEmpty(item.Content) && string.IsNullOrEmpty(item.Title)) return;

            // Deduplicate: check if an identical message exists in recent messages
            int checkStart = Mathf.Max(0, _messages.Count - 8);
            for (int i = _messages.Count - 1; i >= checkStart; i--)
            {
                var existing = _messages[i];
                if (existing.SenderId == item.SenderId &&
                    existing.Content == item.Content &&
                    existing.Title == item.Title &&
                    existing.Channel == item.Channel &&
                    existing.TimeStr == item.TimeStr)
                {
                    return; // Skip duplicate message
                }
            }

            _messages.Add(item);

            if (!s_sharedMessages.Contains(item))
            {
                s_sharedMessages.Add(item);
            }

            if (_messages.Count > maxHistoryCount)
            {
                _messages.RemoveAt(0);
            }
            if (s_sharedMessages.Count > maxHistoryCount)
            {
                s_sharedMessages.RemoveAt(0);
            }

            _shouldScrollToBottom = true;

            if (saveToDisk)
            {
                SaveHistory();
            }
        }

        private void LoadHistory()
        {
            if (_messages.Count > 0) return;

            // 1. Sync from static in-memory list first (prevents loss during soft scene reloads)
            if (s_sharedMessages.Count > 0)
            {
                s_sharedMessages.RemoveAll(item => 
                    item.Channel == ChatChannel.System && 
                    (item.Content.Contains("更新了个人资料") || item.Content.Contains("修改了个人资料"))
                );
                _messages.AddRange(s_sharedMessages);
                _shouldScrollToBottom = true;
                return;
            }

            // 2. Load from PlayerPrefs persistence (prevents loss after domain reload / restarts)
            if (PlayerPrefs.HasKey(PREF_CHAT_HISTORY))
            {
                string json = PlayerPrefs.GetString(PREF_CHAT_HISTORY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var wrapper = JsonUtility.FromJson<ChatHistoryWrapper>(json);
                        if (wrapper != null && wrapper.items != null && wrapper.items.Count > 0)
                        {
                            wrapper.items.RemoveAll(item => 
                                item.Channel == ChatChannel.System && 
                                (item.Content.Contains("更新了个人资料") || item.Content.Contains("修改了个人资料"))
                            );

                            _messages.AddRange(wrapper.items);
                            s_sharedMessages.Clear();
                            s_sharedMessages.AddRange(wrapper.items);
                            _shouldScrollToBottom = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DemoHUD] Error restoring chat history: {ex.Message}");
                    }
                }
            }

            // 3. Fallback welcome message if brand new
            AddSystemMessage("成功连接至奥拉通讯网络。当前频道已就绪！");
            AddSystemMessage("提示: 左上角可查看/修改个人信息 | 聊天界面右键头像可查看名片！");
        }

        private void SaveHistory()
        {
            if (_messages.Count == 0) return;

            try
            {
                var wrapper = new ChatHistoryWrapper();
                int takeCount = Mathf.Min(_messages.Count, 150);
                int startIndex = _messages.Count - takeCount;
                for (int i = startIndex; i < _messages.Count; i++)
                {
                    wrapper.items.Add(_messages[i]);
                }
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(PREF_CHAT_HISTORY, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DemoHUD] Error saving chat history: {ex.Message}");
            }
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
            if (_texHornBg != null) Destroy(_texHornBg);
            if (_texAchievementBg != null) Destroy(_texAchievementBg);
            if (_texModalOverlay != null) Destroy(_texModalOverlay);
            if (_texModalCardBg != null) Destroy(_texModalCardBg);
            if (_texInputDark != null) Destroy(_texInputDark);
            if (_texPlayerBarBg != null) Destroy(_texPlayerBarBg);
            if (_texMiddleColBg != null) Destroy(_texMiddleColBg);
            if (_texContactSelected != null) Destroy(_texContactSelected);
            if (_texContactNormal != null) Destroy(_texContactNormal);
            if (_texContactHover != null) Destroy(_texContactHover);

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
            _texDrawerBg = MakeBorderedTex(32, 32, new Color(0.06f, 0.10f, 0.20f, 0.98f), new Color(0.20f, 0.55f, 0.95f, 0.9f), 1);
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.09f, 0.16f, 0.90f));
            _texAvatarBorder = MakeBorderedTex(42, 42, new Color(0.10f, 0.16f, 0.26f, 1f), new Color(0.20f, 0.55f, 0.95f, 1f), 2);
            _texSmileyIcon = MakeSmileyTex(34);
            _texHornBg = MakeBorderedTex(64, 64, new Color(0.25f, 0.18f, 0.05f, 0.95f), new Color(1.0f, 0.85f, 0.22f, 1.0f), 2);
            _texAchievementBg = MakeBorderedTex(64, 64, new Color(0.20f, 0.08f, 0.30f, 0.95f), new Color(0.92f, 0.55f, 1.0f, 1.0f), 2);

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

            // Horn & Achievement Styles
            _hornBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texHornBg },
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 0, 3, 3)
            };

            _hornTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1.0f, 0.90f, 0.25f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _hornContentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _achievementBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texAchievementBg },
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 0, 3, 3)
            };

            _achievementTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1.0f, 0.75f, 0.20f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _achievementContentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true,
                normal = { textColor = new Color(0.95f, 0.90f, 1.0f) },
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

            // Middle Contact Column Styles (三段式布局联系人列表)
            _texMiddleColBg = MakeBorderedTex(32, 32, new Color(0.04f, 0.07f, 0.14f, 0.96f), new Color(0.12f, 0.22f, 0.36f, 0.80f), 1);
            _texContactSelected = MakeBorderedTex(32, 32, new Color(0.10f, 0.22f, 0.44f, 0.95f), new Color(1.0f, 0.84f, 0.22f, 1.0f), 2);
            _texContactNormal = MakeBorderedTex(32, 32, new Color(0.06f, 0.10f, 0.18f, 0.70f), new Color(0.10f, 0.18f, 0.30f, 0.50f), 1);
            _texContactHover = MakeBorderedTex(32, 32, new Color(0.10f, 0.20f, 0.36f, 0.90f), new Color(0.20f, 0.50f, 0.90f, 0.90f), 1);

            _middleColStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texMiddleColBg },
                padding = new RectOffset(4, 4, 4, 4)
            };

            _contactHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.70f, 0.88f, 1.0f) },
                padding = new RectOffset(2, 2, 4, 4)
            };

            _contactSelectedStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _texContactSelected },
                hover = { background = _texContactSelected },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _contactNormalStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _texContactNormal },
                hover = { background = _texContactHover },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _contactNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _contactStatusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            InitStyles();

            // Track chat input focus & typing state for input protection
            _isChatInputFocused = (GUI.GetNameOfFocusedControl() == "AolaChatInputField");
            IsTyping = _isChatInputFocused || _showInitialSetupModal || _showSelfProfileModal;

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
            bool isThreeColumn = (_currentChannel == ChatChannel.Friend || _currentChannel == ChatChannel.Whisper);
            float minW = isThreeColumn ? 600f : 450f;
            float maxW = isThreeColumn ? 750f : 520f;
            float panelWidth = Mathf.Clamp(Screen.width * (isThreeColumn ? 0.48f : 0.35f), minW, maxW);
            float panelX = 16f;
            float panelY = Screen.height - panelHeight - 16f;

            float tabColWidth = 64f;
            float middleColWidth = isThreeColumn ? 145f : 0f;
            float chatAreaWidth = panelWidth - tabColWidth - middleColWidth;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _mainPanelStyle);

            // -------------------------------------------------------------
            // A. LEFT VERTICAL CHANNEL TAB BAR (奥拉星左侧频道导航栏)
            // -------------------------------------------------------------
            GUILayout.BeginArea(new Rect(0, 0, tabColWidth, panelHeight));
            GUILayout.BeginVertical();

            DrawVerticalChannelTab("世界", ChatChannel.World);
            DrawVerticalChannelTab("好友", ChatChannel.Friend);
            DrawVerticalChannelTab("私聊", ChatChannel.Whisper);
            DrawVerticalChannelTab("附近", ChatChannel.Nearby);
            DrawVerticalChannelTab("系统", ChatChannel.System);

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // -------------------------------------------------------------
            // B. MIDDLE TARGET SELECTOR (中间栏：好友/私聊联系人列表)
            // 仅在好友和私聊模式下显示，构成三段式布局
            // -------------------------------------------------------------
            if (isThreeColumn)
            {
                DrawMiddleContactColumn(tabColWidth, 0, middleColWidth, panelHeight);
            }

            // -------------------------------------------------------------
            // C. RIGHT CHAT CONTENT AREAS (最右侧：聊天信息窗口)
            // 采用绝对分层锚定：底栏与滚动区位置绝对恒定，表情弹窗作为悬浮 Overlay 浮动在底栏上方，
            // 彻底杜绝流式布局累积误差，确保输入框在任何状态下绝不移动、绝不错位！
            // -------------------------------------------------------------
            float chatAreaX = tabColWidth + middleColWidth;
            float topHeaderH = 32f;
            float inputBarH = 50f;
            float inputBarY = panelHeight - inputBarH - 6f; // 固定锚定在面板底部，位置永不改变

            // C1. Top Header Bar (高度严格固定 32px，私聊目标与快捷操作同层并列)
            GUILayout.BeginArea(new Rect(chatAreaX, 0, chatAreaWidth, topHeaderH));
            DrawTopHeaderBar(chatAreaWidth);
            GUILayout.EndArea();

            // C2. Bottom Input Area (先绘制底部输入栏，确保其 Control ID 永远固定，位置绝对恒定)
            GUILayout.BeginArea(new Rect(chatAreaX, inputBarY, chatAreaWidth, inputBarH));
            DrawBottomInputBar(chatAreaWidth);
            GUILayout.EndArea();

            // C3. Scrollable Bubble Messages Area (高度稳定限定在顶部栏与底部输入栏之间)
            float scrollY = topHeaderH + 2f;
            float scrollH = inputBarY - scrollY - 4f;
            if (scrollH < 100f) scrollH = 100f;

            GUILayout.BeginArea(new Rect(chatAreaX, scrollY, chatAreaWidth, scrollH));
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
                if (_currentChannel == ChatChannel.World)
                {
                    // 世界聊天只需要显示：玩家发言、大喇叭、成就播报和系统文本
                    shouldShow = (msg.Channel == ChatChannel.World ||
                                  msg.Channel == ChatChannel.Horn ||
                                  msg.Channel == ChatChannel.Achievement ||
                                  msg.Channel == ChatChannel.System);
                }
                else if (_currentChannel == ChatChannel.Nearby)
                {
                    shouldShow = (msg.Channel == ChatChannel.Nearby);
                }
                else if (_currentChannel == ChatChannel.System)
                {
                    shouldShow = (msg.Channel == ChatChannel.System || msg.Channel == ChatChannel.Achievement);
                }
                else if (_currentChannel == ChatChannel.Friend)
                {
                    shouldShow = (msg.Channel == ChatChannel.Friend && 
                                  !string.IsNullOrEmpty(_selectedFriendId) &&
                                  (msg.SenderId == _selectedFriendId || msg.TargetId == _selectedFriendId));
                }
                else if (_currentChannel == ChatChannel.Whisper)
                {
                    shouldShow = (msg.Channel == ChatChannel.Whisper &&
                                  !string.IsNullOrEmpty(_whisperTargetId) &&
                                  (msg.SenderId == _whisperTargetId || msg.TargetId == _whisperTargetId));
                }

                if (shouldShow)
                {
                    if (msg.Channel == ChatChannel.Horn)
                    {
                        DrawHornBubble(msg, chatAreaWidth);
                    }
                    else if (msg.Channel == ChatChannel.Achievement)
                    {
                        DrawAchievementBubble(msg, chatAreaWidth);
                    }
                    else if (msg.Channel == ChatChannel.System)
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
                    ChatChannel.Friend => string.IsNullOrEmpty(_selectedFriendId) 
                        ? "<color=#78909C>请在左侧列表中选择好友开始聊天...</color>" 
                        : $"<color=#78909C>暂无与 [{_selectedFriendName}] 的聊天记录，发条消息打个招呼吧！</color>",
                    ChatChannel.Whisper => string.IsNullOrEmpty(_whisperTargetId) 
                        ? "<color=#78909C>请在左侧列表中选择私聊对象...</color>" 
                        : $"<color=#78909C>暂无与 [{_whisperTargetName}] 的私聊记录，发条消息打个招呼吧！</color>",
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

            // C4. Quick Emoji & Phrases Drawer (悬浮弹窗 Overlay：浮动于输入栏正上方，不挤压底栏)
            if (_showQuickEmojiDrawer)
            {
                float drawerW = chatAreaWidth - 8f;
                float drawerH = 125f;
                float drawerX = chatAreaX + 4f;
                float drawerY = inputBarY - drawerH - 2f;
                Rect drawerRect = new Rect(drawerX, drawerY, drawerW, drawerH);

                GUILayout.BeginArea(drawerRect, _drawerBoxStyle);
                DrawQuickEmojiDrawer();
                GUILayout.EndArea();

                // 点击弹窗外部（且非底部输入栏区域）时平滑收起，按 Esc 也收起
                Rect inputBarRect = new Rect(chatAreaX, inputBarY, chatAreaWidth, inputBarH);
                Event currentEvt = Event.current;
                if ((currentEvt.type == EventType.MouseDown && 
                     !drawerRect.Contains(currentEvt.mousePosition) && 
                     !inputBarRect.Contains(currentEvt.mousePosition)) ||
                    (currentEvt.type == EventType.KeyDown && currentEvt.keyCode == KeyCode.Escape))
                {
                    _showQuickEmojiDrawer = false;
                }
            }

            GUILayout.EndArea(); // Close panel main area

            // Player Selection Dropdown Overlay (if open)
            if (_showPlayerSelectDropdown)
            {
                DrawPlayerSelectDropdown(panelX + tabColWidth, panelY, chatAreaWidth);
            }
        }

        private void DrawMiddleContactColumn(float colX, float colY, float colW, float colH)
        {
            GUILayout.BeginArea(new Rect(colX, colY, colW, colH), _middleColStyle);
            GUILayout.BeginVertical();

            // 1. Column Header (28px)
            string headerText = _currentChannel == ChatChannel.Friend 
                ? $"👥 <b>好友列表</b> ({_friendsList.Count})" 
                : $"💬 <b>私聊对象</b> ({_whisperContacts.Count})";
            GUILayout.Label(headerText, _contactHeaderStyle, GUILayout.Height(28));
            GUILayout.Space(2);

            // 2. Scrollable Contact Cards
            _contactScrollPosition = GUILayout.BeginScrollView(
                _contactScrollPosition, 
                false, 
                false, 
                GUILayout.Width(colW - 8), 
                GUILayout.Height(colH - 38)
            );

            List<ChatContact> contacts = _currentChannel == ChatChannel.Friend ? _friendsList : _whisperContacts;
            string selectedId = _currentChannel == ChatChannel.Friend ? _selectedFriendId : _whisperTargetId;

            for (int i = 0; i < contacts.Count; i++)
            {
                var contact = contacts[i];
                bool isSelected = (contact.id == selectedId);
                GUIStyle cardStyle = isSelected ? _contactSelectedStyle : _contactNormalStyle;

                Rect cardRect = GUILayoutUtility.GetRect(colW - 14, 46, GUILayout.Width(colW - 14), GUILayout.Height(46));
                if (GUI.Button(cardRect, GUIContent.none, cardStyle))
                {
                    if (_currentChannel == ChatChannel.Friend)
                    {
                        _selectedFriendId = contact.id;
                        _selectedFriendName = contact.name;
                    }
                    else
                    {
                        _whisperTargetId = contact.id;
                        _whisperTargetName = contact.name;
                    }
                    _shouldScrollToBottom = true;
                }

                // Draw Avatar on left (32x32)
                Texture2D avTex = GetAvatarTex(contact.avatarIndex);
                Rect avRect = new Rect(cardRect.x + 5, cardRect.y + 7, 32, 32);
                GUI.DrawTexture(avRect, avTex, ScaleMode.ScaleToFit);

                // Draw Name on right
                string nameColor = isSelected ? "#FFE082" : "#ECEFF1";
                Rect nameRect = new Rect(cardRect.x + 42, cardRect.y + 5, cardRect.width - 46, 18);
                GUI.Label(nameRect, $"<color={nameColor}><b>{contact.name}</b></color>", _contactNameStyle);

                // Draw Gender & Level & Status
                string genderIcon = contact.gender == "female" ? "<color=#FF4081>♀</color>" : "<color=#40C4FF>♂</color>";
                string statusDot = contact.isOnline ? "<color=#00E676>●</color>" : "<color=#78909C>○</color>";
                Rect infoRect = new Rect(cardRect.x + 42, cardRect.y + 24, cardRect.width - 46, 16);
                GUI.Label(infoRect, $"{genderIcon} <size=10><color=#90A4AE>Lv.{contact.level}</color></size> {statusDot}", _contactStatusStyle);

                GUILayout.Space(3);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawVerticalChannelTab(string label, ChatChannel channel)
        {
            bool isActive = (_currentChannel == channel);
            GUIStyle style = isActive ? _tabActiveStyle : _tabInactiveStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(46)))
            {
                _currentChannel = channel;
                _shouldScrollToBottom = true;
            }
        }

        private void DrawTopHeaderBar(float contentWidth)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Space(4);

            if (_currentChannel == ChatChannel.Friend)
            {
                string targetName = !string.IsNullOrEmpty(_selectedFriendName) ? _selectedFriendName : "选择好友";
                GUILayout.Label($"<color=#80D8FF><b>正在与 {targetName} 聊天</b></color>", _senderNameOtherStyle, GUILayout.Height(24));
            }
            else if (_currentChannel == ChatChannel.Whisper)
            {
                string targetName = !string.IsNullOrEmpty(_whisperTargetName) ? _whisperTargetName : "选择目标";
                GUILayout.Label($"<color=#FF80AB><b>正在与 {targetName} 私聊</b></color>", _senderNameOtherStyle, GUILayout.Height(24));
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

            // 右上角操作按钮: 喇叭 / 轮盘 / 访客 / 收起
            if (_currentChannel != ChatChannel.Friend && _currentChannel != ChatChannel.Whisper)
            {
                if (GUILayout.Button("📢 喇叭", _sideActionBtnStyle, GUILayout.Width(58), GUILayout.Height(24)))
                {
                    _inputChat = "/horn ";
                    GUI.FocusControl("AolaChatInputField");
                }

                GUILayout.Space(4);

                if (GUILayout.Button("🎡 轮盘", _sideActionBtnStyle, GUILayout.Width(58), GUILayout.Height(24)))
                {
                    if (EmoteWheelUI.Instance != null) EmoteWheelUI.Instance.OpenWheel();
                }

                GUILayout.Space(4);
            }

            if (GUILayout.Button("🤖 访客", _sideActionBtnStyle, GUILayout.Width(54), GUILayout.Height(24)))
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

                string chTag = "";
                if (_currentChannel == ChatChannel.World && msg.Channel != ChatChannel.World)
                {
                    chTag = msg.Channel switch
                    {
                        ChatChannel.Whisper => "<color=#FF4081>[私聊]</color> ",
                        ChatChannel.Nearby => "<color=#00E5FF>[附近]</color> ",
                        ChatChannel.System => "<color=#76FF03>[系统]</color> ",
                        _ => ""
                    };
                }

                // Content Column (Name above Bubble)
                GUILayout.BeginVertical();
                GUILayout.Label(chTag + msg.SenderName, _senderNameOtherStyle);
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

                string selfChTag = "";
                if (_currentChannel == ChatChannel.World && msg.Channel != ChatChannel.World)
                {
                    selfChTag = msg.Channel switch
                    {
                        ChatChannel.Whisper => "<color=#FF4081>[私聊]</color> ",
                        ChatChannel.Nearby => "<color=#00E5FF>[附近]</color> ",
                        ChatChannel.System => "<color=#76FF03>[系统]</color> ",
                        _ => ""
                    };
                }

                // Content Column (Name above Bubble, Right-aligned)
                GUILayout.BeginVertical();
                string targetPrefix = (!string.IsNullOrEmpty(msg.TargetName)) ? $"对 <color=#80D8FF>{msg.TargetName}</color> 说" : msg.SenderName;
                GUILayout.Label(selfChTag + targetPrefix, _senderNameSelfStyle);
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

        private void DrawHornBubble(ChatBubbleItem msg, float contentWidth)
        {
            GUILayout.Space(4);
            float cardWidth = Mathf.Max(240f, contentWidth - 28f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);

            GUILayout.BeginVertical(_hornBoxStyle, GUILayout.Width(cardWidth));

            // Top Row: Gold Horn Icon & Tag + Sender Name + Timestamp
            GUILayout.BeginHorizontal();
            GUILayout.Label("📢 <b><color=#FFE082>[全服大喇叭]</color></b>", _hornTagStyle, GUILayout.Height(18));
            GUILayout.Space(4);
            GUILayout.Label($"<color=#FFF59D><b>{msg.SenderName}</b></color>", _hornTagStyle, GUILayout.Height(18));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<color=#FFE082><size=10>{msg.TimeStr}</size></color>", _avatarLevelStyle, GUILayout.Height(18));
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Content body: Bold Golden Content
            GUILayout.Label(msg.Content, _hornContentStyle, GUILayout.Width(cardWidth - 20f));

            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }

        private void DrawAchievementBubble(ChatBubbleItem msg, float contentWidth)
        {
            GUILayout.Space(4);
            float cardWidth = Mathf.Max(240f, contentWidth - 28f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);

            GUILayout.BeginVertical(_achievementBoxStyle, GUILayout.Width(cardWidth));

            // Top Row: Trophy Icon & Achievement Tag + Timestamp
            GUILayout.BeginHorizontal();
            GUILayout.Label("🏆 <b><color=#FFD54F>[全服成就播报]</color></b>", _achievementTagStyle, GUILayout.Height(18));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<color=#CE93D8><size=10>{msg.TimeStr}</size></color>", _avatarLevelStyle, GUILayout.Height(18));
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Title line: Player name + Unlocked Achievement Title
            string titleStr = !string.IsNullOrEmpty(msg.Title) ? msg.Title : "空间探索先锋";
            GUILayout.Label($"恭喜玩家 <color=#FFD54F><b>{msg.SenderName}</b></color> 达成荣誉成就【<color=#00E5FF>{titleStr}</color>】！", _achievementContentStyle, GUILayout.Width(cardWidth - 20f));

            if (!string.IsNullOrEmpty(msg.Content))
            {
                GUILayout.Space(1);
                GUILayout.Label($"<color=#B39DDB><i>“{msg.Content}”</i></color>", _achievementContentStyle, GUILayout.Width(cardWidth - 20f));
            }

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
            bool hasComposition = !string.IsNullOrEmpty(Input.compositionString);
            bool pressEnter = (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && isFocused && !e.shift && !hasComposition);

            if (pressEnter)
            {
                e.Use();
                if (!string.IsNullOrEmpty(_inputChat.Trim()))
                {
                    if (SendMessageContent(_inputChat.Trim()))
                    {
                        _inputChat = "";
                        GUI.FocusControl(null);
                    }
                }
            }

            // 1. White Rounded Input Box (使用 GUILayout.TextArea 保证布局流稳定，容量扩至 200 字)
            GUI.SetNextControlName("AolaChatInputField");
            _inputChat = GUILayout.TextArea(_inputChat, 200, _inputFieldStyle, GUILayout.Width(inputW), GUILayout.Height(inputBarH));
            Rect inputRect = GUILayoutUtility.GetLastRect();

            // 占位提示仅在 Repaint 阶段直接绘制，不生成多余控件 ID，杜绝控件 ID 偏移
            if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(_inputChat) && !isFocused)
            {
                string hint = "点击输入...";
                if (_currentChannel == ChatChannel.Friend)
                {
                    hint = string.IsNullOrEmpty(_selectedFriendId) ? "点击输入 (请先在左侧选择好友)..." : $"对 [{_selectedFriendName}] 说...";
                }
                else if (_currentChannel == ChatChannel.Whisper)
                {
                    hint = string.IsNullOrEmpty(_whisperTargetId) ? "点击输入 (请先在左侧选择目标)..." : $"对 [{_whisperTargetName}] 说...";
                }
                _placeholderStyle.Draw(new Rect(inputRect.x + 8, inputRect.y + 7, inputRect.width - 16, 20), hint, false, false, false, false);
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
                if (SendMessageContent(_inputChat.Trim()))
                {
                    _inputChat = "";
                    GUI.FocusControl(null);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawQuickEmojiDrawer()
        {
            GUILayout.BeginVertical();

            // Drawer Header with Sub-tabs
            GUILayout.BeginHorizontal(GUILayout.Height(22));
            GUIStyle tab0Style = (_quickDrawerTab == 0) ? _tabActiveStyle : _tabInactiveStyle;
            GUIStyle tab1Style = (_quickDrawerTab == 1) ? _tabActiveStyle : _tabInactiveStyle;

            if (GUILayout.Button("😀 趣味表情", tab0Style, GUILayout.Width(88), GUILayout.Height(20))) _quickDrawerTab = 0;
            if (GUILayout.Button("💬 常用短语", tab1Style, GUILayout.Width(88), GUILayout.Height(20))) _quickDrawerTab = 1;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(22), GUILayout.Height(18)))
            {
                _showQuickEmojiDrawer = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

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
                            if (GUILayout.Button(item.label, _drawerItemStyle, GUILayout.Height(22)))
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
                    if (GUILayout.Button(_quickPhrases[i], _drawerItemStyle, GUILayout.Height(22)))
                    {
                        SendMessageContent(_quickPhrases[i]);
                        _showQuickEmojiDrawer = false;
                    }
                    if (i + 1 < _quickPhrases.Length)
                    {
                        if (GUILayout.Button(_quickPhrases[i + 1], _drawerItemStyle, GUILayout.Height(22)))
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

        private bool SendMessageContent(string content)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                AddSystemMessage("<color=#FF5252>未连接到空间服务器，消息发送失败！</color>");
                return false;
            }

            // 1. 大喇叭指令 (/horn 内容, /喇叭 内容, /大喇叭 内容)
            if (content.StartsWith("/horn ", StringComparison.OrdinalIgnoreCase) ||
                content.StartsWith("/喇叭 ") ||
                content.StartsWith("/大喇叭 "))
            {
                int firstSpace = content.IndexOf(' ');
                string hornText = (firstSpace >= 0 && firstSpace < content.Length - 1) ? content.Substring(firstSpace + 1).Trim() : "";
                if (!string.IsNullOrEmpty(hornText))
                {
                    NetworkManager.Instance.SendHorn(hornText);
                    return true;
                }
            }

            // 2. 成就播报测试指令 (/achieve 称号, /成就 称号)
            if (content.StartsWith("/achieve ", StringComparison.OrdinalIgnoreCase) ||
                content.StartsWith("/成就 "))
            {
                int firstSpace = content.IndexOf(' ');
                string achTitle = (firstSpace >= 0 && firstSpace < content.Length - 1) ? content.Substring(firstSpace + 1).Trim() : "";
                if (!string.IsNullOrEmpty(achTitle))
                {
                    NetworkManager.Instance.BroadcastAchievement(achTitle, "在智慧空间广场完成了瞩目的荣誉挑战！");
                    return true;
                }
            }

            if (_currentChannel == ChatChannel.Friend)
            {
                if (string.IsNullOrEmpty(_selectedFriendId))
                {
                    AddSystemMessage("<color=#FF5252>请先在左侧选择要聊天的好友！</color>");
                    return false;
                }

                string myId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : "self";
                string myName = NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null 
                    ? NetworkManager.Instance.LocalProfile.username 
                    : "我";
                int myAvatar = NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null 
                    ? NetworkManager.Instance.LocalProfile.avatarId 
                    : 0;

                AddBubbleMessage(new ChatBubbleItem
                {
                    Channel = ChatChannel.Friend,
                    SenderId = myId,
                    SenderName = myName,
                    TargetId = _selectedFriendId,
                    TargetName = _selectedFriendName,
                    Content = content,
                    TimeStr = DateTime.Now.ToString("HH:mm"),
                    IsSelf = true,
                    AvatarIndex = myAvatar,
                    Level = NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.level : 1,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                if (NetworkManager.Instance != null && NetworkManager.Instance.OnlinePlayers.ContainsKey(_selectedFriendId))
                {
                    NetworkManager.Instance.SendWhisper(_selectedFriendId, content);
                }
                else
                {
                    StartCoroutine(SimulateFriendReplyRoutine(_selectedFriendId, _selectedFriendName));
                }
                return true;
            }
            else if (_currentChannel == ChatChannel.Whisper)
            {
                if (string.IsNullOrEmpty(_whisperTargetId))
                {
                    AddSystemMessage("<color=#FF5252>请先在左侧选择私聊目标玩家！</color>");
                    return false;
                }

                NetworkManager.Instance.SendWhisper(_whisperTargetId, content);
                return true;
            }
            else
            {
                NetworkManager.Instance.SendChat(content);
                return true;
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
                    ChatChannel.Friend => "<color=#00E5FF>[好友]</color>",
                    ChatChannel.Nearby => "<color=#00E5FF>[附近]</color>",
                    ChatChannel.Whisper => "<color=#FF4081>[私聊]</color>",
                    ChatChannel.System => "<color=#76FF03>[系统]</color>",
                    ChatChannel.Horn => "<color=#FFD54F>[喇叭]</color>",
                    ChatChannel.Achievement => "<color=#CE93D8>[成就]</color>",
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

            // Bottom Actions: Whisper or Add Friend or Close
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

            GUILayout.Space(6);

            if (GUILayout.Button("🤝  加为好友", _modalPrimaryBtnStyle, GUILayout.Height(38)))
            {
                AddFriendFromProfile(_targetProfileSessionId, _targetProfile);
                _selectedFriendId = _targetProfileSessionId;
                _selectedFriendName = _targetProfile.username;
                _currentChannel = ChatChannel.Friend;
                _showTargetProfileModal = false;
                _isExpanded = true;
                _shouldScrollToBottom = true;
            }

            GUILayout.Space(6);

            if (GUILayout.Button("关 闭", _modalSecondaryBtnStyle, GUILayout.Width(76), GUILayout.Height(38)))
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

