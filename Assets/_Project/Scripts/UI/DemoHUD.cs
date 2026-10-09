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
        Friend = 6,       // 好友 (好友聊天)
        Team = 7          // 组队 (副本队伍聊天)
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
        public static DemoHUD Instance { get; private set; }
        public bool IsExpanded => _isExpanded;

        private const string PREF_CHAT_HISTORY = "AolaStar_ChatHistory_Cache";
        private static readonly List<ChatBubbleItem> s_sharedMessages = new List<ChatBubbleItem>();

        [Header("Chat Settings")]
        [SerializeField] private bool defaultExpanded = true;
        [SerializeField] private int maxHistoryCount = 200;

        public static bool IsTyping { get; private set; }
        public static bool IsPlazaHUDHidden { get; set; } = false;
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

        // Team & Dungeon State
        [SerializeField] private bool _isInTeam = false;
        private string _currentTeamName = "暗夜遗迹探索小队";
        private int _teamMemberCount = 4;

        // Quick Emoji & Phrases Drawer State
        private bool _showQuickEmojiDrawer = false;
        private int _emojiDrawerTab = 0; // 0: 常用, 1: 伊乐, 2: 像素, 3: 恶魔, 4: Q版, 5: 常用语

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
        private GUIStyle _tabDisabledStyle;
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
        private GUIStyle _emojiCardBtnStyle;
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
        private Texture2D _texTabDisabled;
        private Texture2D _texBubbleBlue;
        private Texture2D _texSendBtnYellow;
        private Texture2D _texInputWhite;
        private Texture2D _texEmojiDark;
        private Texture2D _texDrawerBg;
        private Texture2D _texMiniBarBg;
        private Texture2D _texAvatarBorder;
        private Texture2D _texSmileyIcon;
        private Texture2D _texHornIcon;
        private Texture2D _texHornBg;
        private Texture2D _texAchievementBg;

        // Modal & Profile Textures
        private Texture2D _texModalOverlay;
        private Texture2D _texModalCardBg;
        private Texture2D _texInputDark;
        private Texture2D _texPlayerBarBg;

        // Character Profile View Textures & Styles (Aola Star 1:1 Interface)
        private Texture2D _texProfileFullCardBg;
        private Texture2D _texProfileBackBtn;
        private Texture2D _texExpBarBg;
        private Texture2D _texExpBarFill;
        private Texture2D _texAuraRing;
        private Texture2D _texHeartIcon;
        private Texture2D _texBadgeCardBg;
        private Texture2D _texVerticalTabActive;
        private Texture2D _texVerticalTabInactive;

        private GUIStyle _profileCardStyle;
        private GUIStyle _profileBackBtnStyle;
        private GUIStyle _profileVerticalTabActiveStyle;
        private GUIStyle _profileVerticalTabInactiveStyle;
        private GUIStyle _badgeCardStyle;
        private GUIStyle _profileStageHintStyle;

        private int _profileTab = 0; // 0: 信息, 1: 时装, 2: 称号
        private int _previewCostumeId = 0;
        private int _selectedCostumeScheme = 0; // 0 ~ 3 (方案 1 ~ 4)
        private int _costumeCategoryTab = 0; // 0: 全部, 1: 服装, 2: 手持, 3: 背部, 4: 法阵, 5: 背景
        private int _costumeQualityFilter = 0; // 0: 全部品质, 1: 典藏, 2: 传说, 3: 史诗, 4: 稀有
        private string _costumeSearchText = "";
        private Vector2 _costumeScrollPos = Vector2.zero;

        // Costume Data (1:1 风格时装衣橱)
        public class CostumeItem
        {
            public int id;
            public string name;
            public string category;
            public string styleTag;
            public string desc;
            public Color primaryColor;
            public Color accentColor;
            public Texture2D iconTex;
            public CostumeItem(int id, string name, string category, string styleTag, string desc, Color primary, Color accent)
            {
                this.id = id;
                this.name = name;
                this.category = category;
                this.styleTag = styleTag;
                this.desc = desc;
                this.primaryColor = primary;
                this.accentColor = accent;
            }
        }
        private readonly List<CostumeItem> _costumeItems = new List<CostumeItem>();
        private bool _costumesInitialized = false;

        // Achievement & Title Data (成就荣誉殿堂 · 称号佩戴)
        public class AchievementTitleItem
        {
            public string title;
            public string rarity;
            public Color rarityColor;
            public string category;
            public int points;
            public string condition;
            public bool isUnlocked;
            public string progress;
            public AchievementTitleItem(string title, string rarity, Color rarityColor, string category, int points, string condition, bool isUnlocked, string progress)
            {
                this.title = title;
                this.rarity = rarity;
                this.rarityColor = rarityColor;
                this.category = category;
                this.points = points;
                this.condition = condition;
                this.isUnlocked = isUnlocked;
                this.progress = progress;
            }
        }
        private readonly List<AchievementTitleItem> _achievementTitles = new List<AchievementTitleItem>();
        private bool _achievementsInitialized = false;
        private int _titleCategoryTab = 0; // 0: 全部, 1: 空间漫游, 2: 社交达人, 3: 副本荣耀, 4: 典藏限定
        private string _titleSearchText = "";
        private Vector2 _titleScrollPos = Vector2.zero;
        private bool _isEditingName = false;
        private bool _isEditingBio = false;
        private bool _isEditingDetails = false;
        private string _birthYearStr = "2002";
        private string _birthMonthStr = "05";
        private string _birthDayStr = "20";
        private bool _isBirthSecret = false;
        private bool _isDraggingChar = false;
        private Vector2 _lastDragPos = Vector2.zero;
        private Vector2 _profileScrollPos = Vector2.zero;
        private readonly HashSet<string> _blacklistedIds = new HashSet<string>();

        // Featured Photos (精选照片)
        public class ProfilePhotoItem
        {
            public string id;
            public string title;
            public int likes;
            public int presetIndex;
            public string fileName;
            public Texture2D texture;
            public ProfilePhotoItem(string title, int likes, Texture2D texture, int preset = -1, string fileName = "")
            {
                this.id = System.Guid.NewGuid().ToString("N");
                this.title = title;
                this.likes = likes;
                this.texture = texture;
                this.presetIndex = preset;
                this.fileName = fileName;
            }
        }
        private readonly List<ProfilePhotoItem> _featuredPhotos = new List<ProfilePhotoItem>();
        private readonly List<ProfilePhotoItem> _targetFeaturedPhotos = new List<ProfilePhotoItem>();
        private readonly List<ProfilePhotoItem> _editingPhotos = new List<ProfilePhotoItem>();
        private readonly List<ProfilePhotoItem> _photosPendingDelete = new List<ProfilePhotoItem>();
        private bool _isEditingPhotos = false;
        private bool _featuredPhotosInitialized = false;
        private bool _showPhotoUploadModal = false;
        private string _uploadPhotoTitle = "星海漫游打卡";
        private int _uploadSourceTab = 0;
        private Texture2D _uploadPreviewTex = null;
        private int _selectedPresetIndex = 0;
        private string _lastLoadedLocalFileName = "";
        private Vector2 _photoScrollPos = Vector2.zero;
        private int _photoToDeleteIndex = -1;

        // Procedural Avatars (预设 6 款个性化头像)
        private Texture2D[] _avatarTextures;
        private Texture2D _texAvatarSelf;
        private Texture2D[] _texAvatarOthers;

        // Fig 2 1:1 Emoji Definition & Categories
        public struct EmojiCardItem
        {
            public string icon;
            public string name;
            public EmojiCardItem(string icon, string name)
            {
                this.icon = icon;
                this.name = name;
            }
        }

        // 1. ❤️ 常用 (图2同款核心表情包)
        private readonly EmojiCardItem[] _commonEmojis = new EmojiCardItem[]
        {
            new EmojiCardItem("(╬ ﾟдﾟ)", "愤怒"),
            new EmojiCardItem("(๑>؂<๑)", "想要"),
            new EmojiCardItem("(*^▽^*)", "微笑"),
            new EmojiCardItem("( ╥﹏╥ )", "哭泣"),
            new EmojiCardItem("(¬‿¬)", "滑稽"),
            new EmojiCardItem("(T_T)", "心碎"),
            new EmojiCardItem("(￣▽￣;)", "尴尬"),
            new EmojiCardItem("Σ(°△°)", "震惊"),
            new EmojiCardItem("(づ￣³￣)づ", "爱你"),
            new EmojiCardItem("d(･∀･*)b", "点赞")
        };

        // 2. 🐱 伊乐 (萌宠特色表情包)
        private readonly EmojiCardItem[] _yileEmojis = new EmojiCardItem[]
        {
            new EmojiCardItem("(*^▽^*)", "开心"),
            new EmojiCardItem("( ￣^￣ )", "傲娇"),
            new EmojiCardItem("(*/ω＼*)", "撒娇"),
            new EmojiCardItem("(っ˘̩╭╮˘̩)っ", "委屈"),
            new EmojiCardItem("(づ￣³￣)づ♥", "比心"),
            new EmojiCardItem("[ 旦~ ]", "干杯"),
            new EmojiCardItem("[ Zzz ]", "睡觉"),
            new EmojiCardItem("٩('ω')و", "冲鸭"),
            new EmojiCardItem("( ˙-˙ )", "吃瓜"),
            new EmojiCardItem("(•̀ᄇ•́)و", "加油")
        };

        // 3. 👾 像素 (复古像素潮趣表情包)
        private readonly EmojiCardItem[] _pixelEmojis = new EmojiCardItem[]
        {
            new EmojiCardItem("XD", "大笑"),
            new EmojiCardItem("♥_♥", "心动"),
            new EmojiCardItem("(^_^;)", "流汗"),
            new EmojiCardItem("(@_@)", "眩晕"),
            new EmojiCardItem("(B-)", "墨镜"),
            new EmojiCardItem("(O_O)", "吃惊"),
            new EmojiCardItem("(^o^)/", "挥手"),
            new EmojiCardItem("(>_<)", "生气"),
            new EmojiCardItem("m(_ _)m", "拜托"),
            new EmojiCardItem("(^v^)", "胜利")
        };

        // 4. 😈 恶魔 (俏皮恶魔表情包)
        private readonly EmojiCardItem[] _demonEmojis = new EmojiCardItem[]
        {
            new EmojiCardItem("(▼∀▼)", "坏笑"),
            new EmojiCardItem("(皿#)", "怒火"),
            new EmojiCardItem("[ ✦_✦ ]", "幽灵"),
            new EmojiCardItem("[ X_X ]", "骷髅"),
            new EmojiCardItem("(¬_¬)", "戏谑"),
            new EmojiCardItem("(PД`P)", "鬼脸"),
            new EmojiCardItem("[ 炎 ]", "燃烧"),
            new EmojiCardItem("[ 炸 ]", "爆炸"),
            new EmojiCardItem("[ 雷 ]", "雷霆"),
            new EmojiCardItem("[ 蝠 ]", "蝙蝠")
        };

        // 5. 🌟 Q版 (萌系Q版互动表情包)
        private readonly EmojiCardItem[] _chibiEmojis = new EmojiCardItem[]
        {
            new EmojiCardItem("(*°▽°*)", "欢呼"),
            new EmojiCardItem("(^_-)", "眨眼"),
            new EmojiCardItem("(★_★)", "星星眼"),
            new EmojiCardItem("(*’ω’*)", "鼓掌"),
            new EmojiCardItem("v(^_^)v", "比耶"),
            new EmojiCardItem("( ﾟ∀ﾟ)人", "握手"),
            new EmojiCardItem("(つ´ω`)つ", "抱抱"),
            new EmojiCardItem("(¯﹃¯)", "贪吃"),
            new EmojiCardItem("(×_×)", "晕倒"),
            new EmojiCardItem("(◕‿◕✿)", "乖巧")
        };

        private EmojiCardItem[] GetCategoryEmojis(int tab)
        {
            return tab switch
            {
                0 => _commonEmojis,
                1 => _yileEmojis,
                2 => _pixelEmojis,
                3 => _demonEmojis,
                4 => _chibiEmojis,
                _ => _commonEmojis
            };
        }

        // Quick Phrases (保留原有常用语)
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
            Instance = this;
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

            // 预加载高空独立 3D 摄影棚展台场景，彻底消除打开资料界面时的卡顿
            WardrobeSceneController.PreloadProfileStudio();
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

            // 退出队伍后若停留在组队频道，自动切回世界频道
            if (!_isInTeam && _currentChannel == ChatChannel.Team)
            {
                _currentChannel = ChatChannel.World;
            }

            // Toggle Expand/Collapse with 'C' key when not focused in any input
            if (Input.GetKeyDown(KeyCode.C) && !IsTyping)
            {
                ToggleExpand();
            }

            // ESC to close open modals
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_showTargetProfileModal || _showSelfProfileModal) CloseProfileModal();
                else if (_showPlayerSelectDropdown) _showPlayerSelectDropdown = false;
            }
        }

        public void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            if (_isExpanded)
            {
                _shouldScrollToBottom = true;
                if (EmoteWheelUI.Instance != null)
                {
                    EmoteWheelUI.Instance.CloseWheel();
                }
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

        private Texture2D MakeHornTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pix = new Color[size * size];
            Color yellow = new Color(1.0f, 0.86f, 0.18f, 1f);
            Color darkYellow = new Color(0.85f, 0.68f, 0.10f, 1f);
            Color white = Color.white;
            for (int i = 0; i < pix.Length; i++) pix[i] = Color.clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // 1. Speaker base box (u: 0.16..0.34, v: 0.36..0.64)
                    if (u >= 0.16f && u <= 0.34f && v >= 0.36f && v <= 0.64f)
                    {
                        bool isBorder = (u <= 0.19f || u >= 0.31f || v <= 0.39f || v >= 0.61f);
                        pix[y * size + x] = isBorder ? darkYellow : yellow;
                    }
                    // 2. Speaker cone (u: 0.34..0.62, flares from 0.36..0.64 to 0.18..0.82)
                    else if (u > 0.34f && u <= 0.62f)
                    {
                        float flare = (u - 0.34f) / (0.62f - 0.34f);
                        float minY = 0.36f - flare * 0.18f;
                        float maxY = 0.64f + flare * 0.18f;
                        if (v >= minY && v <= maxY)
                        {
                            bool isBorder = (v <= minY + 0.04f || v >= maxY - 0.04f || u >= 0.58f);
                            pix[y * size + x] = isBorder ? darkYellow : yellow;
                        }
                    }
                    // 3. Sound wave arcs (u: 0.65..0.92)
                    else if (x > size * 0.62f && Mathf.Abs(v - 0.5f) <= 0.32f)
                    {
                        Vector2 origin = new Vector2(size * 0.45f, size * 0.50f);
                        float dist = Vector2.Distance(new Vector2(x, y), origin);
                        float d1 = Mathf.Abs(dist - size * 0.28f);
                        float d2 = Mathf.Abs(dist - size * 0.40f);
                        if (d1 < 1.4f || d2 < 1.4f)
                        {
                            pix[y * size + x] = white;
                        }
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeHeartTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pix = new Color[size * size];
            Color pink = new Color(1.0f, 0.40f, 0.65f, 1.0f);
            Color darkPink = new Color(0.85f, 0.20f, 0.45f, 1.0f);
            for (int i = 0; i < pix.Length; i++) pix[i] = Color.clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = ((float)x / size - 0.5f) * 2.2f;
                    float v = ((float)y / size - 0.55f) * 2.2f;
                    float f = (u * u + v * v - 1f);
                    if (f * f * f - u * u * v * v * v <= 0.05f)
                    {
                        pix[y * size + x] = (Mathf.Abs(f * f * f - u * u * v * v * v) < 0.1f) ? darkPink : pink;
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeAuraRingTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pix = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.45f;
            Color gold = new Color(1.0f, 0.85f, 0.25f, 0.85f);
            Color cyan = new Color(0.20f, 0.75f, 1.0f, 0.60f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float diff = Mathf.Abs(dist - radius);
                    if (diff < size * 0.08f)
                    {
                        float alpha = 1f - (diff / (size * 0.08f));
                        Color c = Color.Lerp(gold, cyan, (float)y / size);
                        c.a *= alpha * 0.7f;
                        pix[y * size + x] = c;
                    }
                    else
                    {
                        pix[y * size + x] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakePhotoThumbTex(int width, int height, Color topColor, Color midColor, Color botColor, Color accentColor, int type)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float ty = (float)y / height;
                Color sky = (ty > 0.45f)
                    ? Color.Lerp(midColor, topColor, (ty - 0.45f) / 0.55f)
                    : Color.Lerp(botColor, midColor, ty / 0.45f);

                for (int x = 0; x < width; x++)
                {
                    float tx = (float)x / width;
                    Color c = sky;

                    if (type == 0) // Stars & Moon
                    {
                        float dx = tx - 0.78f;
                        float dy = ty - 0.72f;
                        if (dx * dx + dy * dy < 0.015f && (dx + 0.04f) * (dx + 0.04f) + dy * dy > 0.012f)
                        {
                            c = Color.white;
                        }
                        else if ((x * 17 + y * 31) % 43 == 0 && ty > 0.45f)
                        {
                            c = Color.white;
                        }
                        if (ty < 0.22f + Mathf.Sin(tx * 6f) * 0.05f)
                        {
                            c = new Color(0.04f, 0.06f, 0.12f);
                        }
                    }
                    else if (type == 1) // Floating Island
                    {
                        float cdist = Mathf.Abs(tx - 0.5f);
                        if (ty > 0.26f && ty < 0.50f && cdist < 0.35f * (1f - (ty - 0.26f) * 2f))
                        {
                            c = accentColor;
                        }
                        if (ty > 0.12f && ty < 0.22f)
                        {
                            c = Color.Lerp(c, Color.white, 0.45f);
                        }
                    }
                    else if (type == 2) // Nebula
                    {
                        float swirl = Mathf.Sin(tx * 5f + ty * 4f);
                        if (swirl > 0.35f)
                        {
                            c = Color.Lerp(c, accentColor, (swirl - 0.35f) * 1.5f);
                        }
                        if ((x * 13 + y * 23) % 37 == 0) c = Color.white;
                    }
                    else if (type == 3) // Sunset
                    {
                        float dx = tx - 0.5f;
                        float dy = ty - 0.42f;
                        if (dx * dx + dy * dy < 0.02f)
                        {
                            c = Color.Lerp(Color.yellow, Color.white, 0.6f);
                        }
                        if (ty < 0.28f + Mathf.Abs(Mathf.Sin(tx * 8f)) * 0.12f)
                        {
                            c = new Color(0.08f, 0.05f, 0.14f);
                        }
                    }
                    else if (type == 4) // Aurora
                    {
                        float wave = Mathf.Sin(tx * 7f) * 0.15f + 0.55f;
                        if (Mathf.Abs(ty - wave) < 0.14f)
                        {
                            c = Color.Lerp(c, accentColor, 0.75f);
                        }
                    }
                    else // Sakura Blossom (type 5)
                    {
                        if ((x * 23 + y * 47) % 29 == 0 && ty > 0.2f)
                        {
                            c = new Color(1.0f, 0.7f, 0.85f, 0.95f);
                        }
                        if (ty < 0.2f + Mathf.Sin(tx * 10f) * 0.04f)
                        {
                            c = new Color(0.25f, 0.5f, 0.35f);
                        }
                    }

                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        c = new Color(0.12f, 0.25f, 0.45f, 0.9f);
                    }

                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakePresetPhotoTex(int index)
        {
            switch (index)
            {
                case 0:
                    return MakePhotoThumbTex(96, 64, new Color(0.05f, 0.1f, 0.3f), new Color(0.1f, 0.2f, 0.5f), new Color(0.04f, 0.08f, 0.18f), Color.yellow, 0);
                case 1:
                    return MakePhotoThumbTex(96, 64, new Color(0.1f, 0.4f, 0.7f), new Color(0.2f, 0.6f, 0.8f), new Color(0.1f, 0.5f, 0.4f), new Color(0.0f, 0.85f, 0.65f), 1);
                case 2:
                    return MakePhotoThumbTex(96, 64, new Color(0.2f, 0.05f, 0.35f), new Color(0.4f, 0.1f, 0.6f), new Color(0.1f, 0.05f, 0.2f), new Color(1.0f, 0.4f, 0.8f), 2);
                case 3:
                    return MakePhotoThumbTex(96, 64, new Color(0.5f, 0.15f, 0.25f), new Color(0.85f, 0.45f, 0.15f), new Color(0.95f, 0.75f, 0.2f), new Color(1.0f, 0.9f, 0.5f), 3);
                case 4:
                    return MakePhotoThumbTex(96, 64, new Color(0.05f, 0.15f, 0.35f), new Color(0.08f, 0.35f, 0.5f), new Color(0.02f, 0.12f, 0.25f), new Color(0.0f, 0.95f, 0.7f), 4);
                default:
                    return MakePhotoThumbTex(96, 64, new Color(0.4f, 0.15f, 0.3f), new Color(0.6f, 0.25f, 0.45f), new Color(0.2f, 0.08f, 0.18f), new Color(1.0f, 0.7f, 0.85f), 5);
            }
        }

        private const string PREF_KEY_PHOTOS_INIT = "SmartSpace_PhotosInitialized";
        private const string PREF_KEY_PHOTOS_DATA = "SmartSpace_PhotosData";

        private string GetPhotosDirectory()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "ProfilePhotos");
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void SaveFeaturedPhotosMetadata()
        {
            PlayerPrefs.SetInt(PREF_KEY_PHOTOS_INIT, 1);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _featuredPhotos.Count; i++)
            {
                var p = _featuredPhotos[i];
                if (i > 0) sb.Append(";");
                string safeTitle = p.title.Replace("|", "").Replace(";", "");
                sb.Append($"{p.id}|{safeTitle}|{p.likes}|{p.presetIndex}|{p.fileName}");
            }
            PlayerPrefs.SetString(PREF_KEY_PHOTOS_DATA, sb.ToString());
            PlayerPrefs.Save();
        }

        private void LoadFeaturedPhotosMetadata()
        {
            string raw = PlayerPrefs.GetString(PREF_KEY_PHOTOS_DATA, "");
            _featuredPhotos.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            string[] items = raw.Split(';');
            string dir = GetPhotosDirectory();

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item)) continue;
                string[] parts = item.Split('|');
                if (parts.Length < 5) continue;

                string id = parts[0];
                string title = parts[1];
                int.TryParse(parts[2], out int likes);
                int.TryParse(parts[3], out int presetIdx);
                string fileName = parts[4];

                Texture2D tex = null;
                if (!string.IsNullOrEmpty(fileName))
                {
                    string fullPath = System.IO.Path.Combine(dir, fileName);
                    if (System.IO.File.Exists(fullPath))
                    {
                        try
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                            tex = new Texture2D(2, 2);
                            tex.LoadImage(bytes);
                        }
                        catch { }
                    }
                }

                if (tex == null)
                {
                    tex = MakePresetPhotoTex(presetIdx >= 0 ? presetIdx : 0);
                }

                var photo = new ProfilePhotoItem(title, likes, tex, presetIdx, fileName);
                photo.id = id;
                _featuredPhotos.Add(photo);
            }
        }

        private void InitFeaturedPhotos()
        {
            if (_featuredPhotosInitialized) return;
            _featuredPhotosInitialized = true;

            bool hasSaved = PlayerPrefs.GetInt(PREF_KEY_PHOTOS_INIT, 0) == 1;
            if (hasSaved)
            {
                LoadFeaturedPhotosMetadata();
            }
            else
            {
                _featuredPhotos.Clear();
                _featuredPhotos.Add(new ProfilePhotoItem("星辉漫游", 328, MakePresetPhotoTex(0), 0));
                _featuredPhotos.Add(new ProfilePhotoItem("浮空圣岛", 215, MakePresetPhotoTex(1), 1));
                _featuredPhotos.Add(new ProfilePhotoItem("大星云境", 184, MakePresetPhotoTex(2), 2));
                _featuredPhotos.Add(new ProfilePhotoItem("熔金落日", 260, MakePresetPhotoTex(3), 3));
                _featuredPhotos.Add(new ProfilePhotoItem("极光天穹", 192, MakePresetPhotoTex(4), 4));
                SaveFeaturedPhotosMetadata();
            }

            _editingPhotos.Clear();
            _editingPhotos.AddRange(_featuredPhotos);

            if (_targetFeaturedPhotos.Count == 0)
            {
                _targetFeaturedPhotos.Add(new ProfilePhotoItem("漫游初遇", 218, MakePresetPhotoTex(1), 1));
                _targetFeaturedPhotos.Add(new ProfilePhotoItem("苍穹之上", 175, MakePresetPhotoTex(3), 3));
                _targetFeaturedPhotos.Add(new ProfilePhotoItem("星际列车", 290, MakePresetPhotoTex(4), 4));
                _targetFeaturedPhotos.Add(new ProfilePhotoItem("永恒花园", 142, MakePresetPhotoTex(2), 2));
            }
        }

        private Texture2D CaptureGameSnapshot(int width = 320, int height = 200)
        {
            Camera cam = null;
            if (ThirdPersonCamera.Instance != null)
            {
                cam = ThirdPersonCamera.Instance.GetComponent<Camera>();
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();

            if (cam != null)
            {
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                RenderTexture prevRt = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevRt;

                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                snapshot.Apply();

                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                return snapshot;
            }
            else
            {
                return MakePresetPhotoTex(0);
            }
        }

        private void CaptureNewSnapshot()
        {
            if (_uploadPreviewTex != null)
            {
                Destroy(_uploadPreviewTex);
            }
            _uploadPreviewTex = CaptureGameSnapshot(320, 200);
            _uploadPhotoTitle = $"空间漫游打卡_{DateTime.Now:MMdd}";
            _selectedPresetIndex = -1;
            AddSystemMessage("已成功捕获当前 3D 空间画面！📸");
        }

        private void BrowseLocalImage()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("选择本地照片", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    Texture2D loaded = new Texture2D(2, 2);
                    if (loaded.LoadImage(bytes))
                    {
                        if (_uploadPreviewTex != null)
                        {
                            Destroy(_uploadPreviewTex);
                        }
                        _uploadPreviewTex = loaded;
                        _lastLoadedLocalFileName = System.IO.Path.GetFileName(path);
                        _uploadPhotoTitle = System.IO.Path.GetFileNameWithoutExtension(path);
                        _selectedPresetIndex = -1;
                        AddSystemMessage($"已成功导入本地照片：{_lastLoadedLocalFileName} 📁");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Load local image failed: {ex.Message}");
                }
            }
#else
            AddSystemMessage("请使用实时拍照或精选题材进行上传。");
#endif
        }

        private void SelectPresetWallpaper(int index, string title)
        {
            _selectedPresetIndex = index;
            if (_uploadPreviewTex != null)
            {
                Destroy(_uploadPreviewTex);
            }
            _uploadPreviewTex = MakePresetPhotoTex(index);
            _uploadPhotoTitle = title;
            _lastLoadedLocalFileName = "";
        }

        private void OpenPhotoUploadModal()
        {
            _showPhotoUploadModal = true;
            _uploadSourceTab = 0;
            _uploadPhotoTitle = $"空间漫游打卡_{DateTime.Now:MMdd}";
            _selectedPresetIndex = -1;
            _lastLoadedLocalFileName = "";
            CaptureNewSnapshot();
        }

        private void ClosePhotoUploadModal()
        {
            _showPhotoUploadModal = false;
            if (_uploadPreviewTex != null)
            {
                Destroy(_uploadPreviewTex);
                _uploadPreviewTex = null;
            }
        }

        private void ConfirmUploadPhoto()
        {
            if (_uploadPreviewTex == null)
            {
                AddSystemMessage("<color=#FF5252>上传失败：未获取到有效照片！</color>");
                return;
            }

            string title = string.IsNullOrEmpty(_uploadPhotoTitle.Trim()) ? "精彩瞬间" : _uploadPhotoTitle.Trim();
            string savedFileName = "";

            if (_selectedPresetIndex < 0)
            {
                string id = Guid.NewGuid().ToString("N");
                savedFileName = $"photo_{id}.png";
                string fullPath = System.IO.Path.Combine(GetPhotosDirectory(), savedFileName);
                try
                {
                    byte[] pngBytes = _uploadPreviewTex.EncodeToPNG();
                    if (pngBytes != null)
                    {
                        System.IO.File.WriteAllBytes(fullPath, pngBytes);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Save photo file failed: {ex.Message}");
                }
            }

            Texture2D newTex = new Texture2D(_uploadPreviewTex.width, _uploadPreviewTex.height, TextureFormat.RGB24, false);
            newTex.SetPixels(_uploadPreviewTex.GetPixels());
            newTex.Apply();

            var newPhoto = new ProfilePhotoItem(title, UnityEngine.Random.Range(10, 80), newTex, _selectedPresetIndex, savedFileName);
            _featuredPhotos.Add(newPhoto);
            if (!_editingPhotos.Contains(newPhoto))
            {
                _editingPhotos.Add(newPhoto);
            }
            SaveFeaturedPhotosMetadata();

            AddSystemMessage($"已成功上传精选照片【<color=#FFE082>{title}</color>】到空间社交主页！📷✨");
            ClosePhotoUploadModal();
        }

        private void DeletePhoto(int index)
        {
            if (index < 0 || index >= _featuredPhotos.Count) return;
            var item = _featuredPhotos[index];
            string t = item.title;

            if (!string.IsNullOrEmpty(item.fileName))
            {
                string fullPath = System.IO.Path.Combine(GetPhotosDirectory(), item.fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    try { System.IO.File.Delete(fullPath); } catch { }
                }
            }

            if (item.texture != null)
            {
                Destroy(item.texture);
            }
            _featuredPhotos.RemoveAt(index);
            _editingPhotos.Remove(item);
            SaveFeaturedPhotosMetadata();
            AddSystemMessage($"已成功删除精选照片【<color=#FF8A80>{t}</color>】。🗑️");
        }

        private void CleanupTextures()
        {
            if (_texMainBg != null) Destroy(_texMainBg);
            if (_texTabActive != null) Destroy(_texTabActive);
            if (_texTabInactive != null) Destroy(_texTabInactive);
            if (_texTabDisabled != null) Destroy(_texTabDisabled);
            if (_texBubbleBlue != null) Destroy(_texBubbleBlue);
            if (_texSendBtnYellow != null) Destroy(_texSendBtnYellow);
            if (_texInputWhite != null) Destroy(_texInputWhite);
            if (_texEmojiDark != null) Destroy(_texEmojiDark);
            if (_texDrawerBg != null) Destroy(_texDrawerBg);
            if (_texMiniBarBg != null) Destroy(_texMiniBarBg);
            if (_texAvatarBorder != null) Destroy(_texAvatarBorder);
            if (_texAvatarSelf != null) Destroy(_texAvatarSelf);
            if (_texSmileyIcon != null) Destroy(_texSmileyIcon);
            if (_texHornIcon != null) Destroy(_texHornIcon);
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
            if (_texProfileFullCardBg != null) Destroy(_texProfileFullCardBg);
            if (_texProfileBackBtn != null) Destroy(_texProfileBackBtn);
            if (_texExpBarBg != null) Destroy(_texExpBarBg);
            if (_texExpBarFill != null) Destroy(_texExpBarFill);
            if (_texAuraRing != null) Destroy(_texAuraRing);
            if (_texHeartIcon != null) Destroy(_texHeartIcon);
            if (_texBadgeCardBg != null) Destroy(_texBadgeCardBg);
            if (_texVerticalTabActive != null) Destroy(_texVerticalTabActive);
            if (_texVerticalTabInactive != null) Destroy(_texVerticalTabInactive);

            if (_avatarTextures != null)
            {
                foreach (var t in _avatarTextures) if (t != null) Destroy(t);
            }
            if (_texAvatarOthers != null)
            {
                foreach (var t in _texAvatarOthers) if (t != null) Destroy(t);
            }

            if (_photosPendingDelete != null)
            {
                foreach (var p in _photosPendingDelete) if (p.texture != null) Destroy(p.texture);
                _photosPendingDelete.Clear();
            }
            if (_editingPhotos != null)
            {
                _editingPhotos.Clear();
            }
            if (_featuredPhotos != null)
            {
                foreach (var p in _featuredPhotos) if (p.texture != null) Destroy(p.texture);
                _featuredPhotos.Clear();
            }
            if (_targetFeaturedPhotos != null)
            {
                foreach (var p in _targetFeaturedPhotos) if (p.texture != null) Destroy(p.texture);
                _targetFeaturedPhotos.Clear();
            }
            if (_uploadPreviewTex != null)
            {
                Destroy(_uploadPreviewTex);
                _uploadPreviewTex = null;
            }
            if (_costumeItems != null)
            {
                foreach (var item in _costumeItems)
                {
                    if (item.iconTex != null) Destroy(item.iconTex);
                }
                _costumeItems.Clear();
                _costumesInitialized = false;
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
            _texTabDisabled = MakeSolidTex(2, 2, new Color(0.06f, 0.09f, 0.14f, 0.85f)); // Dim Dark Slate
            _texBubbleBlue = MakeBorderedTex(32, 32, new Color(0.08f, 0.44f, 0.82f, 0.96f), new Color(0.12f, 0.54f, 0.95f, 1f), 1);
            _texSendBtnYellow = MakeSolidTex(2, 2, new Color(1.0f, 0.86f, 0.18f, 1.0f)); // Golden-Yellow
            _texInputWhite = MakeSolidTex(2, 2, new Color(0.93f, 0.95f, 0.97f, 1.0f));
            _texEmojiDark = MakeSolidTex(2, 2, new Color(0.10f, 0.15f, 0.22f, 0.95f));
            _texDrawerBg = MakeBorderedTex(32, 32, new Color(0.06f, 0.10f, 0.20f, 0.98f), new Color(0.20f, 0.55f, 0.95f, 0.9f), 1);
            _texMiniBarBg = MakeSolidTex(2, 2, new Color(0.05f, 0.09f, 0.16f, 0.90f));
            _texAvatarBorder = MakeBorderedTex(42, 42, new Color(0.10f, 0.16f, 0.26f, 1f), new Color(0.20f, 0.55f, 0.95f, 1f), 2);
            _texSmileyIcon = MakeSmileyTex(34);
            _texHornIcon = MakeHornTex(34);
            _texHornBg = MakeBorderedTex(64, 64, new Color(0.25f, 0.18f, 0.05f, 0.95f), new Color(1.0f, 0.85f, 0.22f, 1.0f), 2);
            _texAchievementBg = MakeBorderedTex(64, 64, new Color(0.20f, 0.08f, 0.30f, 0.95f), new Color(0.92f, 0.55f, 1.0f, 1.0f), 2);

            // Modal & Profile Textures
            _texModalOverlay = MakeSolidTex(2, 2, new Color(0.02f, 0.04f, 0.08f, 0.78f)); // Dark backdrop
            _texModalCardBg = MakeBorderedTex(64, 64, new Color(0.06f, 0.10f, 0.19f, 0.98f), new Color(0.15f, 0.45f, 0.85f, 1.0f), 2);
            _texInputDark = MakeBorderedTex(32, 32, new Color(0.04f, 0.07f, 0.14f, 0.95f), new Color(0.22f, 0.38f, 0.62f, 1f), 1);
            _texPlayerBarBg = MakeBorderedTex(48, 48, new Color(0.05f, 0.09f, 0.18f, 0.92f), new Color(0.18f, 0.40f, 0.70f, 0.85f), 1);

            // Character Profile Textures (Aola Star 1:1 Interface)
            _texProfileFullCardBg = MakeBorderedTex(64, 64, new Color(0.04f, 0.08f, 0.16f, 0.94f), new Color(0.20f, 0.55f, 0.95f, 0.90f), 2);
            _texProfileBackBtn = MakeBorderedTex(48, 48, new Color(0.08f, 0.20f, 0.40f, 0.95f), new Color(0.30f, 0.70f, 1.0f, 1.0f), 2);
            _texExpBarBg = MakeSolidTex(2, 2, new Color(0.06f, 0.10f, 0.18f, 1.0f));
            _texExpBarFill = MakeSolidTex(2, 2, new Color(0.0f, 0.85f, 1.0f, 1.0f));
            _texAuraRing = MakeAuraRingTex(128);
            _texHeartIcon = MakeHeartTex(32);
            _texBadgeCardBg = MakeBorderedTex(32, 32, new Color(0.06f, 0.12f, 0.24f, 0.90f), new Color(0.20f, 0.45f, 0.80f, 0.80f), 1);
            _texVerticalTabActive = MakeBorderedTex(48, 48, new Color(0.12f, 0.50f, 0.95f, 0.95f), new Color(0.60f, 0.90f, 1.0f, 1.0f), 2);
            _texVerticalTabInactive = MakeBorderedTex(48, 48, new Color(0.06f, 0.12f, 0.22f, 0.85f), new Color(0.15f, 0.30f, 0.50f, 0.65f), 1);

            // 6 Distinct Procedural Avatars
            _avatarTextures = new Texture2D[UserProfile.AvatarNames.Length];
            for (int i = 0; i < _avatarTextures.Length; i++)
            {
                _avatarTextures[i] = MakeAvatarTex(UserProfile.AvatarPrimaryColors[i], UserProfile.AvatarSecondaryColors[i]);
            }

            _texAvatarSelf = _avatarTextures[0];
            _texAvatarOthers = new Texture2D[] { _avatarTextures[1], _avatarTextures[2], _avatarTextures[3] };

            InitFeaturedPhotos();
            InitCostumes();
            InitAchievementTitles();

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

            _tabDisabledStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.32f, 0.38f, 0.45f, 0.60f), background = _texTabDisabled },
                hover = { textColor = new Color(0.32f, 0.38f, 0.45f, 0.60f), background = _texTabDisabled },
                active = { textColor = new Color(0.32f, 0.38f, 0.45f, 0.60f), background = _texTabDisabled }
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

            _emojiCardBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.90f, 0.94f, 1.0f), background = _texContactNormal },
                hover = { textColor = Color.white, background = _texContactHover },
                padding = new RectOffset(1, 1, 1, 1)
            };

            // Profile View GUIStyles
            _profileCardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texProfileFullCardBg },
                padding = new RectOffset(18, 18, 16, 16)
            };

            _profileBackBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texProfileBackBtn },
                hover = { textColor = new Color(0.0f, 0.95f, 1.0f), background = _texTabActive }
            };

            _profileVerticalTabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texVerticalTabActive }
            };

            _profileVerticalTabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.65f, 0.85f, 1.0f), background = _texVerticalTabInactive },
                hover = { textColor = Color.white, background = _texTabActive }
            };

            _badgeCardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texBadgeCardBg },
                padding = new RectOffset(8, 8, 6, 6)
            };

            _profileStageHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = new Color(0.55f, 0.88f, 1.0f) },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _stylesInitialized = true;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            if (IsPlazaHUDHidden) return;

            InitStyles();

            // Track chat input focus & typing state for input protection
            _isChatInputFocused = (GUI.GetNameOfFocusedControl() == "AolaChatInputField");
            IsTyping = _isChatInputFocused || _showInitialSetupModal || _showSelfProfileModal || _showTargetProfileModal;

            // Full-Screen Aola Star Character Profile View (1:1 原画还原，3D角色展台，纯粹基础信息)
            if (_showSelfProfileModal)
            {
                DrawAolaCharacterProfileView(isSelf: true);
                return;
            }
            if (_showTargetProfileModal)
            {
                DrawAolaCharacterProfileView(isSelf: false);
                return;
            }
            if (_showInitialSetupModal)
            {
                DrawInitialSetupModal();
                return;
            }

            // 1. Top-Left Player Profile Bar (可查看与点击修改个人资料)
            DrawPlayerProfileBar();

            // 2. Independent Bot Button (独立于聊天系统的访客生成入口)
            DrawIndependentBotButton();

            // 3. Independent Team Button (独立于聊天系统的副本组队入口)
            DrawIndependentTeamButton();

            // 4. Chat Panel (2/3 Height Expanded OR Mini Collapsed Bar)
            if (_isExpanded)
            {
                DrawAolaStarChatWindow();
            }
            else
            {
                DrawCollapsedMiniBar();
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

        private void DrawIndependentBotButton()
        {
            float x = 16f + 320f + 10f;
            float y = 16f;
            Rect botRect = new Rect(x, y, 96f, 36f);
            if (GUI.Button(botRect, "🤖 生成访客", _modalSecondaryBtnStyle))
            {
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                {
                    NetworkManager.Instance.SpawnNetworkBot();
                }
                else
                {
                    AddSystemMessage("<color=#FF5252>未连接服务器，无法生成访客！</color>");
                }
            }
        }

        private void DrawIndependentTeamButton()
        {
            float x = 16f + 320f + 10f + 96f + 8f;
            float y = 16f;
            Rect teamRect = new Rect(x, y, 96f, 36f);

            if (!_isInTeam)
            {
                if (GUI.Button(teamRect, "⚔️ 副本组队", _modalSecondaryBtnStyle))
                {
                    JoinDungeonTeam("暗夜英雄副本", 4);
                    if (_isExpanded)
                    {
                        _currentChannel = ChatChannel.Team;
                    }
                }
            }
            else
            {
                if (GUI.Button(teamRect, "🚪 退出队伍", _tabActiveStyle))
                {
                    LeaveDungeonTeam();
                }
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
            float chatAreaX = tabColWidth + middleColWidth;

            float topHeaderH = 32f;
            float inputBarH = 50f;
            float inputBarY = panelHeight - inputBarH - 6f; // 固定锚定在面板底部，位置永不改变

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _mainPanelStyle);

            // -------------------------------------------------------------
            // A. LEFT VERTICAL CHANNEL TAB BAR (奥拉星左侧频道导航栏 + 底部大喇叭)
            // -------------------------------------------------------------
            // A1. Channel Tabs (从顶部排列至输入框水平线之上)
            GUILayout.BeginArea(new Rect(0, 0, tabColWidth, inputBarY - 4f));
            GUILayout.BeginVertical();

            DrawVerticalChannelTab("世界", ChatChannel.World);
            DrawVerticalChannelTab("系统", ChatChannel.System);
            DrawVerticalChannelTab("附近", ChatChannel.Nearby);
            DrawVerticalChannelTab("组队", ChatChannel.Team, isEnabled: _isInTeam);
            DrawVerticalChannelTab("好友", ChatChannel.Friend);
            DrawVerticalChannelTab("私聊", ChatChannel.Whisper);

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // A2. Left Horn Button (聊天喇叭放在世界、系统等选项那一列，和输入框水平齐平，不占用输入框空间)
            float hornW = 44f;
            float hornH = 44f;
            float hornX = (tabColWidth - hornW) * 0.5f;
            float hornY = inputBarY + (inputBarH - hornH) * 0.5f;
            Rect hornRect = new Rect(hornX, hornY, hornW, hornH);

            bool isHornActive = _inputChat.StartsWith("/horn ");
            GUIStyle hornBtnStyle = isHornActive ? _tabActiveStyle : _emojiRoundBtnStyle;
            if (GUI.Button(hornRect, new GUIContent(_texHornIcon, "全服大喇叭广播 (/horn)"), hornBtnStyle))
            {
                if (isHornActive)
                {
                    _inputChat = _inputChat.Substring(6);
                }
                else
                {
                    _inputChat = "/horn " + _inputChat.TrimStart();
                }
                GUI.FocusControl("AolaChatInputField");
            }

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
                else if (_currentChannel == ChatChannel.Team)
                {
                    shouldShow = (msg.Channel == ChatChannel.Team);
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
                    ChatChannel.Team => $"<color=#78909C>队伍 [{_currentTeamName}] 暂无发言，与队友商讨副本战术吧！</color>",
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
                float drawerH = 190f;
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
                if (Event.current.type == EventType.MouseDown && avRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    OpenTargetProfileModal(contact.id, contact.name, contact.avatarIndex);
                }

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

        private void DrawVerticalChannelTab(string label, ChatChannel channel, bool isEnabled = true)
        {
            if (!isEnabled)
            {
                bool prevEnabled = GUI.enabled;
                GUI.enabled = false;
                GUILayout.Button(label, _tabDisabledStyle, GUILayout.Height(44));
                GUI.enabled = prevEnabled;
                return;
            }

            bool isActive = (_currentChannel == channel);
            GUIStyle style = isActive ? _tabActiveStyle : _tabInactiveStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(44)))
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
            else if (_currentChannel == ChatChannel.Team)
            {
                GUILayout.Label($"<color=#00E676><b>● 队伍【{_currentTeamName}】</b></color>", _senderNameOtherStyle, GUILayout.Height(24));
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

            // 右上角操作按钮仅保留收起 (组队、喇叭、轮盘、访客均已独立于聊天系统)
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
                        ChatChannel.Team => "<color=#00E676>[组队]</color> ",
                        ChatChannel.Friend => "<color=#00E5FF>[好友]</color> ",
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
                        ChatChannel.Team => "<color=#00E676>[组队]</color> ",
                        ChatChannel.Friend => "<color=#00E5FF>[好友]</color> ",
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
            float inputW = Mathf.Max(120f, contentWidth - rightBtnsW);

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

            // 1. White Rounded Input Box (使用 GUILayout.TextArea 保证布局流稳定，容量扩至 200 字，独占左侧空间)
            GUI.SetNextControlName("AolaChatInputField");
            _inputChat = GUILayout.TextArea(_inputChat, 200, _inputFieldStyle, GUILayout.Width(inputW), GUILayout.Height(inputBarH));
            Rect inputRect = GUILayoutUtility.GetLastRect();

            // 占位提示仅在 Repaint 阶段直接绘制，不生成多余控件 ID，杜绝控件 ID 偏移
            bool isHornActive = _inputChat.StartsWith("/horn ");
            if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(_inputChat) && !isFocused)
            {
                string hint = isHornActive ? "输入全服大喇叭内容..." : "请点击输入...";
                if (!isHornActive)
                {
                    if (_currentChannel == ChatChannel.Friend)
                    {
                        hint = string.IsNullOrEmpty(_selectedFriendId) ? "点击输入 (请先在左侧选择好友)..." : $"对 [{_selectedFriendName}] 说...";
                    }
                    else if (_currentChannel == ChatChannel.Whisper)
                    {
                        hint = string.IsNullOrEmpty(_whisperTargetId) ? "点击输入 (请先在左侧选择目标)..." : $"对 [{_whisperTargetName}] 说...";
                    }
                    else if (_currentChannel == ChatChannel.Team)
                    {
                        hint = $"在队伍 [{_currentTeamName}] 中发言...";
                    }
                }
                _placeholderStyle.Draw(new Rect(inputRect.x + 8, inputRect.y + 7, inputRect.width - 16, 20), hint, false, false, false, false);
            }

            GUILayout.Space(4);

            // Right side buttons container (vertically centered with the tall input box)
            GUILayout.BeginVertical(GUILayout.Height(inputBarH));
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            // 3. Yellow Smiley Emoji Button (1:1 Procedural Icon matching reference image)
            if (GUILayout.Button(_texSmileyIcon, _emojiRoundBtnStyle, GUILayout.Width(36), GUILayout.Height(36)))
            {
                _showQuickEmojiDrawer = !_showQuickEmojiDrawer;
            }

            GUILayout.Space(4);

            // 4. Vibrant Golden-Yellow Send Button
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

            // 1. Top Category Tabs Bar (❤️ 常用, 🐱 伊乐, 👾 像素, 😈 恶魔, 🌟 Q版, 💬 常用语)
            GUILayout.BeginHorizontal(GUILayout.Height(24));
            string[] tabNames = new string[] { "❤️ 常用", "🐱 伊乐", "👾 像素", "😈 恶魔", "🌟 Q版", "💬 常用语" };
            for (int t = 0; t < tabNames.Length; t++)
            {
                bool isSel = (_emojiDrawerTab == t);
                GUIStyle tStyle = isSel ? _tabActiveStyle : _tabInactiveStyle;
                float w = (t == 0) ? 58f : ((t == 5) ? 68f : 54f);
                if (GUILayout.Button(tabNames[t], tStyle, GUILayout.Width(w), GUILayout.Height(22)))
                {
                    _emojiDrawerTab = t;
                }
                GUILayout.Space(2);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(22), GUILayout.Height(20)))
            {
                _showQuickEmojiDrawer = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // 2. Content Area: Either Emoji 2x5 Grid OR Quick Phrases
            if (_emojiDrawerTab < 5)
            {
                EmojiCardItem[] currentCategory = GetCategoryEmojis(_emojiDrawerTab);
                // 2 Rows x 5 Columns = 10 Emojis
                for (int r = 0; r < 2; r++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 5; c++)
                    {
                        int idx = r * 5 + c;
                        if (idx < currentCategory.Length)
                        {
                            var item = currentCategory[idx];
                            string btnText = $"<size=12><b>{item.icon}</b></size>\n<size=10><color=#ECEFF1>{item.name}</color></size>";
                            if (GUILayout.Button(btnText, _emojiCardBtnStyle, GUILayout.Height(48)))
                            {
                                if (string.IsNullOrEmpty(_inputChat.Trim()))
                                {
                                    SendMessageContent($"[{item.name}] {item.icon}");
                                    _showQuickEmojiDrawer = false;
                                }
                                else
                                {
                                    _inputChat += $"{item.icon}[{item.name}]";
                                    GUI.FocusControl("AolaChatInputField");
                                }
                            }
                        }
                        if (c < 4) GUILayout.Space(3);
                    }
                    GUILayout.EndHorizontal();
                    if (r == 0) GUILayout.Space(3);
                }

                GUILayout.Space(3);

                // 3. Bottom Pagination Dots (● ○ ○)
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("<color=#00E5FF>●</color>  <color=#78909C>○</color>  <color=#78909C>○</color>", _marqueeStyle, GUILayout.Height(15));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                // Tab 5: 💬 常用语 (保留原有的快速用语)
                for (int i = 0; i < _quickPhrases.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(_quickPhrases[i], _drawerItemStyle, GUILayout.Height(24)))
                    {
                        SendMessageContent(_quickPhrases[i]);
                        _showQuickEmojiDrawer = false;
                    }
                    if (i + 1 < _quickPhrases.Length)
                    {
                        if (GUILayout.Button(_quickPhrases[i + 1], _drawerItemStyle, GUILayout.Height(24)))
                        {
                            SendMessageContent(_quickPhrases[i + 1]);
                            _showQuickEmojiDrawer = false;
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (i + 2 < _quickPhrases.Length) GUILayout.Space(3);
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

            // 3. 队伍快捷指令 (/team, /jointeam, /leaveteam)
            if (content.Equals("/team", StringComparison.OrdinalIgnoreCase) ||
                content.Equals("/jointeam", StringComparison.OrdinalIgnoreCase))
            {
                JoinDungeonTeam("暗夜之城·英雄副本 队伍", 4);
                _currentChannel = ChatChannel.Team;
                return true;
            }
            if (content.Equals("/leaveteam", StringComparison.OrdinalIgnoreCase) ||
                content.Equals("/quitteam", StringComparison.OrdinalIgnoreCase))
            {
                LeaveDungeonTeam();
                return true;
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
            else if (_currentChannel == ChatChannel.Team)
            {
                if (!_isInTeam)
                {
                    AddSystemMessage("<color=#FF5252>当前不在副本队伍中，无法发送组队消息！</color>");
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
                    Channel = ChatChannel.Team,
                    SenderId = myId,
                    SenderName = myName,
                    Content = content,
                    TimeStr = DateTime.Now.ToString("HH:mm"),
                    IsSelf = true,
                    AvatarIndex = myAvatar,
                    Level = NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null ? NetworkManager.Instance.LocalProfile.level : 1,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                StartCoroutine(SimulateTeamReplyRoutine(content));
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
            bool wheelOpen = EmoteWheelUI.Instance != null && EmoteWheelUI.Instance.IsOpen;
            if (wheelOpen)
            {
                // 当动作/表情底栏展开时（见示图1），收缩聊天栏完全隐藏避让，避免重叠
                return;
            }

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
                    ChatChannel.Team => "<color=#00E676>[组队]</color>",
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

            // 2. 独立动作/轮盘入口 (放在收缩聊天窗口的右边，打开聊天界面后不可见)
            float actionBtnX = barX + barW + 10f;
            float actionBtnY = barY;
            float actionBtnW = 86f;
            float actionBtnH = barH;
            Rect actionRect = new Rect(actionBtnX, actionBtnY, actionBtnW, actionBtnH);

            if (GUI.Button(actionRect, "💃 动作", _tabInactiveStyle))
            {
                if (EmoteWheelUI.Instance != null)
                {
                    EmoteWheelUI.Instance.ToggleOpen();
                }
            }
        }

        public void CloseProfileModal()
        {
            _showSelfProfileModal = false;
            _showTargetProfileModal = false;
            _isDraggingChar = false;
            _isEditingName = false;
            _isEditingBio = false;
            _isEditingDetails = false;
            _isEditingPhotos = false;
            _photoToDeleteIndex = -1;
            _editingPhotos.Clear();
            _editingPhotos.AddRange(_featuredPhotos);
            _photosPendingDelete.Clear();
            ClosePhotoUploadModal();

            // 0. Close independent wardrobe studio scene
            WardrobeSceneController.CloseProfileStudio();

            // 1. Reset Camera mode
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.IsProfileViewMode = false;
                ThirdPersonCamera.Instance.ProfileTarget = null;
            }

            // 2. Restore Overhead UI visibility
            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var ov = NetworkManager.Instance.LocalPlayerObject.GetComponentInChildren<PlayerOverheadUI>();
                if (ov != null) ov.SetVisible(true);
            }
            if (NetworkManager.Instance != null)
            {
                foreach (var kvp in NetworkManager.Instance.SpawnedPlayers)
                {
                    if (kvp.Value != null)
                    {
                        var ov = kvp.Value.GetComponentInChildren<PlayerOverheadUI>();
                        if (ov != null) ov.SetVisible(true);
                    }
                }
            }

            // 3. Apply confirmed costume & title to local player in plaza
            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                if (lpc != null && NetworkManager.Instance.LocalProfile != null)
                {
                    lpc.ApplyAvatarVisual(NetworkManager.Instance.LocalProfile.costumeId);
                    lpc.UpdateOverheadTitle(NetworkManager.Instance.LocalProfile.title);
                }
            }
        }

        private void InitBirthdayEditFields()
        {
            var p = (_editingProfile != null) ? _editingProfile : ((NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null) ? NetworkManager.Instance.LocalProfile : UserProfile.LoadFromPrefs());
            if (p == null) return;

            if (string.IsNullOrEmpty(p.birthday) || p.birthday == "保密")
            {
                _isBirthSecret = true;
                _birthYearStr = "2002";
                _birthMonthStr = "05";
                _birthDayStr = "20";
            }
            else
            {
                _isBirthSecret = false;
                string[] parts = p.birthday.Split('-');
                if (parts.Length == 3)
                {
                    _birthYearStr = parts[0];
                    _birthMonthStr = parts[1];
                    _birthDayStr = parts[2];
                }
                else if (parts.Length == 2)
                {
                    _birthYearStr = "2002";
                    _birthMonthStr = parts[0];
                    _birthDayStr = parts[1];
                }
                else
                {
                    _birthYearStr = "2002";
                    _birthMonthStr = "05";
                    _birthDayStr = "20";
                }
            }
        }

        private void SaveAllEditingProfile(UserProfile prof)
        {
            if (prof == null) return;

            // 1. Nickname
            if (_isEditingName && !string.IsNullOrEmpty(_editingProfile.username.Trim()))
            {
                prof.username = _editingProfile.username.Trim();
            }
            else
            {
                _editingProfile.username = prof.username;
            }

            // 2. Bio
            if (_isEditingBio)
            {
                prof.bio = _editingProfile.bio;
            }

            // 3. Details (Gender, Region, Birthday, Status)
            if (_isEditingDetails)
            {
                prof.gender = _editingProfile.gender;
                prof.region = _editingProfile.region;
                if (_isBirthSecret)
                {
                    _editingProfile.birthday = "保密";
                }
                else
                {
                    int.TryParse(_birthYearStr, out int yVal);
                    int.TryParse(_birthMonthStr, out int mVal);
                    int.TryParse(_birthDayStr, out int dVal);
                    if (yVal < 1920 || yVal > 2026) yVal = 2002;
                    if (mVal < 1 || mVal > 12) mVal = 5;
                    if (dVal < 1 || dVal > 31) dVal = 20;
                    _editingProfile.birthday = $"{yVal:D4}-{mVal:D2}-{dVal:D2}";
                }
                prof.birthday = _editingProfile.birthday;
                prof.status = _editingProfile.status;
            }

            // 4. Photos (Commit staged deletions)
            if (_photosPendingDelete.Count > 0)
            {
                foreach (var p in _photosPendingDelete)
                {
                    if (!_editingPhotos.Contains(p))
                    {
                        if (!string.IsNullOrEmpty(p.fileName))
                        {
                            string fullPath = System.IO.Path.Combine(GetPhotosDirectory(), p.fileName);
                            if (System.IO.File.Exists(fullPath))
                            {
                                try { System.IO.File.Delete(fullPath); } catch { }
                            }
                        }
                        if (p.texture != null)
                        {
                            Destroy(p.texture);
                        }
                    }
                }
                _photosPendingDelete.Clear();
            }
            _featuredPhotos.Clear();
            _featuredPhotos.AddRange(_editingPhotos);
            SaveFeaturedPhotosMetadata();

            prof.SaveToPrefs();
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UpdateProfile(prof);
            }

            _isEditingName = false;
            _isEditingBio = false;
            _isEditingDetails = false;
            _isEditingPhotos = false;
            _photoToDeleteIndex = -1;
            AddSystemMessage("个人资料与相册已快速保存！✨");
        }

        private void CancelAllEditingProfile(UserProfile prof)
        {
            if (prof != null)
            {
                _editingProfile.username = prof.username;
                _editingProfile.bio = prof.bio;
                _editingProfile.gender = prof.gender;
                _editingProfile.region = prof.region;
                _editingProfile.birthday = prof.birthday;
                _editingProfile.status = prof.status;
            }
            InitBirthdayEditFields();
            _editingPhotos.Clear();
            _editingPhotos.AddRange(_featuredPhotos);
            _photosPendingDelete.Clear();
            _photoToDeleteIndex = -1;
            _isEditingName = false;
            _isEditingBio = false;
            _isEditingDetails = false;
            _isEditingPhotos = false;
        }

        private void OpenSelfProfileModal()
        {
            var localProf = (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
                ? NetworkManager.Instance.LocalProfile
                : UserProfile.LoadFromPrefs();
            _editingProfile = localProf.Clone();
            _editingAgeStr = _editingProfile.age > 0 ? _editingProfile.age.ToString() : "";
            _previewCostumeId = _editingProfile.costumeId;
            _profileTab = 0;
            _isEditingName = false;
            _isEditingBio = false;
            _isEditingDetails = false;
            _isEditingPhotos = false;
            _photoToDeleteIndex = -1;
            _editingPhotos.Clear();
            _editingPhotos.AddRange(_featuredPhotos);
            _photosPendingDelete.Clear();
            InitBirthdayEditFields();
            _showSelfProfileModal = true;
            _showTargetProfileModal = false;

            // 开启独立 3D 摄影棚展台场景（与主广场彻底隔离，零穿模零干扰）
            WardrobeSceneController.OpenProfileStudio(_editingProfile);

            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var ov = NetworkManager.Instance.LocalPlayerObject.GetComponentInChildren<PlayerOverheadUI>();
                if (ov != null) ov.SetVisible(false);
            }
        }

        private void OpenTargetProfileModal(string senderId, string senderName, int avatarIndex)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.OnlineProfiles.TryGetValue(senderId, out var prof))
            {
                _targetProfile = prof.Clone();
            }
            else
            {
                int hash = Math.Abs(senderId.GetHashCode());
                _targetProfile = new UserProfile
                {
                    username = senderName,
                    avatarId = avatarIndex,
                    gender = (hash % 2 == 0) ? "male" : "female",
                    age = (hash % 10) + 16,
                    bio = "在奥拉星智慧空间漫游，探索未知世界！✨",
                    level = (hash % 30) + 15,
                    uid = (hash % 9000000 + 1000000).ToString(),
                    region = "中国湖北",
                    birthday = $"{1996 + (hash % 12)}-0{(hash % 9) + 1}-{(hash % 20) + 10:D2}",
                    status = UserProfile.StatusPresets[hash % UserProfile.StatusPresets.Length],
                    title = "天赋异禀",
                    flowers = (hash % 3000) + 1200,
                    achievePoints = (hash % 2000) + 1500,
                    residenceDays = (hash % 500) + 50,
                    friendsCount = (hash % 20) + 5,
                    costumeId = avatarIndex
                };
            }
            _targetProfileSessionId = senderId;
            _profileTab = 0;
            _previewCostumeId = _targetProfile.costumeId;
            _showTargetProfileModal = true;
            _showSelfProfileModal = false;

            // Camera close-up focus on target player (if spawned), else fallback to local player
            Transform targetTransform = null;
            if (NetworkManager.Instance != null && NetworkManager.Instance.SpawnedPlayers.TryGetValue(senderId, out var targetObj) && targetObj != null)
            {
                targetTransform = targetObj.transform;
                var ov = targetObj.GetComponentInChildren<PlayerOverheadUI>();
                if (ov != null) ov.SetVisible(false);
            }
            else if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                targetTransform = NetworkManager.Instance.LocalPlayerObject.transform;
            }

            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var localOv = NetworkManager.Instance.LocalPlayerObject.GetComponentInChildren<PlayerOverheadUI>();
                if (localOv != null) localOv.SetVisible(false);
            }

            // 开启独立 3D 摄影棚展台场景展示目标玩家
            WardrobeSceneController.OpenProfileStudio(_targetProfile);
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

        #region Aola Star Character Profile View (1:1 风格重构)

        private void DrawAolaCharacterProfileView(bool isSelf)
        {
            var prof = isSelf
                ? ((NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null) ? NetworkManager.Instance.LocalProfile : _editingProfile)
                : _targetProfile;

            if (prof == null)
            {
                CloseProfileModal();
                return;
            }

            // 1. Subtle Background Overlay (若无 3D 摄影棚展台则全屏半透明暗化，若有则保持 3D 展台通透透亮)
            if (ShowcaseStudioStage.Instance == null || !ShowcaseStudioStage.Instance.IsActive)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texModalOverlay);
            }

            // 2. Right Main Card Dimensions & Left Blank Space Center
            float tabW = 46f;
            float cardW = Mathf.Clamp(Screen.width * 0.54f, 550f, 740f);
            float cardH = Mathf.Clamp(Screen.height * 0.85f, 510f, 660f);
            float cardX = Screen.width - cardW - tabW - 24f;
            float cardY = (Screen.height - cardH) * 0.5f + 16f;
            float leftBlankCenterX = cardX * 0.5f;

            // 3. 3D Character Interactive Dragging & Aura Base (左侧空白区域视觉正中)
            Transform activeTarget = (ThirdPersonCamera.Instance != null && ThirdPersonCamera.Instance.ProfileTarget != null)
                ? ThirdPersonCamera.Instance.ProfileTarget
                : (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null ? NetworkManager.Instance.LocalPlayerObject.transform : null);

            HandleProfileCharacterDrag(activeTarget, cardX);

            // Draw glowing ground aura ring under character (居中对齐人物脚底)
            float ringW = Mathf.Clamp(cardX * 0.52f, 240f, 380f);
            float ringH = ringW * 0.28f;
            float ringX = leftBlankCenterX - (ringW * 0.5f);
            float ringY = Screen.height * 0.80f;
            if (_texAuraRing != null)
            {
                GUI.color = new Color(0.0f, 0.85f, 1.0f, 0.45f);
                GUI.DrawTexture(new Rect(ringX, ringY, ringW, ringH), _texAuraRing, ScaleMode.StretchToFill);
                GUI.color = Color.white;
            }

            // Drag hint text (仅在非时装页展示，居中对齐左侧空白区中点)
            if (_profileTab != 1)
            {
                Rect hintRect = new Rect(leftBlankCenterX - 240f, Screen.height * 0.90f, 480f, 24f);
                GUI.Label(hintRect, "<color=#80D8FF>❖ 按住鼠标右键漫游环绕 · 滚轮缩放视角（双击复位） ❖</color>", _profileStageHintStyle ?? _marqueeStyle);
            }

            // 4. Top-Left Header: Back Button
            Rect backRect = new Rect(24f, 20f, 130f, 38f);
            if (GUI.Button(backRect, "◀  返回 角色", _profileBackBtnStyle))
            {
                CloseProfileModal();
                return;
            }

            // Top Header Title (无任何货币图标)
            Rect titleRect = new Rect(170f, 22f, 320f, 34f);
            string viewTitle = _profileTab switch
            {
                0 => isSelf ? "个人资料档案" : $"{prof.username} 的个人档案",
                1 => isSelf ? "时装衣橱 · 3D实时预览" : $"{prof.username} 的时装搭配",
                2 => isSelf ? "成就荣誉殿堂 · 空间专属称号" : $"{prof.username} 的成就荣誉",
                _ => "个人资料档案"
            };
            GUI.Label(titleRect, $"<size=17><b><color=#00E5FF>◆</color> {viewTitle} <color=#00E5FF>◆</color></b></size>", _senderNameOtherStyle);

            if (_profileTab == 1)
            {
                DrawCostumeLeftOverlay();
            }

            // 5. Draw Vertical Right Tabs (信息 / 时装 / 称号)
            float tabY0 = cardY + 20f;
            float tabH = 74f;
            if (GUI.Button(new Rect(cardX + cardW + 2f, tabY0, tabW, tabH), "信\n息", _profileTab == 0 ? _profileVerticalTabActiveStyle : _profileVerticalTabInactiveStyle))
            {
                _profileTab = 0;
            }
            if (GUI.Button(new Rect(cardX + cardW + 2f, tabY0 + tabH + 6f, tabW, tabH), "时\n装", _profileTab == 1 ? _profileVerticalTabActiveStyle : _profileVerticalTabInactiveStyle))
            {
                _profileTab = 1;
            }
            if (GUI.Button(new Rect(cardX + cardW + 2f, tabY0 + (tabH + 6f) * 2, tabW, tabH), "称\n号", _profileTab == 2 ? _profileVerticalTabActiveStyle : _profileVerticalTabInactiveStyle))
            {
                _profileTab = 2;
            }

            // 5. Main Card Content Area
            GUILayout.BeginArea(new Rect(cardX, cardY, cardW, cardH), _profileCardStyle);
            GUILayout.BeginVertical();

            if (_profileTab == 0)
            {
                DrawProfileInfoTab(prof, isSelf, cardW);
            }
            else if (_profileTab == 1)
            {
                DrawCostumeWardrobeTab(prof, isSelf);
            }
            else if (_profileTab == 2)
            {
                DrawAchievementTitlesTab(prof, isSelf);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            if (isSelf && _showPhotoUploadModal)
            {
                DrawPhotoUploadModal();
            }
        }

        private void HandleProfileCharacterDrag(Transform charTransform, float cardX)
        {
            if (charTransform == null && WardrobeSceneController.Instance == null) return;

            Event e = Event.current;
            Rect dragArea = new Rect(0, 60, cardX, Screen.height - 100);

            // 双击右键一键重置视角机位与旋转
            if (e.type == EventType.MouseDown && e.button == 1 && e.clickCount == 2 && dragArea.Contains(e.mousePosition))
            {
                if (WardrobeSceneController.Instance != null)
                {
                    WardrobeSceneController.Instance.ResetView();
                }
                e.Use();
                return;
            }

            // 按下鼠标右键 (button 1) 全向自由旋转 3D 人物与调节上下视角
            if (e.type == EventType.MouseDown && e.button == 1 && dragArea.Contains(e.mousePosition))
            {
                _isDraggingChar = true;
                _lastDragPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 1 && _isDraggingChar)
            {
                _isDraggingChar = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingChar)
            {
                float deltaX = e.mousePosition.x - _lastDragPos.x;
                float deltaY = e.mousePosition.y - _lastDragPos.y;
                _lastDragPos = e.mousePosition;
                if (WardrobeSceneController.Instance != null)
                {
                    WardrobeSceneController.Instance.AddOrbitInput(deltaX, deltaY);
                }
                else if (charTransform != null)
                {
                    charTransform.Rotate(Vector3.up, -deltaX * 0.75f, Space.World);
                }
                e.Use();
            }

            // 安全重置：若用户在右键释放时事件被拦截，确保不卡住拖拽
            if (!Input.GetMouseButton(1) && _isDraggingChar)
            {
                _isDraggingChar = false;
            }
        }

        private void DrawProfileInfoTab(UserProfile prof, bool isSelf, float cardW)
        {
            _profileScrollPos = GUILayout.BeginScrollView(_profileScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

            // ==================== Row 1: Header (Avatar + Level + Name + Basic Info) ====================
            GUILayout.BeginHorizontal();

            // Large Avatar (66x66)
            Rect avRect = GUILayoutUtility.GetRect(66, 66, GUILayout.Width(66), GUILayout.Height(66));
            GUI.DrawTexture(avRect, _texAvatarBorder);
            GUI.DrawTexture(new Rect(avRect.x + 3, avRect.y + 3, 60, 60), GetAvatarTex(prof.avatarId));
            Rect lvlBadge = new Rect(avRect.x + 2, avRect.y + 46, 36, 16);
            GUI.DrawTexture(lvlBadge, _texMiniBarBg);
            GUI.Label(lvlBadge, $"Lv.{prof.level}", _avatarLevelStyle);

            GUILayout.Space(14);
            GUILayout.BeginVertical();

            // Row 1A: Username + Gender Icon + Edit Name + Flower Badge
            GUILayout.BeginHorizontal();
            string gSymbol = UserProfile.GetGenderSymbol(prof.gender);
            string gColor = UserProfile.GetGenderColor(prof.gender);

            if (isSelf && _isEditingName)
            {
                _editingProfile.username = GUILayout.TextField(_editingProfile.username, 14, _modalInputStyle, GUILayout.Width(130), GUILayout.Height(26));
                if (GUILayout.Button("✓", _sideActionBtnStyle, GUILayout.Width(26), GUILayout.Height(24)))
                {
                    if (string.IsNullOrWhiteSpace(_editingProfile.username))
                    {
                        _editingProfile.username = UserProfile.GenerateRandomNickname();
                    }
                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateProfile(_editingProfile);
                    }
                    _isEditingName = false;
                }
                if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(26), GUILayout.Height(24)))
                {
                    _editingProfile.username = prof.username;
                    _isEditingName = false;
                }
            }
            else
            {
                GUILayout.Label($"<size=18><b>{prof.username}</b></size> <size=16><color={gColor}>{gSymbol}</color></size>", _playerBarNameStyle);
                if (isSelf)
                {
                    if (GUILayout.Button("✏️", _sideActionBtnStyle, GUILayout.Width(28), GUILayout.Height(22)))
                    {
                        _isEditingName = true;
                    }
                }
            }

            GUILayout.FlexibleSpace();

            // Heart / Flowers Received Count (送花统计)
            Rect flowerRect = GUILayoutUtility.GetRect(96, 26, GUILayout.Width(96), GUILayout.Height(26));
            GUI.DrawTexture(flowerRect, _texMiniBarBg);
            if (_texHeartIcon != null)
            {
                GUI.DrawTexture(new Rect(flowerRect.x + 6, flowerRect.y + 5, 16, 16), _texHeartIcon);
            }
            GUI.Label(new Rect(flowerRect.x + 26, flowerRect.y + 1, 66, 24), $"<b>{prof.flowers}</b>", _systemContentStyle);

            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Row 1B: Level Experience Progress Bar (经验条)
            GUILayout.BeginHorizontal();
            float expRatio = Mathf.Clamp01((prof.level % 10) / 10f);
            if (expRatio <= 0.05f) expRatio = 0.65f;
            Rect expBg = GUILayoutUtility.GetRect(cardW - 130, 8, GUILayout.Height(8));
            GUI.DrawTexture(expBg, _texMiniBarBg);
            GUI.DrawTexture(new Rect(expBg.x, expBg.y, expBg.width * expRatio, expBg.height), _texTabActive);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Row 1C: 编号 & 性别
            GUILayout.BeginHorizontal();
            string uidStr = !string.IsNullOrEmpty(prof.uid) ? prof.uid : "9474911";
            GUILayout.Label($"<color=#90A4AE>编号: </color><color=#80D8FF>{uidStr}</color>", _playerBarTagStyle);
            GUILayout.Space(16);
            string gLabel = UserProfile.GetGenderLabel(prof.gender);
            GUILayout.Label($"<color=#90A4AE>性别: </color><color={gColor}>{gSymbol} {gLabel}</color>", _playerBarTagStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Row 1D: 状态 (放到编号下面一点点)
            GUILayout.BeginHorizontal();
            string curStatus = string.IsNullOrEmpty(prof.status) ? "休闲中 ☕" : prof.status;
            GUILayout.Label($"<color=#90A4AE>状态: </color><color=#00E5FF><b>{curStatus}</b></color>", _playerBarTagStyle);
            if (isSelf && !_isEditingDetails)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("✏️ 编辑资料", _sideActionBtnStyle, GUILayout.Width(76), GUILayout.Height(20)))
                {
                    InitBirthdayEditFields();
                    _isEditingDetails = true;
                    _isEditingPhotos = true;
                    _editingPhotos.Clear();
                    _editingPhotos.AddRange(_featuredPhotos);
                    _photosPendingDelete.Clear();
                    _photoToDeleteIndex = -1;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Inline Details Editing Panel (Self Only)
            if (isSelf && _isEditingDetails)
            {
                GUILayout.Space(6);
                GUILayout.BeginVertical(_drawerBoxStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<size=11><color=#80D8FF><b>❖ 编辑基础资料（性别 / 属地 / 生日 / 状态）</b></color></size>", _modalLabelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✓ 保存", _modalPrimaryBtnStyle, GUILayout.Width(54), GUILayout.Height(22)))
                {
                    prof.gender = _editingProfile.gender;
                    prof.region = _editingProfile.region;
                    if (_isBirthSecret)
                    {
                        _editingProfile.birthday = "保密";
                    }
                    else
                    {
                        int.TryParse(_birthYearStr, out int yVal);
                        int.TryParse(_birthMonthStr, out int mVal);
                        int.TryParse(_birthDayStr, out int dVal);
                        if (yVal < 1920 || yVal > 2026) yVal = 2002;
                        if (mVal < 1 || mVal > 12) mVal = 5;
                        if (dVal < 1 || dVal > 31) dVal = 20;
                        _editingProfile.birthday = $"{yVal:D4}-{mVal:D2}-{dVal:D2}";
                    }
                    prof.birthday = _editingProfile.birthday;
                    prof.status = _editingProfile.status;
                    prof.SaveToPrefs();
                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateProfile(_editingProfile);
                    }
                    _isEditingDetails = false;
                    AddSystemMessage("个人基础资料已更新！✨");
                }
                GUILayout.Space(4);
                if (GUILayout.Button("✕ 取消", _modalSecondaryBtnStyle, GUILayout.Width(50), GUILayout.Height(22)))
                {
                    _editingProfile.gender = prof.gender;
                    _editingProfile.region = prof.region;
                    _editingProfile.birthday = prof.birthday;
                    _editingProfile.status = prof.status;
                    InitBirthdayEditFields();
                    _isEditingDetails = false;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Gender row
                GUILayout.BeginHorizontal();
                GUILayout.Label("<size=11><color=#90A4AE>性别:</color></size>", _marqueeStyle, GUILayout.Width(42));
                string[] genders = new string[] { "male", "female", "secret" };
                string[] gLabels = new string[] { "♂ 男生", "♀ 女生", "✦ 保密" };
                for (int g = 0; g < 3; g++)
                {
                    bool isSel = _editingProfile.gender == genders[g];
                    if (GUILayout.Button(gLabels[g], isSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Width(68), GUILayout.Height(22)))
                    {
                        _editingProfile.gender = genders[g];
                    }
                    GUILayout.Space(4);
                }
                GUILayout.Space(10);
                // Region input
                GUILayout.Label("<size=11><color=#90A4AE>属地:</color></size>", _marqueeStyle, GUILayout.Width(42));
                _editingProfile.region = GUILayout.TextField(_editingProfile.region, 20, _modalInputStyle, GUILayout.Width(90), GUILayout.Height(22));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Birthday row (年月日设置 + 保密切换)
                GUILayout.BeginHorizontal();
                GUILayout.Label("<size=11><color=#90A4AE>生日:</color></size>", _marqueeStyle, GUILayout.Width(42));
                if (_isBirthSecret)
                {
                    GUILayout.Label("<size=11><color=#FFE082>✦ 已设为保密</color></size>", _playerBarTagStyle, GUILayout.Width(92), GUILayout.Height(22));
                    GUILayout.Space(6);
                    if (GUILayout.Button("设置年月日", _sideActionBtnStyle, GUILayout.Width(82), GUILayout.Height(22)))
                    {
                        _isBirthSecret = false;
                        if (string.IsNullOrEmpty(_birthYearStr)) _birthYearStr = "2002";
                        if (string.IsNullOrEmpty(_birthMonthStr)) _birthMonthStr = "05";
                        if (string.IsNullOrEmpty(_birthDayStr)) _birthDayStr = "20";
                    }
                }
                else
                {
                    _birthYearStr = GUILayout.TextField(_birthYearStr, 4, _modalInputStyle, GUILayout.Width(46), GUILayout.Height(22));
                    GUILayout.Label("<color=#90A4AE>年</color>", _playerBarTagStyle, GUILayout.Width(18), GUILayout.Height(22));
                    _birthMonthStr = GUILayout.TextField(_birthMonthStr, 2, _modalInputStyle, GUILayout.Width(28), GUILayout.Height(22));
                    GUILayout.Label("<color=#90A4AE>月</color>", _playerBarTagStyle, GUILayout.Width(18), GUILayout.Height(22));
                    _birthDayStr = GUILayout.TextField(_birthDayStr, 2, _modalInputStyle, GUILayout.Width(28), GUILayout.Height(22));
                    GUILayout.Label("<color=#90A4AE>日</color>", _playerBarTagStyle, GUILayout.Width(18), GUILayout.Height(22));
                    GUILayout.Space(8);
                    if (GUILayout.Button("设为保密", _sideActionBtnStyle, GUILayout.Width(68), GUILayout.Height(22)))
                    {
                        _isBirthSecret = true;
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Status row
                GUILayout.BeginHorizontal();
                GUILayout.Label("<size=11><color=#90A4AE>状态:</color></size>", _marqueeStyle, GUILayout.Width(42));
                for (int s = 0; s < UserProfile.StatusPresets.Length; s++)
                {
                    string st = UserProfile.StatusPresets[s];
                    bool isStSel = _editingProfile.status == st;
                    if (GUILayout.Button(st, isStSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(22)))
                    {
                        _editingProfile.status = st;
                    }
                    if (s < UserProfile.StatusPresets.Length - 1) GUILayout.Space(3);
                }
                GUILayout.Space(6);
                _editingProfile.status = GUILayout.TextField(_editingProfile.status, 12, _modalInputStyle, GUILayout.Width(76), GUILayout.Height(22));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            GUILayout.Space(6);

            // ==================== 属地 & 生日 (放在个性签名上边，分别单起一行) ====================
            // 属地 (单起一行)
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=12><color=#90A4AE>属地: </color></size>", _playerBarTagStyle, GUILayout.Width(44), GUILayout.Height(20));
            string regStr = string.IsNullOrEmpty(prof.region) ? "中国湖北" : prof.region;
            GUILayout.Label($"<size=12><color=#E0F7FA><b>{regStr}</b></color></size>", _playerBarTagStyle, GUILayout.Height(20));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // 生日 (单起一行)
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=12><color=#90A4AE>生日: </color></size>", _playerBarTagStyle, GUILayout.Width(44), GUILayout.Height(20));
            string bdayStr = string.IsNullOrEmpty(prof.birthday) ? "保密" : prof.birthday;
            GUILayout.Label($"<size=12><color=#FFE082><b>{bdayStr}</b></color></size>", _playerBarTagStyle, GUILayout.Height(20));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ==================== Section 2: 个性签名 (Personality Signature) ====================
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>个性签名</b>", _modalLabelStyle);
            if (isSelf && !_isEditingBio)
            {
                if (GUILayout.Button("✏️ 编辑", _sideActionBtnStyle, GUILayout.Width(54), GUILayout.Height(20)))
                {
                    _isEditingBio = true;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (isSelf && _isEditingBio)
            {
                GUILayout.BeginHorizontal();
                _editingProfile.bio = GUILayout.TextField(_editingProfile.bio, 50, _modalInputStyle, GUILayout.Height(26));
                if (GUILayout.Button("保存", _modalPrimaryBtnStyle, GUILayout.Width(52), GUILayout.Height(26)))
                {
                    prof.bio = _editingProfile.bio;
                    prof.SaveToPrefs();
                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateProfile(_editingProfile);
                    }
                    _isEditingBio = false;
                }
                if (GUILayout.Button("取消", _modalSecondaryBtnStyle, GUILayout.Width(46), GUILayout.Height(26)))
                {
                    _editingProfile.bio = prof.bio;
                    _isEditingBio = false;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                string bio = string.IsNullOrEmpty(prof.bio)
                    ? (isSelf ? "点击右上角编辑按钮设置个性签名吧~" : "这个玩家很神秘，还没有写个性签名~")
                    : prof.bio;
                GUILayout.BeginVertical(_drawerBoxStyle);
                GUILayout.Label($"<color=#E0F7FA><i>“{bio}”</i></color>", _systemContentStyle);
                GUILayout.EndVertical();
            }

            GUILayout.Space(8);

            // ==================== Section 3: 空间社交档案 (两行两列严格纵向对齐) ====================
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>空间社交档案</b>", _modalLabelStyle);
            GUILayout.Space(8);
            GUILayout.Label("<size=11><color=#78909C>纯净社交空间成长履历与荣誉档案</color></size>", _marqueeStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            float colW = (cardW - 74f) * 0.5f;

            // Row 1 (两列对齐: 空间成就 + 鲜花人气)
            GUILayout.BeginHorizontal();
            DrawSocialGridCard("🏆 空间成就", $"{prof.achievePoints} 点", new Color(1.0f, 0.85f, 0.2f), "智慧空间探索成就积分", colW);
            GUILayout.Space(8);
            DrawSocialGridCard("🌸 鲜花人气", $"{prof.flowers} 朵", new Color(1.0f, 0.5f, 0.75f), "累计获赠空间鲜花总值", colW);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Row 2 (两列对齐: 空间好友 + 漫游常驻)
            GUILayout.BeginHorizontal();
            DrawSocialGridCard("👥 空间好友", $"{prof.friendsCount} 人", new Color(0.2f, 0.8f, 1.0f), "互相关注的挚友与旅伴", colW);
            GUILayout.Space(8);
            DrawSocialGridCard("📅 漫游常驻", $"{prof.residenceDays} 天", new Color(0.4f, 1.0f, 0.6f), "与智慧空间一同见证成长", colW);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ==================== Section 4: 精选照片 (水平排列若干照片) ====================
            bool isEditingAny = _isEditingDetails || _isEditingName || _isEditingBio || _isEditingPhotos;
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>📷 精选照片</b>", _modalLabelStyle);
            GUILayout.Space(8);
            if (isSelf && isEditingAny)
            {
                GUILayout.Label("<size=11><color=#FF8A80>编辑中：点击照片右上角 [✕] 可删除照片</color></size>", _marqueeStyle);
            }
            else
            {
                GUILayout.Label("<size=11><color=#78909C>漫游精彩瞬间与打卡合影</color></size>", _marqueeStyle);
            }
            GUILayout.FlexibleSpace();
            if (isSelf)
            {
                if (GUILayout.Button("➕ 上传照片", _sideActionBtnStyle, GUILayout.Width(86), GUILayout.Height(20)))
                {
                    OpenPhotoUploadModal();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            DrawFeaturedPhotosRow(isSelf);

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // ==================== Section 5: Bottom Action Bar ====================
            GUILayout.BeginHorizontal();
            if (isSelf)
            {
                // 自己模式：隐私设置、鲜花记录、编辑资料
                if (GUILayout.Button("🔒 隐私设置", _modalSecondaryBtnStyle, GUILayout.Height(36)))
                {
                    AddSystemMessage("已将个人信息访问权限设置为：<color=#80D8FF>全员公开</color>");
                }
                GUILayout.Space(8);
                if (GUILayout.Button("🌸 鲜花记录", _modalSecondaryBtnStyle, GUILayout.Height(36)))
                {
                    AddSystemMessage($"鲜花记录：累计收到空间好友赠送的 <color=#FF80AB>{prof.flowers}</color> 朵鲜花！");
                }
                GUILayout.FlexibleSpace();
                if (isEditingAny)
                {
                    if (GUILayout.Button("✓ 快速保存", _modalPrimaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
                    {
                        SaveAllEditingProfile(prof);
                    }
                    GUILayout.Space(8);
                    if (GUILayout.Button("✕ 退出编辑", _modalSecondaryBtnStyle, GUILayout.Width(96), GUILayout.Height(36)))
                    {
                        CancelAllEditingProfile(prof);
                    }
                }
                else
                {
                    if (GUILayout.Button("✏️ 快速编辑资料", _modalPrimaryBtnStyle, GUILayout.Width(130), GUILayout.Height(36)))
                    {
                        InitBirthdayEditFields();
                        _isEditingName = true;
                        _isEditingBio = true;
                        _isEditingDetails = true;
                        _isEditingPhotos = true;
                        _editingPhotos.Clear();
                        _editingPhotos.AddRange(_featuredPhotos);
                        _photosPendingDelete.Clear();
                        _photoToDeleteIndex = -1;
                    }
                }
            }
            else
            {
                // 他人模式：黑名单、发私聊、加好友、送鲜花
                bool isBlacklisted = _blacklistedIds.Contains(_targetProfileSessionId);
                string blText = isBlacklisted ? "解除拉黑" : "🚫 黑名单";
                if (GUILayout.Button(blText, _modalSecondaryBtnStyle, GUILayout.Width(92), GUILayout.Height(36)))
                {
                    if (isBlacklisted)
                    {
                        _blacklistedIds.Remove(_targetProfileSessionId);
                        AddSystemMessage($"已将 <color=#FFD54F>{prof.username}</color> 移出黑名单。");
                    }
                    else
                    {
                        _blacklistedIds.Add(_targetProfileSessionId);
                        AddSystemMessage($"已将 <color=#FF5252>{prof.username}</color> 加入黑名单，将屏蔽其消息。");
                    }
                }

                GUILayout.Space(8);

                if (GUILayout.Button("💬 发送私聊", _modalSecondaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
                {
                    _whisperTargetId = _targetProfileSessionId;
                    _whisperTargetName = prof.username;
                    _currentChannel = ChatChannel.Whisper;
                    CloseProfileModal();
                    _isExpanded = true;
                    _shouldScrollToBottom = true;
                }

                GUILayout.Space(8);

                if (GUILayout.Button("🤝 添加好友", _modalPrimaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
                {
                    AddFriendFromProfile(_targetProfileSessionId, prof);
                    _selectedFriendId = _targetProfileSessionId;
                    _selectedFriendName = prof.username;
                    _currentChannel = ChatChannel.Friend;
                    CloseProfileModal();
                    _isExpanded = true;
                    _shouldScrollToBottom = true;
                }

                GUILayout.Space(8);

                if (GUILayout.Button("🌸 赠送鲜花", _modalPrimaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
                {
                    prof.flowers += 1;
                    AddSystemMessage($"你向 <color=#FFD54F>{prof.username}</color> 赠送了 1 朵鲜花！🌸 （Ta 的鲜花值增加为 {prof.flowers}）");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSocialGridCard(string iconTitle, string value, Color valColor, string desc, float width)
        {
            GUILayout.BeginVertical(_badgeCardStyle, GUILayout.Width(width), GUILayout.Height(44));

            // Line 1: Title on left, Value on right
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            GUILayout.Label($"<size=11><b>{iconTitle}</b></size>", _modalLabelStyle, GUILayout.Width(80), GUILayout.Height(18));
            GUILayout.FlexibleSpace();
            string hexColor = ColorUtility.ToHtmlStringRGB(valColor);
            GUILayout.Label($"<size=12><b><color=#{hexColor}>{value}</color></b></size>", _systemContentStyle, GUILayout.Width(66), GUILayout.Height(18));
            GUILayout.Space(4);
            GUILayout.EndHorizontal();

            GUILayout.Space(1);

            // Line 2: Subtitle Description
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            GUILayout.Label($"<size=10><color=#78909C>{desc}</color></size>", _marqueeStyle, GUILayout.Height(16));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawFeaturedPhotosRow(bool isSelf)
        {
            bool isEditing = isSelf && (_isEditingName || _isEditingBio || _isEditingDetails || _isEditingPhotos);
            List<ProfilePhotoItem> photos = isSelf ? (isEditing ? _editingPhotos : _featuredPhotos) : _targetFeaturedPhotos;
            if ((photos == null || photos.Count == 0) && !_featuredPhotosInitialized)
            {
                InitFeaturedPhotos();
                photos = isSelf ? (isEditing ? _editingPhotos : _featuredPhotos) : _targetFeaturedPhotos;
            }

            if (photos == null || photos.Count == 0)
            {
                GUILayout.BeginVertical(_drawerBoxStyle, GUILayout.ExpandWidth(true), GUILayout.Height(68));
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("<size=11><color=#90A4AE>❖ 暂无精选照片，点击右上角“➕ 上传照片”分享美好瞬间 ❖</color></size>", _marqueeStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            _photoScrollPos = GUILayout.BeginScrollView(_photoScrollPos, false, false, GUILayout.Height(102));
            GUILayout.BeginHorizontal();

            int photoToDelete = -1;

            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                GUILayout.BeginVertical(_badgeCardStyle, GUILayout.Width(88), GUILayout.Height(92));

                // Photo Thumbnail (80 x 42)
                Rect thumbRect = GUILayoutUtility.GetRect(80, 42, GUILayout.Width(80), GUILayout.Height(42));
                if (photo.texture != null)
                {
                    GUI.DrawTexture(thumbRect, photo.texture);
                }
                else
                {
                    GUI.DrawTexture(thumbRect, _texMiniBarBg);
                }

                // Delete button (Self & Edit Mode ONLY!)
                if (isSelf && isEditing)
                {
                    Rect delBtnRect = new Rect(thumbRect.x + thumbRect.width - 18, thumbRect.y + 2, 16, 16);
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1.0f, 0.25f, 0.25f, 0.9f);
                    if (GUI.Button(delBtnRect, "✕", _sideActionBtnStyle))
                    {
                        photoToDelete = i;
                    }
                    GUI.backgroundColor = prevBg;
                }

                GUILayout.Space(2);
                // Title
                GUILayout.Label($"<size=10><color=#E0F7FA><b>{photo.title}</b></color></size>", _marqueeStyle, GUILayout.Width(80), GUILayout.Height(16));

                // Like count button
                if (GUILayout.Button($"❤️ {photo.likes}", _sideActionBtnStyle, GUILayout.Width(80), GUILayout.Height(18)))
                {
                    photo.likes += 1;
                    if (isSelf) SaveFeaturedPhotosMetadata();
                    AddSystemMessage($"你为照片【{photo.title}】点赞！当前获赞：{photo.likes} ❤️");
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            if (isSelf)
            {
                GUILayout.BeginVertical(_badgeCardStyle, GUILayout.Width(58), GUILayout.Height(92));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("➕\n上传", _sideActionBtnStyle, GUILayout.Width(50), GUILayout.Height(52)))
                {
                    OpenPhotoUploadModal();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (photoToDelete >= 0 && photoToDelete < _editingPhotos.Count)
            {
                var p = _editingPhotos[photoToDelete];
                _editingPhotos.RemoveAt(photoToDelete);
                if (!_photosPendingDelete.Contains(p))
                {
                    _photosPendingDelete.Add(p);
                }
                _photoToDeleteIndex = -1;
                AddSystemMessage($"已将照片【<color=#FF8A80>{p.title}</color>】移出精选，点击下方“✓ 快速保存”生效。🗑️");
            }
        }

        private void DrawPhotoUploadModal()
        {
            if (!_showPhotoUploadModal) return;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texModalOverlay);

            float modalW = 530f;
            float modalH = 390f;
            float modalX = (Screen.width - modalW) * 0.5f;
            float modalY = (Screen.height - modalH) * 0.5f;

            GUILayout.BeginArea(new Rect(modalX, modalY, modalW, modalH), _modalCardStyle);
            GUILayout.BeginVertical();

            // Header Title
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=15><b><color=#00E5FF>📷 上传精选照片 · 空间相册</color></b></size>", _modalTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _sideActionBtnStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                ClosePhotoUploadModal();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Row 1: Source tabs
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=12><color=#90A4AE>照片来源:</color></size>", _playerBarTagStyle, GUILayout.Width(66), GUILayout.Height(26));
            string[] sourceTabs = new string[] { "📸 实时拍照", "🎨 风景预设", "📁 本地导入" };
            for (int t = 0; t < sourceTabs.Length; t++)
            {
                bool isSel = (_uploadSourceTab == t);
                if (GUILayout.Button(sourceTabs[t], isSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Width(96), GUILayout.Height(26)))
                {
                    _uploadSourceTab = t;
                    if (t == 0) CaptureNewSnapshot();
                    else if (t == 1)
                    {
                        string[] presetNames = new string[] { "星辉漫游", "浮空圣岛", "大星云境", "熔金落日", "极光天穹", "樱语花海" };
                        SelectPresetWallpaper(0, presetNames[0]);
                    }
                }
                GUILayout.Space(4);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Main Content Area: Left Preview (190x165), Right Settings
            GUILayout.BeginHorizontal();

            // Left: Photo Preview
            GUILayout.BeginVertical(_badgeCardStyle, GUILayout.Width(190), GUILayout.Height(175));
            GUILayout.Label("<size=11><color=#80D8FF><b>预览效果</b></color></size>", _playerBarTagStyle);
            GUILayout.Space(2);
            Rect prevRect = GUILayoutUtility.GetRect(174, 114, GUILayout.Width(174), GUILayout.Height(114));
            if (_uploadPreviewTex != null)
            {
                GUI.DrawTexture(prevRect, _uploadPreviewTex, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.DrawTexture(prevRect, _texMiniBarBg);
            }
            GUILayout.Space(4);
            GUILayout.Label("<size=10><color=#78909C>支持 3:2 / 16:9 比例画面</color></size>", _marqueeStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Right: Options depending on source tab
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (_uploadSourceTab == 0) // 📸 实时拍照
            {
                GUILayout.Label("<size=11><color=#E0F7FA><b>📸 拍摄当前 3D 空间画面</b></color></size>", _modalLabelStyle);
                GUILayout.Label("<size=10><color=#90A4AE>以当前镜头角度捕获场景中角色与环境</color></size>", _marqueeStyle);
                GUILayout.Space(10);
                if (GUILayout.Button("📷 重新拍摄当前画面", _modalPrimaryBtnStyle, GUILayout.Height(34)))
                {
                    CaptureNewSnapshot();
                }
                GUILayout.Space(8);
                GUILayout.Label("<size=10><color=#80D8FF>❖ 提示：可在场景中漫游旋转后再打开拍照 ❖</color></size>", _marqueeStyle);
            }
            else if (_uploadSourceTab == 1) // 🎨 风景预设
            {
                GUILayout.Label("<size=11><color=#E0F7FA><b>🎨 选择空间主题风景壁纸</b></color></size>", _modalLabelStyle);
                GUILayout.Space(6);
                string[] presetNames = new string[] { "星辉漫游", "浮空圣岛", "大星云境", "熔金落日", "极光天穹", "樱语花海" };
                for (int r = 0; r < 2; r++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 3; c++)
                    {
                        int pIdx = r * 3 + c;
                        bool isSel = (_selectedPresetIndex == pIdx);
                        if (GUILayout.Button(presetNames[pIdx], isSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(28)))
                        {
                            SelectPresetWallpaper(pIdx, presetNames[pIdx]);
                        }
                        if (c < 2) GUILayout.Space(4);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                }
            }
            else if (_uploadSourceTab == 2) // 📁 本地导入
            {
                GUILayout.Label("<size=11><color=#E0F7FA><b>📁 从电脑本地选择图片</b></color></size>", _modalLabelStyle);
                GUILayout.Label("<size=10><color=#90A4AE>支持 PNG、JPG、JPEG 本地图片文件</color></size>", _marqueeStyle);
                GUILayout.Space(10);
                if (GUILayout.Button("📂 打开文件浏览器选择...", _modalPrimaryBtnStyle, GUILayout.Height(34)))
                {
                    BrowseLocalImage();
                }
                GUILayout.Space(6);
                if (!string.IsNullOrEmpty(_lastLoadedLocalFileName))
                {
                    GUILayout.Label($"<size=10><color=#00E676>已选文件: {_lastLoadedLocalFileName}</color></size>", _marqueeStyle);
                }
            }

            GUILayout.Space(8);

            // Title input
            GUILayout.Label("<size=11><color=#90A4AE>照片标题 / 描述:</color></size>", _playerBarTagStyle);
            _uploadPhotoTitle = GUILayout.TextField(_uploadPhotoTitle, 16, _modalInputStyle, GUILayout.Height(26));

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Bottom Actions: Confirm & Cancel
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕ 取消", _modalSecondaryBtnStyle, GUILayout.Width(80), GUILayout.Height(34)))
            {
                ClosePhotoUploadModal();
            }
            GUILayout.Space(10);
            if (GUILayout.Button("✓ 确认上传发布", _modalPrimaryBtnStyle, GUILayout.Width(130), GUILayout.Height(34)))
            {
                ConfirmUploadPhoto();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawCostumeLeftOverlay()
        {
            // 1. Far-left 4 Scheme buttons (under back button)
            float startY = 70f;
            float btnW = 54f;
            float btnH = 50f;
            float startX = 24f;

            string[] schemeNames = new string[] { "方案\n1", "方案\n2", "方案\n3", "方案\n4" };
            for (int i = 0; i < 4; i++)
            {
                bool isSel = (_selectedCostumeScheme == i);
                GUIStyle st = isSel ? _tabActiveStyle : _tabInactiveStyle;
                Rect sRect = new Rect(startX, startY + i * (btnH + 8f), btnW, btnH);
                if (GUI.Button(sRect, schemeNames[i], st))
                {
                    _selectedCostumeScheme = i;
                    int[] schemePresets = new int[] { 0, 4, 8, 11 };
                    _previewCostumeId = schemePresets[i];
                    if (WardrobeSceneController.Instance != null)
                    {
                        WardrobeSceneController.Instance.PreviewCostume(_previewCostumeId);
                    }
                    if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
                    {
                        var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                        if (lpc != null) lpc.ApplyAvatarVisual(_previewCostumeId);
                    }
                    AddSystemMessage($"已切换至装扮【方案 {i + 1}】！👗");
                }
            }

            // 2. Bottom-left "时装图鉴" button
            Rect bookRect = new Rect(startX, Screen.height - 76f, 84f, 44f);
            if (GUI.Button(bookRect, "👕 时装图鉴", _sideActionBtnStyle))
            {
                AddSystemMessage("【时装图鉴】已收录空间典藏 12 件限定装扮，当前全套解锁进度：100%！✨");
            }

            // 3. Feet area: "首次无消耗" hint & Gender toggle
            float tabW = 46f;
            float cardW = Mathf.Clamp(Screen.width * 0.54f, 550f, 740f);
            float cardX = Screen.width - cardW - tabW - 24f;
            float centerX = cardX * 0.5f;
            float footY = Screen.height * 0.88f;

            Rect rotHintRect = new Rect(centerX - 200f, footY - 48f, 400f, 20f);
            GUI.Label(rotHintRect, "<size=11><color=#80D8FF>❖ 按住右键漫游环绕 · 滚轮缩放视角（双击复位） ❖</color></size>", _profileStageHintStyle ?? _marqueeStyle);

            Rect hintRect = new Rect(centerX - 60f, footY - 24f, 120f, 20f);
            GUI.Label(hintRect, "<size=10><color=#80D8FF><b>首次无消耗</b></color></size>", _profileStageHintStyle ?? _marqueeStyle);

            Rect genderRect = new Rect(centerX - 24f, footY, 48f, 28f);
            string gIcon = (_editingProfile != null && _editingProfile.gender == "female") ? "♀ 女" : "♂ 男";
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (_editingProfile != null && _editingProfile.gender == "female") ? new Color(1f, 0.4f, 0.7f) : new Color(0.2f, 0.7f, 1f);
            if (GUI.Button(genderRect, gIcon, _tabActiveStyle))
            {
                if (_editingProfile != null)
                {
                    _editingProfile.gender = (_editingProfile.gender == "female") ? "male" : "female";
                    AddSystemMessage($"已切换形象预览性别：{UserProfile.GetGenderLabel(_editingProfile.gender)}");
                }
            }
            GUI.backgroundColor = prevBg;
        }

        private void InitCostumes()
        {
            if (_costumesInitialized) return;
            _costumesInitialized = true;

            _costumeItems.Clear();
            _costumeItems.Add(new CostumeItem(0, "虎龙誓印", "法阵", "古风", "龙虎金光环绕护体法阵", new Color(1.0f, 0.85f, 0.2f), new Color(1.0f, 0.45f, 0.0f)));
            _costumeItems.Add(new CostumeItem(1, "新春风旅人", "手持", "典藏", "春风拂面的祥瑞风车道具", new Color(1.0f, 0.2f, 0.25f), new Color(1.0f, 0.85f, 0.2f)));
            _costumeItems.Add(new CostumeItem(2, "单身东京狗", "背部", "浪漫", "治愈系可爱白色萌犬背饰", new Color(0.95f, 0.95f, 0.95f), new Color(0.3f, 0.75f, 1.0f)));
            _costumeItems.Add(new CostumeItem(3, "森罗灵弓", "手持", "复古", "自然森罗之力凝聚而成的灵弓", new Color(0.0f, 0.9f, 0.85f), new Color(0.1f, 0.8f, 0.4f)));
            _costumeItems.Add(new CostumeItem(4, "周年庆典卫衣", "服装", "现代", "奥拉星周年庆典限定潮牌卫衣", new Color(0.95f, 0.95f, 0.98f), new Color(1.0f, 0.35f, 0.6f)));
            _costumeItems.Add(new CostumeItem(5, "梦中花海", "背景", "浪漫", "阳光洒落在花海之上的梦幻背景", new Color(1.0f, 0.9f, 0.4f), new Color(0.2f, 0.7f, 1.0f)));
            _costumeItems.Add(new CostumeItem(6, "周年庆典", "背景", "浪漫", "漫天繁星与庆典彩带浪漫夜景", new Color(0.5f, 0.25f, 0.95f), new Color(1.0f, 0.85f, 0.2f)));
            _costumeItems.Add(new CostumeItem(7, "经典蓝灰", "服装", "现代", "奥拉星标准轻装探索服", new Color(0.25f, 0.6f, 0.95f), new Color(0.4f, 0.5f, 0.6f)));
            _costumeItems.Add(new CostumeItem(8, "烈焰战甲", "服装", "史诗", "注入烈火高温的高阶作战战甲", new Color(1.0f, 0.25f, 0.15f), new Color(1.0f, 0.65f, 0.1f)));
            _costumeItems.Add(new CostumeItem(9, "灵溪法袍", "服装", "稀有", "流淌生命灵溪的治愈系法袍", new Color(0.15f, 0.85f, 0.45f), new Color(0.0f, 0.95f, 0.85f)));
            _costumeItems.Add(new CostumeItem(10, "暗夜行者", "服装", "典藏", "隐匿于虚空之影的潜行夜行装", new Color(0.45f, 0.15f, 0.95f), new Color(0.85f, 0.2f, 0.95f)));
            _costumeItems.Add(new CostumeItem(11, "黄金神圣", "服装", "传说", "散发圣光辉芒的纯金定制战甲", new Color(1.0f, 0.85f, 0.15f), new Color(1.0f, 0.6f, 0.0f)));

            foreach (var item in _costumeItems)
            {
                item.iconTex = MakeCostumeThumbTex(item);
            }
        }

        private Texture2D MakeCostumeThumbTex(CostumeItem item)
        {
            int w = 80;
            int h = 56;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];

            Color bg1 = new Color(item.primaryColor.r * 0.22f + 0.04f, item.primaryColor.g * 0.22f + 0.07f, item.primaryColor.b * 0.22f + 0.12f, 0.95f);
            Color bg2 = new Color(item.accentColor.r * 0.35f + 0.06f, item.accentColor.g * 0.35f + 0.10f, item.accentColor.b * 0.35f + 0.18f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float ty = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float tx = (float)x / (w - 1);
                    float dist = Vector2.Distance(new Vector2(tx, ty), new Vector2(0.5f, 0.5f));
                    Color c = Color.Lerp(bg2, bg1, dist * 1.3f);

                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    {
                        c = Color.Lerp(item.accentColor, Color.white, 0.3f);
                        c.a = 0.8f;
                    }

                    if (item.category == "法阵")
                    {
                        if (Mathf.Abs(dist - 0.28f) < 0.04f || Mathf.Abs(dist - 0.15f) < 0.03f || (Mathf.Abs(tx - 0.5f) < 0.02f && ty > 0.2f && ty < 0.8f) || (Mathf.Abs(ty - 0.5f) < 0.02f && tx > 0.2f && tx < 0.8f))
                        {
                            c = Color.Lerp(c, item.primaryColor, 0.9f);
                        }
                    }
                    else if (item.category == "手持")
                    {
                        float angle = Mathf.Atan2(ty - 0.5f, tx - 0.5f);
                        if (dist < 0.35f && Mathf.Abs(Mathf.Sin(angle * 4f)) > 0.6f)
                        {
                            c = Color.Lerp(c, item.primaryColor, 0.85f);
                        }
                        if (dist < 0.08f) c = item.accentColor;
                    }
                    else if (item.category == "背部")
                    {
                        if (dist < 0.28f)
                        {
                            c = Color.Lerp(c, item.primaryColor, 0.9f);
                        }
                        if (Mathf.Abs(tx - 0.5f) < 0.1f && ty < 0.32f && ty > 0.24f)
                        {
                            c = new Color(1.0f, 0.2f, 0.3f);
                        }
                    }
                    else if (item.category == "背景")
                    {
                        if (ty < 0.35f) c = Color.Lerp(c, item.accentColor, 0.6f);
                        if (dist < 0.18f && ty > 0.4f) c = Color.Lerp(c, item.primaryColor, 0.85f);
                    }
                    else
                    {
                        if (Mathf.Abs(tx - 0.5f) < 0.26f && ty > 0.18f && ty < 0.72f)
                        {
                            c = Color.Lerp(c, item.primaryColor, 0.85f);
                            if (Mathf.Abs(tx - 0.5f) < 0.08f && ty > 0.4f && ty < 0.65f)
                            {
                                c = Color.Lerp(c, item.accentColor, 0.9f);
                            }
                        }
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawCostumeWardrobeTab(UserProfile prof, bool isSelf)
        {
            if (!_costumesInitialized) InitCostumes();

            // 1. Top Filter Row (Category buttons, Quality filter, Search input)
            GUILayout.BeginHorizontal();
            string[] categories = new string[] { "全部", "服装", "手持", "背部", "法阵", "背景" };
            for (int i = 0; i < categories.Length; i++)
            {
                bool isSel = (_costumeCategoryTab == i);
                if (GUILayout.Button(categories[i], isSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26), GUILayout.Width(50)))
                {
                    _costumeCategoryTab = i;
                }
                GUILayout.Space(2);
            }

            GUILayout.Space(6);

            string[] qualities = new string[] { "品质:全部", "品质:典藏", "品质:传说", "品质:史诗", "品质:稀有" };
            string qText = qualities[_costumeQualityFilter % qualities.Length];
            if (GUILayout.Button(qText, _sideActionBtnStyle, GUILayout.Height(26), GUILayout.Width(76)))
            {
                _costumeQualityFilter = (_costumeQualityFilter + 1) % qualities.Length;
            }

            GUILayout.Space(6);

            GUILayout.Label("<color=#90A4AE>🔍</color>", _marqueeStyle, GUILayout.Width(16));
            _costumeSearchText = GUILayout.TextField(_costumeSearchText, 12, _modalInputStyle, GUILayout.Height(24), GUILayout.Width(92));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Costume Items Grid
            List<CostumeItem> filtered = new List<CostumeItem>();
            string targetCat = _costumeCategoryTab == 0 ? "" : categories[_costumeCategoryTab];
            string[] qKeys = new string[] { "", "典藏", "传说", "史诗", "稀有" };
            string targetQ = qKeys[_costumeQualityFilter];

            foreach (var item in _costumeItems)
            {
                if (!string.IsNullOrEmpty(targetCat) && item.category != targetCat) continue;
                if (!string.IsNullOrEmpty(targetQ) && item.styleTag != targetQ) continue;
                if (!string.IsNullOrEmpty(_costumeSearchText.Trim()) && !item.name.Contains(_costumeSearchText.Trim())) continue;
                filtered.Add(item);
            }

            _costumeScrollPos = GUILayout.BeginScrollView(_costumeScrollPos, false, true, GUILayout.ExpandHeight(true));

            int columns = 3;
            for (int i = 0; i < filtered.Count; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int idx = i + c;
                    if (idx < filtered.Count)
                    {
                        var item = filtered[idx];
                        bool isWearing = (prof.costumeId == item.id);
                        bool isPreviewing = (_previewCostumeId == item.id);

                        GUIStyle cardStyle = isPreviewing ? _middleColStyle : _badgeCardStyle;
                        GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(115));

                        // Header tags
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(3);
                        string tagColor = item.styleTag switch
                        {
                            "传说" => "#FFD54F",
                            "典藏" => "#FFD700",
                            "史诗" => "#E040FB",
                            "古风" => "#FFAB40",
                            "浪漫" => "#FF4081",
                            "现代" => "#00E5FF",
                            _ => "#80D8FF"
                        };
                        GUILayout.Label($"<size=10><color={tagColor}><b>[{item.styleTag}]</b></color></size>", _marqueeStyle, GUILayout.Height(16));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("<size=10><color=#00E5FF><b>永久</b></color></size>", _marqueeStyle, GUILayout.Height(16));
                        GUILayout.Space(3);
                        GUILayout.EndHorizontal();

                        // Thumbnail image
                        Rect iconRect = GUILayoutUtility.GetRect(76, 44, GUILayout.Width(76), GUILayout.Height(44));
                        if (item.iconTex != null)
                        {
                            GUI.DrawTexture(iconRect, item.iconTex, ScaleMode.ScaleToFit);
                        }

                        GUILayout.Space(2);

                        // Bottom Name and Wear State
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        string nameColor = isWearing ? "#00E676" : (isPreviewing ? "#00E5FF" : "#E0F7FA");
                        GUILayout.Label($"<size=11><b><color={nameColor}>{item.name}</color></b></size>", _marqueeStyle, GUILayout.Height(16));
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (isWearing)
                        {
                            GUILayout.Label("<size=10><color=#00E676>● 已穿戴</color></size>", _marqueeStyle, GUILayout.Height(14));
                        }
                        else if (isPreviewing)
                        {
                            GUILayout.Label("<size=10><color=#00E5FF>● 试穿中</color></size>", _marqueeStyle, GUILayout.Height(14));
                        }
                        else
                        {
                            GUILayout.Label($"<size=10><color=#78909C>{item.category}</color></size>", _marqueeStyle, GUILayout.Height(14));
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.EndVertical();

                        // Click whole card to preview
                        if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                        {
                            _previewCostumeId = item.id;
                            if (WardrobeSceneController.Instance != null)
                            {
                                WardrobeSceneController.Instance.PreviewCostume(item.id);
                            }
                            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
                            {
                                var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                                if (lpc != null) lpc.ApplyAvatarVisual(item.id);
                            }
                            Event.current.Use();
                        }
                    }
                    else
                    {
                        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        GUILayout.EndVertical();
                    }

                    if (c < columns - 1) GUILayout.Space(6);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // 3. Bottom Actions
            GUILayout.BeginHorizontal();
            if (isSelf)
            {
                if (GUILayout.Button("💾  保存并穿戴", _modalPrimaryBtnStyle, GUILayout.Width(140), GUILayout.Height(36)))
                {
                    _editingProfile.costumeId = _previewCostumeId;
                    prof.costumeId = _previewCostumeId;
                    prof.SaveToPrefs();
                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateProfile(_editingProfile);
                    }
                    if (WardrobeSceneController.Instance != null)
                    {
                        WardrobeSceneController.Instance.SaveAndEquipCostume();
                    }
                    string selName = (_previewCostumeId >= 0 && _previewCostumeId < _costumeItems.Count) ? _costumeItems[_previewCostumeId].name : "专属时装";
                    AddSystemMessage($"已成功穿戴时装【<color=#00E5FF>{selName}</color>】并在空间全服同步！✨");
                }

                GUILayout.Space(8);

                if (GUILayout.Button("↺  恢复原装", _modalSecondaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
                {
                    _previewCostumeId = prof.costumeId;
                    if (WardrobeSceneController.Instance != null)
                    {
                        WardrobeSceneController.Instance.PreviewCostume(_previewCostumeId);
                    }
                    if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
                    {
                        var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                        if (lpc != null)
                        {
                            lpc.ApplyAvatarVisual(_previewCostumeId);
                        }
                    }
                }
            }
            else
            {
                GUILayout.Label("<color=#90A4AE>正在浏览他人的时装搭配</color>", _marqueeStyle);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("关闭", _modalSecondaryBtnStyle, GUILayout.Width(80), GUILayout.Height(36)))
            {
                CloseProfileModal();
            }

            GUILayout.EndHorizontal();
        }

        private void InitAchievementTitles()
        {
            if (_achievementsInitialized) return;
            _achievementsInitialized = true;

            _achievementTitles.Clear();
            _achievementTitles.Add(new AchievementTitleItem("天赋异禀", "传说", new Color(1.0f, 0.85f, 0.2f), "典藏限定", 200, "在奥拉星智慧空间完成全面成长挑战", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("星海漫游者", "史诗", new Color(0.88f, 0.35f, 1.0f), "空间漫游", 150, "在智慧大空间内自由探索漫游累计超过 100 公里", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("社交天花板", "史诗", new Color(0.88f, 0.35f, 1.0f), "社交达人", 150, "结交空间好友达到 8 位并进行深度社交互动", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("鲜花万人迷", "稀有", new Color(1.0f, 0.35f, 0.65f), "社交达人", 120, "累计收到空间好友赠送的 4000 朵鲜花", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("暗夜征服者", "稀有", new Color(1.0f, 0.35f, 0.35f), "副本荣耀", 120, "加入副本攻坚队并完成暗夜之城英雄副本挑战", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("造物先行者", "经典", new Color(0.0f, 0.9f, 1.0f), "空间漫游", 100, "在空间相册首次上传并发布精选打卡照片", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("元气满满", "经典", new Color(0.2f, 0.95f, 0.5f), "空间漫游", 100, "在智慧空间连续打卡漫游 30 天", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("奥拉之星", "传说", new Color(1.0f, 0.85f, 0.2f), "典藏限定", 300, "达成智慧空间年度漫游大使荣誉认证", true, "100% (已达成)"));
            _achievementTitles.Add(new AchievementTitleItem("百团战神", "史诗", new Color(1.0f, 0.65f, 0.2f), "副本荣耀", 200, "累计参与 50 场智慧空间团队协作活动", false, "32/50 (进行中)"));
            _achievementTitles.Add(new AchievementTitleItem("虚空主宰", "传说", new Color(1.0f, 0.85f, 0.2f), "典藏限定", 500, "在隐藏星域发现并激活 10 处失落的古代星石", false, "3/10 (进行中)"));
        }

        private static string FormatTitleDisplay(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            title = title.Trim();
            if (title.StartsWith("【") && title.EndsWith("】")) return title;
            return $"【{title}】";
        }

        private void EquipAchievementTitle(UserProfile prof, string title)
        {
            prof.title = title;
            _editingProfile.title = title;
            prof.SaveToPrefs();
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UpdateProfile(prof);
            }
            if (WardrobeSceneController.Instance != null)
            {
                WardrobeSceneController.Instance.EquipTitle(title);
            }
            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                if (lpc != null)
                {
                    lpc.UpdateOverheadTitle(title);
                }
            }
            AddSystemMessage($"已成功佩戴称号【<color=#FFD54F><b>{title}</b></color>】！3D 头顶专属头衔已全服生效！👑✨");
        }

        private void UnequipAchievementTitle(UserProfile prof)
        {
            prof.title = "";
            _editingProfile.title = "";
            prof.SaveToPrefs();
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UpdateProfile(prof);
            }
            if (WardrobeSceneController.Instance != null)
            {
                WardrobeSceneController.Instance.EquipTitle("");
            }
            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
            {
                var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                if (lpc != null)
                {
                    lpc.UpdateOverheadTitle("");
                }
            }
            AddSystemMessage("已卸下当前佩戴的成就称号。");
        }

        private void DrawAchievementTitlesTab(UserProfile prof, bool isSelf)
        {
            if (!_achievementsInitialized) InitAchievementTitles();

            // 1. Top Summary Banner
            GUILayout.BeginVertical(_drawerBoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b><color=#FFD54F>🏆 成就荣誉殿堂 · 空间称号</color></b></size>", _modalTitleStyle);
            GUILayout.FlexibleSpace();
            int unlockedCount = 0;
            foreach (var t in _achievementTitles) if (t.isUnlocked) unlockedCount++;
            GUILayout.Label($"<size=11><color=#90A4AE>成就总点数: </color><color=#FFD54F><b>{prof.achievePoints} 点</b></color>  <color=#90A4AE>|</color>  <color=#90A4AE>已解锁: </color><color=#00E5FF><b>{unlockedCount}/{_achievementTitles.Count}</b></color></size>", _playerBarTagStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            GUILayout.Label("<size=10><color=#78909C>探索智慧空间、结交好友、参与副本可解锁专属称号，佩戴后将在 3D 头顶名牌与全频道展示！</color></size>", _marqueeStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 2. Currently Equipped Title Card
            GUILayout.BeginVertical(_badgeCardStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=12><color=#90A4AE>当前佩戴称号:</color></size>", _playerBarTagStyle, GUILayout.Width(92), GUILayout.Height(26));

            if (!string.IsNullOrEmpty(prof.title))
            {
                GUILayout.Label($"<size=15><b><color=#FFD54F>{FormatTitleDisplay(prof.title)}</color></b></size>", _modalLabelStyle, GUILayout.Height(26));
                GUILayout.Space(8);
                GUILayout.Label("<size=10><color=#00E5FF>● 3D 头顶尊贵专属光环佩戴中</color></size>", _marqueeStyle, GUILayout.Height(26));
                GUILayout.FlexibleSpace();
                if (isSelf)
                {
                    if (GUILayout.Button("✕ 卸下称号", _sideActionBtnStyle, GUILayout.Width(86), GUILayout.Height(24)))
                    {
                        UnequipAchievementTitle(prof);
                    }
                }
            }
            else
            {
                GUILayout.Label("<size=12><color=#78909C>【暂未佩戴任何称号】</color></size>", _marqueeStyle, GUILayout.Height(26));
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 3. Category Filter Tabs & Search
            GUILayout.BeginHorizontal();
            string[] catTabs = new string[] { "全部", "空间漫游", "社交达人", "副本荣耀", "典藏限定" };
            for (int i = 0; i < catTabs.Length; i++)
            {
                bool isSel = (_titleCategoryTab == i);
                if (GUILayout.Button(catTabs[i], isSel ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(26), GUILayout.Width(72)))
                {
                    _titleCategoryTab = i;
                }
                GUILayout.Space(2);
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("<color=#90A4AE>🔍</color>", _marqueeStyle, GUILayout.Width(16));
            _titleSearchText = GUILayout.TextField(_titleSearchText, 10, _modalInputStyle, GUILayout.Height(24), GUILayout.Width(96));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 4. Scrollable Titles List
            List<AchievementTitleItem> filteredTitles = new List<AchievementTitleItem>();
            string targetCat = _titleCategoryTab == 0 ? "" : catTabs[_titleCategoryTab];
            foreach (var t in _achievementTitles)
            {
                if (!string.IsNullOrEmpty(targetCat) && t.category != targetCat) continue;
                if (!string.IsNullOrEmpty(_titleSearchText.Trim()) && !t.title.Contains(_titleSearchText.Trim())) continue;
                filteredTitles.Add(t);
            }

            _titleScrollPos = GUILayout.BeginScrollView(_titleScrollPos, false, true, GUILayout.ExpandHeight(true));

            for (int i = 0; i < filteredTitles.Count; i++)
            {
                var item = filteredTitles[i];
                bool isEquipped = (prof.title == item.title);

                GUIStyle cardStyle = isEquipped ? _middleColStyle : _badgeCardStyle;
                GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(58));

                // Line 1: Rarity tag + Title Name + Points + Action Button
                GUILayout.BeginHorizontal();
                string hexColor = ColorUtility.ToHtmlStringRGB(item.rarityColor);
                GUILayout.Label($"<size=11><b><color=#{hexColor}>[{item.rarity}]</color></b></size>", _marqueeStyle, GUILayout.Width(46), GUILayout.Height(22));
                GUILayout.Label($"<size=13><b><color=#{hexColor}>{FormatTitleDisplay(item.title)}</color></b></size>", _modalLabelStyle, GUILayout.Width(130), GUILayout.Height(22));
                GUILayout.Label($"<size=10><color=#FFE082>+{item.points} 点</color></size>", _playerBarTagStyle, GUILayout.Width(64), GUILayout.Height(22));
                GUILayout.FlexibleSpace();

                if (isEquipped)
                {
                    GUILayout.Label("<size=11><color=#00E676><b>● 已穿戴</b></color></size>", _playerBarTagStyle, GUILayout.Width(76), GUILayout.Height(22));
                }
                else if (item.isUnlocked)
                {
                    if (isSelf)
                    {
                        if (GUILayout.Button("穿戴称号", _modalPrimaryBtnStyle, GUILayout.Width(76), GUILayout.Height(22)))
                        {
                            EquipAchievementTitle(prof, item.title);
                        }
                    }
                    else
                    {
                        GUILayout.Label("<size=10><color=#00E5FF>已解锁</color></size>", _marqueeStyle, GUILayout.Width(60), GUILayout.Height(22));
                    }
                }
                else
                {
                    GUILayout.Label("<size=10><color=#78909C>🔒 未解锁</color></size>", _marqueeStyle, GUILayout.Width(68), GUILayout.Height(22));
                }
                GUILayout.EndHorizontal();

                // Line 2: Condition / Description & Progress
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<size=10><color=#90A4AE>{item.condition}</color></size>", _marqueeStyle, GUILayout.Height(16));
                GUILayout.FlexibleSpace();
                string progColor = item.isUnlocked ? "#00E676" : "#80D8FF";
                GUILayout.Label($"<size=10><color={progColor}>达成状态: {item.progress}</color></size>", _marqueeStyle, GUILayout.Height(16));
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // 5. Bottom Actions
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=10><color=#90A4AE>❖ 提示：穿戴称号后，在大空间漫游时头顶将显现专属荣誉头衔 ❖</color></size>", _marqueeStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", _modalSecondaryBtnStyle, GUILayout.Width(80), GUILayout.Height(36)))
            {
                CloseProfileModal();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawProfileStatBadge(string title, string value, string sub, Color valColor)
        {
            GUILayout.BeginVertical(_badgeCardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(66));
            GUILayout.Label($"<size=11><color=#90A4AE>{title}</color></size>", _modalSubLabelStyle);
            GUILayout.Space(1);
            string hexColor = ColorUtility.ToHtmlStringRGB(valColor);
            GUILayout.Label($"<size=16><b><color=#{hexColor}>{value}</color></b></size>", _senderNameOtherStyle);
            GUILayout.Space(1);
            GUILayout.Label($"<size=10><color=#546E7A>{sub}</color></size>", _marqueeStyle);
            GUILayout.EndVertical();
        }

        #endregion

        #region Team & Dungeon Methods

        /// <summary>
        /// 加入副本队伍，启用组队频道
        /// </summary>
        public void JoinDungeonTeam(string teamName = "暗夜之城·英雄副本 队伍", int memberCount = 4)
        {
            _isInTeam = true;
            _currentTeamName = teamName;
            _teamMemberCount = memberCount;

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.System,
                SenderId = "system",
                SenderName = "系统",
                Content = $"你已加入副本队伍【{teamName}】（{memberCount}/4人），组队频道已启用！",
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Team,
                SenderId = "teammate_lead",
                SenderName = "炎魔队长",
                AvatarIndex = 1,
                Level = 45,
                Content = "欢迎加入副本攻坚队！全员就绪打1，准备开怪！",
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            _shouldScrollToBottom = true;
        }

        /// <summary>
        /// 退出队伍，禁用组队频道
        /// </summary>
        public void LeaveDungeonTeam()
        {
            if (!_isInTeam) return;

            string oldTeam = _currentTeamName;
            _isInTeam = false;

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.System,
                SenderId = "system",
                SenderName = "系统",
                Content = $"你已退出副本队伍【{oldTeam}】，组队频道已关闭。",
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            // 退出队伍后若停留在组队频道，自动切回世界频道
            if (_currentChannel == ChatChannel.Team)
            {
                _currentChannel = ChatChannel.World;
            }

            _shouldScrollToBottom = true;
        }

        private System.Collections.IEnumerator SimulateTeamReplyRoutine(string playerMsg)
        {
            yield return new WaitForSeconds(1.2f);
            if (!_isInTeam) yield break;

            string[] teammates = new string[] { "炎魔队长", "星辉圣骑", "灵溪治疗师" };
            int[] avatars = new int[] { 1, 2, 3 };
            int[] levels = new int[] { 45, 42, 40 };
            int idx = UnityEngine.Random.Range(0, teammates.Length);

            string reply;
            if (playerMsg.Contains("1") || playerMsg.ToLower().Contains("ready"))
            {
                reply = "收到，全员就绪！准备进入 BOSS 房间！";
            }
            else if (playerMsg.Contains("开") || playerMsg.Contains("打"))
            {
                reply = "我来开怪拉仇恨，大家注意集火小怪！";
            }
            else
            {
                string[] replies = new string[]
                {
                    "收到！注意留好大招打爆发伤害！",
                    "收到！我留着控制技能打断 BOSS 吟唱！",
                    "收到，后排治疗就位，大家放心输出！",
                    "打完这趟副本可以去交周常任务啦~"
                };
                reply = replies[UnityEngine.Random.Range(0, replies.Length)];
            }

            AddBubbleMessage(new ChatBubbleItem
            {
                Channel = ChatChannel.Team,
                SenderId = "teammate_" + idx,
                SenderName = teammates[idx],
                Content = reply,
                TimeStr = DateTime.Now.ToString("HH:mm"),
                IsSelf = false,
                Level = levels[idx],
                AvatarIndex = avatars[idx],
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            _shouldScrollToBottom = true;
        }

        #endregion

        #endregion
    }
}

