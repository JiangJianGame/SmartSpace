using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmartSpace.Network;
using SmartSpace.Character;

namespace SmartSpace.UI
{
    /// <summary>
    /// 独立衣橱场景总控制器 (Wardrobe Scene Controller)
    /// 负责：
    /// 1. 独立场景与主广场场景之间的无干扰平滑过渡与数据同步
    /// 2. 独立场景的 3D 展台、摄像机特写、角色展示模型管理
    /// 3. 1:1 奥拉星风格的时装试衣间、称号佩戴与角色资料管理
    /// 4. 完美支持独立启动（Standalone 模式）与主场景无缝叠加（Additive 模式）
    /// </summary>
    public class WardrobeSceneController : MonoBehaviour
    {
        public static WardrobeSceneController Instance { get; private set; }
        public static bool IsInWardrobeScene { get; private set; } = false;

        [Header("Scene References")]
        [SerializeField] private Camera wardrobeCamera;
        [SerializeField] private WardrobeAvatarDisplay avatarDisplay;
        [SerializeField] private ShowcaseStudioStage studioStage;
        [SerializeField] private Transform characterAnchor;

        public const float WARDROBE_WORLD_Y = 1000f;
        public const float CHARACTER_OFFSET_X = 0f;

        public Camera WardrobeCamera => wardrobeCamera;

        [Header("Orbit Camera Settings (第三人称自由环绕视角)")]
        [SerializeField] private float defaultYaw = 0f;
        [SerializeField] private float defaultPitch = 12f;
        [SerializeField] private float defaultDistance = 4.8f;
        [SerializeField] private float minPitch = -8.0f;     // 仰视极限：低机位仰看角色与头顶称号
        [SerializeField] private float maxPitch = 52.0f;    // 俯视极限：高机位俯瞰地台与角色全身
        [SerializeField] private float minDistance = 2.6f;   // 滚轮放大极限：近距离特写
        [SerializeField] private float maxDistance = 6.6f;   // 滚轮缩小极限：全景远眺
        [SerializeField] private float lookAtHeight = 1.05f; // 视觉瞄准高度 (角色躯干中心)
        [SerializeField] private float mouseSensitivity = 0.35f; // 鼠标右键旋转灵敏度
        [SerializeField] private float zoomSensitivity = 1.8f;  // 滚轮缩放灵敏度
        [SerializeField] private float smoothSpeed = 12f;       // 视角平滑阻尼速度

        private float _currentYaw = 0f;
        private float _targetYaw = 0f;
        private float _currentPitch = 12f;
        private float _targetPitch = 12f;
        private float _currentDistance = 4.8f;
        private float _targetDistance = 4.8f;

        // State & Profile
        private UserProfile _workingProfile;
        private int _currentTab = 1; // 0: 资料, 1: 时装, 2: 称号
        private int _previewCostumeId = 0;
        private int _selectedCostumeScheme = 0; // 0~3 (方案 1~4)
        private int _costumeCategoryTab = 0; // 0: 全部, 1: 服装, 2: 手持, 3: 背部, 4: 法阵, 5: 背景
        private int _costumeQualityFilter = 0; // 0: 全部品质, 1: 典藏, 2: 传说, 3: 史诗, 4: 稀有
        private string _costumeSearchText = "";
        private Vector2 _costumeScrollPos = Vector2.zero;

        // Title State
        private int _titleCategoryTab = 0;
        private string _titleSearchText = "";
        private Vector2 _titleScrollPos = Vector2.zero;

        // Profile Editing State
        private bool _isEditingBio = false;
        private string _tempBio = "";
        private string _tempStatus = "";

        // Notification Toast
        private string _toastMessage = "";
        private float _toastTimer = 0f;

        // Mouse Drag Rotation
        private bool _isDraggingModel = false;
        private Vector2 _lastMousePos;

        // Costume Data
        public struct CostumeData
        {
            public int id;
            public string name;
            public string category;
            public string styleTag;
            public string desc;
            public Color primaryColor;
            public Color accentColor;
            public CostumeData(int id, string name, string category, string styleTag, string desc, Color primary, Color accent)
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
        private readonly List<CostumeData> _costumeList = new List<CostumeData>();

        // Title Data
        public struct TitleData
        {
            public string title;
            public string rarity;
            public Color rarityColor;
            public string category;
            public int points;
            public string condition;
            public bool isUnlocked;
            public TitleData(string title, string rarity, Color rarityColor, string category, int points, string condition, bool isUnlocked)
            {
                this.title = title;
                this.rarity = rarity;
                this.rarityColor = rarityColor;
                this.category = category;
                this.points = points;
                this.condition = condition;
                this.isUnlocked = isUnlocked;
            }
        }
        private readonly List<TitleData> _titleList = new List<TitleData>();

        // UI Textures & Styles
        private bool _stylesInitialized = false;
        private Texture2D _texDarkCard;
        private Texture2D _texHeaderBg;
        private Texture2D _texBtnPrimary;
        private Texture2D _texBtnSecondary;
        private Texture2D _texBtnSchemeActive;
        private Texture2D _texBtnSchemeNormal;
        private Texture2D _texCategoryTabActive;
        private Texture2D _texCategoryTabNormal;
        private Texture2D _texItemCardBg;
        private Texture2D _texItemCardSelected;

        private GUIStyle _cardBgStyle;
        private GUIStyle _headerTitleStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _primaryBtnStyle;
        private GUIStyle _secondaryBtnStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _tabNormalStyle;
        private GUIStyle _schemeBtnActiveStyle;
        private GUIStyle _schemeBtnNormalStyle;
        private GUIStyle _itemCardStyle;
        private GUIStyle _itemCardSelectedStyle;
        private GUIStyle _tagBadgeStyle;
        private GUIStyle _toastStyle;

        #region Transition & Public Static API

        /// <summary>
        /// 从主场景平滑进入独立衣橱场景
        /// </summary>
        public static void EnterWardrobe(int initialTab = 1)
        {
            if (IsInWardrobeScene) return;

            // 1. 记录并隐藏主场景摄像机与HUD
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.gameObject.SetActive(false);
            }
            else if (Camera.main != null)
            {
                Camera.main.gameObject.SetActive(false);
            }

            // 2. 通知 DemoHUD 隐藏主广场 UI 并暂停玩家输入
            DemoHUD.IsPlazaHUDHidden = true;

            // 3. 异步叠加加载独立衣橱场景
            IsInWardrobeScene = true;
            var asyncOp = SceneManager.LoadSceneAsync("WardrobeScene", LoadSceneMode.Additive);
            asyncOp.completed += (op) =>
            {
                Scene wardrobeScene = SceneManager.GetSceneByName("WardrobeScene");
                if (wardrobeScene.IsValid())
                {
                    SceneManager.SetActiveScene(wardrobeScene);
                }

                if (Instance != null)
                {
                    Instance.InitializeWithProfile(GetActiveUserProfile(), initialTab);
                }
            };
        }

        /// <summary>
        /// 从独立衣橱安全返回主广场场景
        /// </summary>
        public static void ExitWardrobe()
        {
            if (!IsInWardrobeScene && SceneManager.sceneCount <= 1)
            {
                // Standalone 运行模式下，返回 NetManager 主场景
                SceneManager.LoadScene("NetManager", LoadSceneMode.Single);
                return;
            }

            // 1. 同步保存当前资料与装扮到网络管理器
            if (Instance != null && Instance._workingProfile != null)
            {
                Instance.SaveCurrentProfileToNetwork();
            }

            // 2. 卸载衣橱独立场景
            IsInWardrobeScene = false;
            Scene plazaScene = SceneManager.GetSceneByName("NetManager");
            if (plazaScene.IsValid())
            {
                SceneManager.SetActiveScene(plazaScene);
            }

            var asyncOp = SceneManager.UnloadSceneAsync("WardrobeScene");
            asyncOp.completed += (op) =>
            {
                // 3. 重新激活主广场摄像机与HUD
                if (ThirdPersonCamera.Instance != null)
                {
                    ThirdPersonCamera.Instance.gameObject.SetActive(true);
                }
                else
                {
                    var allCams = Camera.allCameras;
                    if (allCams.Length > 0) allCams[0].gameObject.SetActive(true);
                }

                DemoHUD.IsPlazaHUDHidden = false;

                // 4. 同步更新广场主角的视觉模型与头顶称号
                if (NetworkManager.Instance != null && NetworkManager.Instance.LocalPlayerObject != null)
                {
                    var lpc = NetworkManager.Instance.LocalPlayerObject.GetComponent<LocalPlayerController>();
                    if (lpc != null && NetworkManager.Instance.LocalProfile != null)
                    {
                        lpc.ApplyAvatarVisual(NetworkManager.Instance.LocalProfile.costumeId);
                        lpc.UpdateOverheadTitle(NetworkManager.Instance.LocalProfile.title);
                    }
                }
            };
        }

        /// <summary>
        /// 在游戏启动时预先在后台叠加加载衣橱摄影棚场景，彻底消除后续打开时的任何卡顿与等待
        /// </summary>
        public static void PreloadProfileStudio()
        {
            Scene wardrobeScene = SceneManager.GetSceneByName("WardrobeScene");
            if (!wardrobeScene.isLoaded)
            {
                var asyncOp = SceneManager.LoadSceneAsync("WardrobeScene", LoadSceneMode.Additive);
                asyncOp.completed += (op) =>
                {
                    if (Instance != null)
                    {
                        // 预加载完成，默认在后台待命，关闭摄像机与展台以节省性能并避免音频监听冲突
                        Instance.SetStudioActive(false);
                    }
                };
            }
            else
            {
                if (Instance != null && !IsInWardrobeScene)
                {
                    Instance.SetStudioActive(false);
                }
            }
        }

        /// <summary>
        /// 为个人信息界面开启独立 3D 摄影棚展台场景（已预加载，零延迟瞬间激活）
        /// </summary>
        public static void OpenProfileStudio(UserProfile profile, Action onReady = null)
        {
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.gameObject.SetActive(false);
            }
            else if (Camera.main != null)
            {
                Camera.main.gameObject.SetActive(false);
            }

            IsInWardrobeScene = true;

            Scene wardrobeScene = SceneManager.GetSceneByName("WardrobeScene");
            if (!wardrobeScene.isLoaded)
            {
                // 保底防错：若尚未预加载完成则异步加载
                var asyncOp = SceneManager.LoadSceneAsync("WardrobeScene", LoadSceneMode.Additive);
                asyncOp.completed += (op) =>
                {
                    if (Instance != null)
                    {
                        Instance.SetStudioActive(true);
                        Instance.InitializeWithProfile(profile != null ? profile : GetActiveUserProfile(), 0);
                    }
                    onReady?.Invoke();
                };
            }
            else
            {
                // 已预加载：零延迟即刻激活！
                if (Instance != null)
                {
                    Instance.SetStudioActive(true);
                    Instance.InitializeWithProfile(profile != null ? profile : GetActiveUserProfile(), 0);
                }
                onReady?.Invoke();
            }
        }

        /// <summary>
        /// 关闭独立 3D 摄影棚展台场景并无缝恢复主广场摄像机与控制（保持场景常驻，无需反复卸载加载）
        /// </summary>
        public static void CloseProfileStudio(Action onCompleted = null)
        {
            IsInWardrobeScene = false;

            // 保持 WardrobeScene 常驻内存，仅关闭高空特写摄像机与摄影棚，避免下次打开产生卡顿
            if (Instance != null)
            {
                Instance.SetStudioActive(false);
            }

            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.gameObject.SetActive(true);
            }
            else
            {
                var allCams = Camera.allCameras;
                if (allCams.Length > 0) allCams[0].gameObject.SetActive(true);
            }

            onCompleted?.Invoke();
        }

        /// <summary>
        /// 控制高空摄影棚及专属摄像机的激活状态（待机时关闭以节省 GPU 与监听）
        /// </summary>
        public void SetStudioActive(bool active)
        {
            if (wardrobeCamera != null)
            {
                wardrobeCamera.gameObject.SetActive(active);
            }
            if (studioStage != null)
            {
                studioStage.gameObject.SetActive(active);
            }
            if (characterAnchor != null)
            {
                characterAnchor.gameObject.SetActive(active);
            }
            if (avatarDisplay != null && (characterAnchor == null || avatarDisplay.transform.parent != characterAnchor))
            {
                avatarDisplay.gameObject.SetActive(active);
            }
            if (active)
            {
                UpdateCameraFraming();
            }
        }

        /// <summary>
        /// 接收鼠标右键拖拽输入，执行与主广场第三人称漫游一致的 3D 自由轨道环绕旋转 (Orbit)
        /// 水平拖拽改变 Yaw (360° 环绕)，垂直拖拽改变 Pitch (俯仰角)
        /// </summary>
        public void AddOrbitInput(float deltaX, float deltaY)
        {
            _targetYaw += deltaX * mouseSensitivity;
            // deltaY 在 GUI 坐标中向下为正，向上拖拽为负：向上拖拽减少 Pitch (仰视抬头)，向下拖拽增加 Pitch (俯视低头)
            _targetPitch += deltaY * mouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
        }

        public void AddRotation(float deltaX)
        {
            AddOrbitInput(deltaX, 0f);
        }

        public void AddVerticalViewInput(float deltaY)
        {
            AddOrbitInput(0f, deltaY);
        }

        /// <summary>
        /// 双击右键一键重置视角至正前方默认机位、标准俯仰与默认缩放距离
        /// </summary>
        public void ResetView()
        {
            // 将当前 Yaw 归一化至 [-180, 180] 范围内，使得平滑归位时走最短路径
            _currentYaw = Mathf.Repeat(_currentYaw + 180f, 360f) - 180f;
            _targetYaw = defaultYaw;
            _targetPitch = defaultPitch;
            _targetDistance = defaultDistance;
            if (avatarDisplay != null) avatarDisplay.ResetRotation();
        }

        private static UserProfile GetActiveUserProfile()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.LocalProfile != null)
            {
                return NetworkManager.Instance.LocalProfile.Clone();
            }
            return UserProfile.LoadFromPrefs();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitCostumeAndTitleData();
            EnsureComponents();
        }

        private void Start()
        {
            if (DemoHUD.Instance != null || SceneManager.sceneCount > 1)
            {
                // 作为预加载后台摄影棚场景运行，默认关闭自身摄像机与展台以节省性能并避免音频监听冲突
                SetStudioActive(false);
            }
            else
            {
                // 如果是在 Editor 中直接独立启动此场景（Standalone 模式）
                IsInWardrobeScene = true;
                SetStudioActive(true);
                InitializeWithProfile(GetActiveUserProfile(), _currentTab);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupTextures();
        }

        private void Update()
        {
            if (IsInWardrobeScene && wardrobeCamera != null && wardrobeCamera.gameObject.activeInHierarchy)
            {
                UpdateCameraFraming();
            }

            HandleCameraZoom();
            HandleMouseDragRotation();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
            }
        }

        #endregion

        #region Initialization

        public void InitializeWithProfile(UserProfile profile, int initialTab)
        {
            _workingProfile = profile != null ? profile : new UserProfile();
            _previewCostumeId = _workingProfile.costumeId;
            _currentTab = initialTab;
            _tempBio = _workingProfile.bio;
            _tempStatus = _workingProfile.status;

            // Update avatar display
            if (avatarDisplay != null)
            {
                avatarDisplay.ApplyCostumeVisual(_previewCostumeId);
                avatarDisplay.ApplyTitleAndName(_workingProfile.username, _workingProfile.title);
                avatarDisplay.ResetRotation();
            }

            // Position and bind showcase stage
            if (studioStage != null && avatarDisplay != null)
            {
                studioStage.Show(avatarDisplay.transform, wardrobeCamera);
            }

            UpdateCameraFraming();

            ShowToast($"✨ 欢迎来到独立时装衣橱！当前装扮：【{GetCostumeName(_previewCostumeId)}】");
        }

        private void EnsureComponents()
        {
            if (characterAnchor == null)
            {
                var anchorGo = GameObject.Find("CharacterAnchor");
                if (anchorGo != null) characterAnchor = anchorGo.transform;
                if (characterAnchor == null)
                {
                    var go = new GameObject("CharacterAnchor");
                    go.transform.SetParent(this.transform);
                    characterAnchor = go.transform;
                }
            }
            if (characterAnchor != null)
            {
                characterAnchor.position = new Vector3(0f, WARDROBE_WORLD_Y, 0f);
            }

            if (wardrobeCamera == null)
            {
                var camGo = GameObject.Find("WardrobeCamera");
                if (camGo != null) wardrobeCamera = camGo.GetComponent<Camera>();
                if (wardrobeCamera == null) wardrobeCamera = GetComponentInChildren<Camera>();
                if (wardrobeCamera == null)
                {
                    var camObj = new GameObject("WardrobeCamera");
                    camObj.transform.SetParent(this.transform);
                    wardrobeCamera = camObj.AddComponent<Camera>();
                    wardrobeCamera.clearFlags = CameraClearFlags.SolidColor;
                    wardrobeCamera.backgroundColor = new Color(0.024f, 0.043f, 0.10f, 1.0f);
                    wardrobeCamera.depth = 10;
                    camObj.AddComponent<AudioListener>();
                }
            }
            if (wardrobeCamera != null)
            {
                wardrobeCamera.fieldOfView = 30f;
                UpdateCameraFraming();
            }

            if (avatarDisplay == null)
            {
                avatarDisplay = FindObjectOfType<WardrobeAvatarDisplay>();
                if (avatarDisplay == null)
                {
                    // Create visual avatar preview object
                    var charObj = CreateDisplayAvatarObject();
                    charObj.transform.SetParent(characterAnchor);
                    charObj.transform.localPosition = Vector3.zero;
                    charObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    avatarDisplay = charObj.AddComponent<WardrobeAvatarDisplay>();
                    avatarDisplay.InitVisualReferences();
                }
            }

            if (studioStage == null)
            {
                studioStage = FindObjectOfType<ShowcaseStudioStage>();
                if (studioStage == null)
                {
                    var stageGo = new GameObject("ShowcaseStudioStage");
                    stageGo.transform.SetParent(this.transform);
                    studioStage = stageGo.AddComponent<ShowcaseStudioStage>();
                }
            }
            if (studioStage != null && characterAnchor != null)
            {
                studioStage.transform.position = characterAnchor.position;
            }
        }

        private GameObject CreateDisplayAvatarObject()
        {
            // Create mannequin avatar matching LocalPlayer structure
            var root = new GameObject("WardrobeAvatar");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Body Capsule
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visual.transform);
            body.transform.localPosition = new Vector3(0, 1, 0);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;
            Destroy(body.GetComponent<Collider>());

            // Visor Cube
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            visor.transform.SetParent(visual.transform);
            visor.transform.localPosition = new Vector3(0, 1.45f, 0.38f);
            visor.transform.localRotation = Quaternion.identity;
            visor.transform.localScale = new Vector3(0.55f, 0.22f, 0.3f);
            Destroy(visor.GetComponent<Collider>());

            // Overhead UI Text for Title
            var ov = new GameObject("OverheadUI");
            ov.transform.SetParent(root.transform);
            ov.transform.localPosition = new Vector3(0, 2.18f, 0);
            var textMesh = ov.AddComponent<TMPro.TextMeshPro>();
            textMesh.fontSize = 1.6f;
            textMesh.alignment = TMPro.TextAlignmentOptions.Center;
            textMesh.text = "<size=75%><color=#FFD54F><b>【星际旅者】</b></color></size>\n<color=#00E5FF>展示模特</color>";
            var rt = ov.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(4f, 1f);

            return root;
        }

        #endregion

        #region Data Setup

        private void InitCostumeAndTitleData()
        {
            _costumeList.Clear();
            _costumeList.Add(new CostumeData(0, "虎龙誓印", "服装", "典藏限定", "光环环绕的神圣降临典藏神兽法袍", new Color(1.0f, 0.85f, 0.2f), new Color(1.0f, 0.45f, 0.0f)));
            _costumeList.Add(new CostumeData(1, "新春风旅人", "手持", "传说", "随风而动的喜庆新春风车手杖", new Color(1.0f, 0.2f, 0.25f), new Color(1.0f, 0.85f, 0.2f)));
            _costumeList.Add(new CostumeData(2, "单身东京狗", "背部", "传说", "呆萌可爱的极夜纯白治愈雪纳瑞背饰", new Color(0.95f, 0.95f, 0.95f), new Color(0.3f, 0.75f, 1.0f)));
            _costumeList.Add(new CostumeData(3, "森罗灵弓", "手持", "传说", "汇聚自然森林之灵的翡翠长弓", new Color(0.0f, 0.9f, 0.85f), new Color(0.1f, 0.8f, 0.4f)));
            _costumeList.Add(new CostumeData(4, "周年卫衣", "服装", "现代潮流", "十周年限定纪念潮牌粉色卫衣", new Color(0.95f, 0.95f, 0.98f), new Color(1.0f, 0.35f, 0.6f)));
            _costumeList.Add(new CostumeData(5, "梦中花海", "背景", "典藏", "漫步于繁花之上的梦幻幻境展台", new Color(1.0f, 0.9f, 0.4f), new Color(0.2f, 0.7f, 1.0f)));
            _costumeList.Add(new CostumeData(6, "周年庆典", "法阵", "史诗", "闪耀璀璨金色华彩的夜空旋转庆典法阵", new Color(0.5f, 0.25f, 0.95f), new Color(1.0f, 0.85f, 0.2f)));
            _costumeList.Add(new CostumeData(7, "经典蓝灰", "服装", "现代潮流", "奥拉星标准制式探索防护服", new Color(0.25f, 0.6f, 0.95f), new Color(0.4f, 0.5f, 0.6f)));
            _costumeList.Add(new CostumeData(8, "烈焰战甲", "服装", "史诗", "注入熔岩烈火的高阶重装战甲", new Color(1.0f, 0.25f, 0.15f), new Color(1.0f, 0.65f, 0.1f)));
            _costumeList.Add(new CostumeData(9, "灵溪法袍", "服装", "稀有", "流淌灵溪清泉之息的法术长袍", new Color(0.15f, 0.85f, 0.45f), new Color(0.0f, 0.95f, 0.85f)));
            _costumeList.Add(new CostumeData(10, "暗夜行者", "服装", "史诗", "游弋于暗影之隙的潜行夜战套装", new Color(0.45f, 0.15f, 0.95f), new Color(0.85f, 0.2f, 0.95f)));
            _costumeList.Add(new CostumeData(11, "黄金神圣", "服装", "传说", "散发纯粹圣洁光芒的炽阳重甲", new Color(1.0f, 0.85f, 0.15f), new Color(1.0f, 0.6f, 0.0f)));

            _titleList.Clear();
            _titleList.Add(new TitleData("星际旅者", "典藏限定", new Color(1.0f, 0.84f, 0.0f), "空间漫游", 500, "登录智慧空间满 30 天", true));
            _titleList.Add(new TitleData("天赋异禀", "传说", new Color(0.9f, 0.4f, 1.0f), "空间漫游", 350, "首次达成全图探索 100%", true));
            _titleList.Add(new TitleData("社交达人", "史诗", new Color(0.2f, 0.8f, 1.0f), "社交达人", 200, "添加好友数量超过 10 人", true));
            _titleList.Add(new TitleData("深渊征服者", "传说", new Color(0.9f, 0.4f, 1.0f), "副本荣耀", 400, "通关英雄副本第 5 层", true));
            _titleList.Add(new TitleData("十周年老兵", "典藏限定", new Color(1.0f, 0.84f, 0.0f), "典藏限定", 800, "参与十周年庆典并获得限定套装", true));
            _titleList.Add(new TitleData("潮玩收藏家", "稀有", new Color(0.2f, 0.9f, 0.4f), "空间漫游", 150, "解锁 6 套以上时装装扮", true));
            _titleList.Add(new TitleData("幻梦之声", "史诗", new Color(0.2f, 0.8f, 1.0f), "社交达人", 250, "发送世界大喇叭 5 次", true));
            _titleList.Add(new TitleData("暗夜终结者", "传说", new Color(0.9f, 0.4f, 1.0f), "副本荣耀", 450, "单人击败暗夜终极首领", false));
        }

        private string GetCostumeName(int id)
        {
            if (id >= 0 && id < _costumeList.Count) return _costumeList[id].name;
            return "专署时装";
        }

        #endregion

        #region Camera Zoom, Framing & Mouse Drag

        /// <summary>
        /// 动态计算并更新高空 3D 自由环绕摄像机机位：
        /// 1. 采用与主场景 ThirdPersonCamera 一致的球坐标轨道环绕 (Yaw 360° + Pitch 俯仰)
        /// 2. 结合视锥横向动态偏移 (shiftX)，确保无论如何旋转与缩放，角色视觉中心严格锁定在左侧空白区域正中央 (X = cardX * 0.5f)
        /// 3. 支持鼠标滚轮平滑无级缩放
        /// </summary>
        public void UpdateCameraFraming()
        {
            if (wardrobeCamera == null) return;

            // 1. 平滑阻尼插值当前环绕与缩放参数，提供与 ThirdPersonCamera 一致的丝滑漫游体验
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, Time.deltaTime * smoothSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * smoothSpeed);
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * smoothSpeed);

            // 2. 计算右侧资料卡片的起始 X 坐标与左侧空白区中点（与 DemoHUD 保持完全一致）
            float tabW = 46f;
            float cardW = Mathf.Clamp(Screen.width * 0.54f, 550f, 740f);
            float cardX = Screen.width - cardW - tabW - 24f;
            float targetScreenX = cardX * 0.5f;
            float targetViewportX = targetScreenX / (float)Screen.width;

            // 3. 3D 空间中的注视目标点 (角色模型胸口中心)
            Vector3 targetCenter = characterAnchor != null
                ? characterAnchor.position + Vector3.up * lookAtHeight
                : new Vector3(0f, WARDROBE_WORLD_Y + lookAtHeight, 0f);

            // 4. 环绕姿态四元数与摄像机局部坐标轴
            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 camForward = rotation * Vector3.forward;
            Vector3 camRight = rotation * Vector3.right;

            // 5. 基于摄像机 FOV、长宽比 aspect 以及当前环绕距离计算视锥半宽高
            float halfHeight = _currentDistance * Mathf.Tan(wardrobeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * wardrobeCamera.aspect;

            // 6. 沿摄像机本地右向向量 (camRight) 施加精确平移，使得 targetCenter 在视口中的 X 坐标严格锁定于 targetViewportX
            // targetViewportX = 0.5f - shiftX / (2f * halfWidth) => shiftX = (0.5f - targetViewportX) * 2f * halfWidth
            float shiftX = (0.5f - targetViewportX) * 2f * halfWidth;

            // 7. 计算摄像机最终世界坐标与朝向
            Vector3 camPos = targetCenter - camForward * _currentDistance + camRight * shiftX;

            // 展台防穿地保护：保证摄像机不会穿透高空展台地表
            if (camPos.y < WARDROBE_WORLD_Y + 0.35f)
            {
                camPos.y = WARDROBE_WORLD_Y + 0.35f;
            }

            wardrobeCamera.transform.position = camPos;
            wardrobeCamera.transform.rotation = rotation;
        }

        private void HandleCameraZoom()
        {
            float tabW = 46f;
            float cardW = Mathf.Clamp(Screen.width * 0.54f, 550f, 740f);
            float cardX = Screen.width - cardW - tabW - 24f;

            if (Input.mousePosition.x > cardX) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * zoomSensitivity * 2.0f, minDistance, maxDistance);
            }
        }

        private void HandleMouseDragRotation()
        {
            // 当 DemoHUD 存在时，由 DemoHUD 统一处理 UI 区域与鼠标右键拖拽交互，避免重复计算
            if (DemoHUD.Instance != null) return;

            // Standalone 模式下的鼠标右键环绕旋转与视角调节
            float tabW = 46f;
            float cardW = Mathf.Clamp(Screen.width * 0.54f, 550f, 740f);
            float cardX = Screen.width - cardW - tabW - 24f;
            Vector2 mousePos = Input.mousePosition;
            bool isOverStage = mousePos.x < cardX && mousePos.y > 60f && mousePos.y < Screen.height - 60f;

            if (Input.GetMouseButtonDown(1) && isOverStage)
            {
                _isDraggingModel = true;
                _lastMousePos = mousePos;
            }
            else if (Input.GetMouseButtonUp(1) || !Input.GetMouseButton(1))
            {
                _isDraggingModel = false;
            }

            if (_isDraggingModel && Input.GetMouseButton(1))
            {
                float deltaX = mousePos.x - _lastMousePos.x;
                float deltaY = -(mousePos.y - _lastMousePos.y); // Input coords Y is bottom-up, invert to match GUI coords
                AddOrbitInput(deltaX, deltaY);
                _lastMousePos = mousePos;
            }
        }

        #endregion

        #region Actions

        public void PreviewCostume(int costumeId)
        {
            _previewCostumeId = costumeId;
            if (avatarDisplay != null)
            {
                avatarDisplay.ApplyCostumeVisual(_previewCostumeId);
            }
        }

        public void SaveAndEquipCostume()
        {
            if (_workingProfile == null) return;

            _workingProfile.costumeId = _previewCostumeId;
            _workingProfile.SaveToPrefs();

            SaveCurrentProfileToNetwork();

            if (avatarDisplay != null)
            {
                avatarDisplay.ApplyCostumeVisual(_previewCostumeId);
            }

            ShowToast($"🎉 成功保存并穿戴时装：【{GetCostumeName(_previewCostumeId)}】！");
        }

        public void ResetToCurrentCostume()
        {
            if (_workingProfile == null) return;
            _previewCostumeId = _workingProfile.costumeId;
            if (avatarDisplay != null)
            {
                avatarDisplay.ApplyCostumeVisual(_previewCostumeId);
            }
            ShowToast("🔄 已恢复为您当前佩戴的原装扮！");
        }

        public void EquipTitle(string titleName)
        {
            if (_workingProfile == null) return;
            _workingProfile.title = titleName;
            _workingProfile.SaveToPrefs();

            SaveCurrentProfileToNetwork();

            if (avatarDisplay != null)
            {
                avatarDisplay.ApplyTitleAndName(_workingProfile.username, titleName);
            }

            ShowToast($"🏆 已成功佩戴称号：【{titleName}】！");
        }

        private void SaveCurrentProfileToNetwork()
        {
            if (_workingProfile == null) return;

            if (NetworkManager.Instance != null)
            {
                if (NetworkManager.Instance.LocalProfile != null)
                {
                    NetworkManager.Instance.LocalProfile.costumeId = _workingProfile.costumeId;
                    NetworkManager.Instance.LocalProfile.title = _workingProfile.title;
                    NetworkManager.Instance.LocalProfile.bio = _workingProfile.bio;
                    NetworkManager.Instance.LocalProfile.status = _workingProfile.status;
                }
                NetworkManager.Instance.UpdateProfile(_workingProfile);
            }
        }

        private void ShowToast(string msg)
        {
            _toastMessage = msg;
            _toastTimer = 3.5f;
        }

        #endregion

        #region GUI Rendering (1:1 奥拉星时装衣橱)

        private void InitGUIStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _texDarkCard = MakeTex(2, 2, new Color(0.035f, 0.055f, 0.11f, 0.94f));
            _texHeaderBg = MakeTex(2, 2, new Color(0.06f, 0.10f, 0.20f, 0.96f));
            _texBtnPrimary = MakeTex(2, 2, new Color(0.0f, 0.85f, 0.75f, 0.95f));
            _texBtnSecondary = MakeTex(2, 2, new Color(0.12f, 0.18f, 0.32f, 0.90f));
            _texBtnSchemeActive = MakeTex(2, 2, new Color(0.15f, 0.70f, 0.95f, 0.95f));
            _texBtnSchemeNormal = MakeTex(2, 2, new Color(0.06f, 0.10f, 0.18f, 0.75f));
            _texCategoryTabActive = MakeTex(2, 2, new Color(0.0f, 0.75f, 0.95f, 0.90f));
            _texCategoryTabNormal = MakeTex(2, 2, new Color(0.08f, 0.12f, 0.22f, 0.80f));
            _texItemCardBg = MakeTex(2, 2, new Color(0.06f, 0.09f, 0.17f, 0.85f));
            _texItemCardSelected = MakeTex(2, 2, new Color(0.12f, 0.24f, 0.45f, 0.95f));

            _cardBgStyle = new GUIStyle
            {
                normal = { background = _texDarkCard },
                padding = new RectOffset(16, 16, 16, 16)
            };

            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.75f, 0.9f) }
            };

            _primaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texBtnPrimary, textColor = Color.black },
                hover = { background = _texBtnPrimary, textColor = Color.black }
            };

            _secondaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texBtnSecondary, textColor = Color.white }
            };

            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texCategoryTabActive, textColor = Color.black }
            };

            _tabNormalStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texCategoryTabNormal, textColor = new Color(0.7f, 0.85f, 1.0f) }
            };

            _schemeBtnActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texBtnSchemeActive, textColor = Color.black }
            };

            _schemeBtnNormalStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texBtnSchemeNormal, textColor = new Color(0.7f, 0.8f, 0.95f) }
            };

            _itemCardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texItemCardBg }
            };

            _itemCardSelectedStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texItemCardSelected }
            };

            _tagBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _toastStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texHeaderBg, textColor = new Color(1f, 0.92f, 0.4f) }
            };
        }

        private void OnGUI()
        {
            // 当 DemoHUD 存在时，由 DemoHUD 统一绘制完整的个人信息主界面（信息/时装/称号）
            // 独立衣橱场景专注于提供零干扰的 3D 高画质展台背景与模特模型
            if (DemoHUD.Instance != null) return;

            InitGUIStyles();

            // 1. Top Universal Navigation Bar (返回广场 + 方案切换 + 标签切换)
            DrawTopNavigationBar();

            // 2. Left 3D Stage Hint / Controls (旋转提示、特写提示)
            DrawStageControlsHint();

            // 3. Right Functional Panel (时装衣橱 / 成就称号 / 个人资料)
            DrawRightPanel();

            // 4. Toast Notification
            if (_toastTimer > 0f && !string.IsNullOrEmpty(_toastMessage))
            {
                float tw = 480f;
                float th = 44f;
                Rect tr = new Rect((Screen.width - tw) * 0.5f, 75f, tw, th);
                GUI.Box(tr, _toastMessage, _toastStyle);
            }
        }

        private void DrawTopNavigationBar()
        {
            float barH = 54f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), _texHeaderBg);

            // Left: 返回广场 Button
            if (GUI.Button(new Rect(16, 9, 130, 36), "◀  返回广场", _primaryBtnStyle))
            {
                ExitWardrobe();
            }

            // Left Center: 场景指示器
            GUI.Label(new Rect(160, 10, 180, 34), "<color=#00E5FF><b>【独立时装衣橱】</b></color>\n<size=11><color=#90CAF9>零干扰 3D 专属试衣间</color></size>", _subHeaderStyle);

            // Center: 试衣方案选择 (方案 1 ~ 4)
            float schemeStartX = Screen.width * 0.32f;
            GUI.Label(new Rect(schemeStartX - 70f, 16, 65, 24), "<color=#E0E0E0>预设方案:</color>", _subHeaderStyle);
            int[] schemeDefaults = new int[] { 0, 4, 8, 11 };
            for (int i = 0; i < 4; i++)
            {
                bool isSel = (_selectedCostumeScheme == i);
                Rect sRect = new Rect(schemeStartX + i * 80f, 10, 74, 34);
                if (GUI.Button(sRect, $"方案 {i + 1}", isSel ? _schemeBtnActiveStyle : _schemeBtnNormalStyle))
                {
                    _selectedCostumeScheme = i;
                    _previewCostumeId = schemeDefaults[i];
                    if (avatarDisplay != null)
                    {
                        avatarDisplay.ApplyCostumeVisual(_previewCostumeId);
                    }
                    ShowToast($"✨ 已切换至试衣方案 {i + 1}（{GetCostumeName(_previewCostumeId)}）");
                }
            }

            // Right: 导航标签切换 [👗 时装衣橱] [🏆 成就称号] [📋 个人资料]
            float tabW = 120f;
            float tabStartX = Screen.width - (tabW * 3 + 24f);
            string[] tabs = new string[] { "📋 个人资料", "👗 时装衣橱", "🏆 成就称号" };
            int[] tabIndices = new int[] { 0, 1, 2 };

            for (int i = 0; i < 3; i++)
            {
                int targetTab = tabIndices[i];
                bool isTabActive = (_currentTab == targetTab);
                Rect tRect = new Rect(tabStartX + i * (tabW + 6f), 10, tabW, 34);
                if (GUI.Button(tRect, tabs[i], isTabActive ? _tabActiveStyle : _tabNormalStyle))
                {
                    _currentTab = targetTab;
                }
            }
        }

        private void DrawStageControlsHint()
        {
            // Bottom-Left Stage View Hint
            float hintW = 380f;
            float hintH = 34f;
            Rect hr = new Rect(16, Screen.height - hintH - 16, hintW, hintH);
            GUI.Box(hr, "", _secondaryBtnStyle);
            GUI.Label(hr, "🖱️ <b>按住鼠标右键横向拖拽</b> 360° 旋转试衣 | <b>滚轮</b> 调节特写", _subHeaderStyle);
        }

        private void DrawRightPanel()
        {
            float panelWidth = Mathf.Clamp(Screen.width * 0.44f, 520f, 660f);
            float panelHeight = Screen.height - 70f;
            float panelX = Screen.width - panelWidth - 16f;
            float panelY = 60f;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), _cardBgStyle);

            switch (_currentTab)
            {
                case 1:
                    DrawCostumeTab(panelWidth, panelHeight);
                    break;
                case 2:
                    DrawTitlesTab(panelWidth, panelHeight);
                    break;
                case 0:
                    DrawProfileInfoTab(panelWidth, panelHeight);
                    break;
            }

            GUILayout.EndArea();
        }

        #endregion

        #region Tab: 👗 时装衣橱

        private void DrawCostumeTab(float panelWidth, float panelHeight)
        {
            // 1. Header Row: Title + Status
            GUILayout.BeginHorizontal();
            GUILayout.Label("👗 <b>时装试衣间</b> <color=#00E5FF>· 3D 实时换装</color>", _headerTitleStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            string currName = GetCostumeName(_workingProfile != null ? _workingProfile.costumeId : 0);
            GUILayout.Label($"<color=#B0BEC5>当前穿着：</color><color=#FFD54F><b>{currName}</b></color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Category Filter Tabs
            string[] categories = new string[] { "全部", "服装", "手持", "背部", "法阵", "背景" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < categories.Length; i++)
            {
                bool isCatActive = (_costumeCategoryTab == i);
                if (GUILayout.Button(categories[i], isCatActive ? _schemeBtnActiveStyle : _schemeBtnNormalStyle, GUILayout.Height(28)))
                {
                    _costumeCategoryTab = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 3. Search & Quality Filter Row
            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 搜索:", GUILayout.Width(46));
            _costumeSearchText = GUILayout.TextField(_costumeSearchText, GUILayout.Width(130), GUILayout.Height(24));
            if (!string.IsNullOrEmpty(_costumeSearchText) && GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(24)))
            {
                _costumeSearchText = "";
            }

            GUILayout.Space(10);
            GUILayout.Label("品质:", GUILayout.Width(36));
            string[] qualities = new string[] { "全部", "典藏", "传说", "史诗", "稀有" };
            for (int q = 0; q < qualities.Length; q++)
            {
                bool isQ = (_costumeQualityFilter == q);
                if (GUILayout.Button(qualities[q], isQ ? _schemeBtnActiveStyle : _schemeBtnNormalStyle, GUILayout.Height(24)))
                {
                    _costumeQualityFilter = q;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 4. Costume Card Grid (Scrollable)
            List<CostumeData> filtered = GetFilteredCostumes();
            float scrollHeight = panelHeight - 165f;
            _costumeScrollPos = GUILayout.BeginScrollView(_costumeScrollPos, GUILayout.Height(scrollHeight));

            int columns = 3;
            int total = filtered.Count;
            for (int row = 0; row < Mathf.CeilToInt((float)total / columns); row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;
                    if (idx < total)
                    {
                        var item = filtered[idx];
                        bool isEquipped = (_workingProfile != null && _workingProfile.costumeId == item.id);
                        bool isPreviewing = (_previewCostumeId == item.id);

                        DrawCostumeCard(item, isEquipped, isPreviewing);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // 5. Bottom Action Controls
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📖 时装图鉴", _secondaryBtnStyle, GUILayout.Width(110), GUILayout.Height(36)))
            {
                ShowToast("📖 奥拉星时装图鉴已全套集齐（12/12 套），获得【潮玩收藏家】专属特权！");
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔄 恢复原装", _secondaryBtnStyle, GUILayout.Width(100), GUILayout.Height(36)))
            {
                ResetToCurrentCostume();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("👗 保存装扮并同步", _primaryBtnStyle, GUILayout.Width(160), GUILayout.Height(36)))
            {
                SaveAndEquipCostume();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCostumeCard(CostumeData item, bool isEquipped, bool isPreviewing)
        {
            GUIStyle cardBoxStyle = isPreviewing ? _itemCardSelectedStyle : _itemCardStyle;
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(152), GUILayout.Height(105));

            // Row 1: Swatch Color Badge + Quality Tag
            GUILayout.BeginHorizontal();
            // Color Swatch Rect
            Rect swatchRect = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22), GUILayout.Height(22));
            DrawColorSwatch(swatchRect, item.primaryColor, item.accentColor);

            GUILayout.Space(4);
            string rColorHex = GetRarityColorHex(item.styleTag);
            GUILayout.Label($"<color={rColorHex}><b>{item.styleTag}</b></color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            // Row 2: Name
            GUILayout.Label($"<b>{item.name}</b>", _headerTitleStyle, GUILayout.Height(24));

            // Row 3: Status Badge
            if (isEquipped)
            {
                GUILayout.Label("<color=#00E676>● 已穿戴</color>", _subHeaderStyle);
            }
            else if (isPreviewing)
            {
                GUILayout.Label("<color=#00E5FF>● 试穿预览中</color>", _subHeaderStyle);
            }
            else
            {
                GUILayout.Label("<color=#78909C>点击试穿</color>", _subHeaderStyle);
            }

            GUILayout.EndVertical();

            // Click Card Event
            if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                _previewCostumeId = item.id;
                if (avatarDisplay != null)
                {
                    avatarDisplay.ApplyCostumeVisual(item.id);
                }
                Event.current.Use();
            }
        }

        private List<CostumeData> GetFilteredCostumes()
        {
            List<CostumeData> list = new List<CostumeData>();
            string[] categories = new string[] { "全部", "服装", "手持", "背部", "法阵", "背景" };
            string[] qualities = new string[] { "全部", "典藏", "传说", "史诗", "稀有" };

            string filterCat = _costumeCategoryTab == 0 ? null : categories[_costumeCategoryTab];
            string filterQ = _costumeQualityFilter == 0 ? null : qualities[_costumeQualityFilter];

            for (int i = 0; i < _costumeList.Count; i++)
            {
                var item = _costumeList[i];
                if (filterCat != null && item.category != filterCat) continue;
                if (filterQ != null && !item.styleTag.Contains(filterQ)) continue;
                if (!string.IsNullOrEmpty(_costumeSearchText) && !item.name.Contains(_costumeSearchText)) continue;
                list.Add(item);
            }
            return list;
        }

        private string GetRarityColorHex(string tag)
        {
            if (tag.Contains("典藏")) return "#FFD54F";
            if (tag.Contains("传说")) return "#E040FB";
            if (tag.Contains("史诗")) return "#00E5FF";
            if (tag.Contains("稀有")) return "#69F0AE";
            return "#B0BEC5";
        }

        private void DrawColorSwatch(Rect r, Color c1, Color c2)
        {
            // Left half c1, right half c2
            Rect r1 = new Rect(r.x, r.y, r.width * 0.5f, r.height);
            Rect r2 = new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height);

            Texture2D t1 = MakeTex(1, 1, c1);
            Texture2D t2 = MakeTex(1, 1, c2);
            GUI.DrawTexture(r1, t1);
            GUI.DrawTexture(r2, t2);
            Destroy(t1);
            Destroy(t2);
        }

        #endregion

        #region Tab: 🏆 成就称号

        private static string FormatTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            title = title.Trim();
            if (title.StartsWith("【") && title.EndsWith("】")) return title;
            return $"【{title}】";
        }

        private void DrawTitlesTab(float panelWidth, float panelHeight)
        {
            // 1. Header Row
            GUILayout.BeginHorizontal();
            GUILayout.Label("🏆 <b>成就荣誉殿堂</b> <color=#FFD54F>· 空间称号佩戴</color>", _headerTitleStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            string currTitle = _workingProfile != null ? _workingProfile.title : "暂无称号";
            GUILayout.Label($"<color=#B0BEC5>当前佩戴：</color><color=#FFD54F><b>{FormatTitle(currTitle)}</b></color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Category Filter Tabs
            string[] catNames = new string[] { "全部", "空间漫游", "社交达人", "副本荣耀", "典藏限定" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < catNames.Length; i++)
            {
                bool isCatActive = (_titleCategoryTab == i);
                if (GUILayout.Button(catNames[i], isCatActive ? _schemeBtnActiveStyle : _schemeBtnNormalStyle, GUILayout.Height(28)))
                {
                    _titleCategoryTab = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 3. Search Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 搜索称号:", GUILayout.Width(76));
            _titleSearchText = GUILayout.TextField(_titleSearchText, GUILayout.Width(160), GUILayout.Height(24));
            if (!string.IsNullOrEmpty(_titleSearchText) && GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(24)))
            {
                _titleSearchText = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 4. Scrollable Title List
            float scrollHeight = panelHeight - 120f;
            _titleScrollPos = GUILayout.BeginScrollView(_titleScrollPos, GUILayout.Height(scrollHeight));

            for (int i = 0; i < _titleList.Count; i++)
            {
                var title = _titleList[i];
                if (_titleCategoryTab > 0 && title.category != catNames[_titleCategoryTab]) continue;
                if (!string.IsNullOrEmpty(_titleSearchText) && !title.title.Contains(_titleSearchText)) continue;

                bool isEquipped = (_workingProfile != null && _workingProfile.title == title.title);

                DrawTitleRow(title, isEquipped);
                GUILayout.Space(6);
            }

            GUILayout.EndScrollView();
        }

        private void DrawTitleRow(TitleData title, bool isEquipped)
        {
            GUIStyle boxStyle = isEquipped ? _itemCardSelectedStyle : _itemCardStyle;
            GUILayout.BeginHorizontal(boxStyle, GUILayout.Height(52));

            // Left: Title Badge & Info
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            string rarityHex = ColorUtility.ToHtmlStringRGB(title.rarityColor);
            GUILayout.Label($"<color=#{rarityHex}><b>{FormatTitle(title.title)}</b></color> <color=#90CAF9>[{title.rarity}]</color>", _headerTitleStyle, GUILayout.Height(22));
            GUILayout.Label($"<color=#FFD54F>成就点 +{title.points}</color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color=#78909C>获得条件：{title.condition}</color>", _subHeaderStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Right: Equip / Status Button
            if (isEquipped)
            {
                GUILayout.Box("<color=#00E676><b>已佩戴</b></color>", _secondaryBtnStyle, GUILayout.Width(90), GUILayout.Height(36));
            }
            else if (title.isUnlocked)
            {
                if (GUILayout.Button("佩戴此称号", _primaryBtnStyle, GUILayout.Width(95), GUILayout.Height(36)))
                {
                    EquipTitle(title.title);
                }
            }
            else
            {
                GUILayout.Box("<color=#546E7A>未解锁</color>", _secondaryBtnStyle, GUILayout.Width(90), GUILayout.Height(36));
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Tab: 📋 个人资料

        private void DrawProfileInfoTab(float panelWidth, float panelHeight)
        {
            if (_workingProfile == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("📋 <b>个人空间名片</b>", _headerTitleStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"UID: <color=#00E5FF>{_workingProfile.uid}</color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Stats row (鲜花、成就点、常驻天数)
            GUILayout.BeginHorizontal(_itemCardStyle);
            GUILayout.Label($"🌹 鲜花人气: <color=#FF80AB><b>{_workingProfile.flowers}</b></color>", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"🏆 成就荣誉点: <color=#FFD54F><b>{_workingProfile.achievePoints}</b></color>", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"📅 漫游天数: <color=#00E5FF><b>{_workingProfile.residenceDays}</b> 天</color>", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Nickname & Gender & Birthday
            GUILayout.BeginVertical(_itemCardStyle);
            GUILayout.Label($"<b>昵称：</b> <color=#00E5FF>{_workingProfile.username}</color>", _subHeaderStyle);
            GUILayout.Label($"<b>性别：</b> {(_workingProfile.gender == "male" ? "♂ 男" : "♀ 女")}  |  <b>年龄：</b> {_workingProfile.age} 岁", _subHeaderStyle);
            GUILayout.Label($"<b>属地：</b> {_workingProfile.region}  |  <b>生日：</b> {_workingProfile.birthday}", _subHeaderStyle);
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Bio Editor
            GUILayout.BeginVertical(_itemCardStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>个性签名：</b>", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            if (!_isEditingBio)
            {
                if (GUILayout.Button("✏️ 编辑", _secondaryBtnStyle, GUILayout.Width(64), GUILayout.Height(24)))
                {
                    _isEditingBio = true;
                }
            }
            else
            {
                if (GUILayout.Button("💾 保存", _primaryBtnStyle, GUILayout.Width(64), GUILayout.Height(24)))
                {
                    _workingProfile.bio = _tempBio;
                    _workingProfile.SaveToPrefs();
                    SaveCurrentProfileToNetwork();
                    _isEditingBio = false;
                    ShowToast("✨ 个性签名已更新并保存！");
                }
            }
            GUILayout.EndHorizontal();

            if (_isEditingBio)
            {
                _tempBio = GUILayout.TextArea(_tempBio, GUILayout.Height(50));
            }
            else
            {
                GUILayout.Label($"<color=#B0BEC5><i>“{_workingProfile.bio}”</i></color>", _subHeaderStyle);
            }
            GUILayout.EndVertical();

            GUILayout.Space(16);

            // Quick Actions
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👗 立即前往时装衣橱换装", _primaryBtnStyle, GUILayout.Height(40)))
            {
                _currentTab = 1;
            }
            if (GUILayout.Button("🏆 前往称号荣誉殿堂", _secondaryBtnStyle, GUILayout.Height(40)))
            {
                _currentTab = 2;
            }
            GUILayout.EndHorizontal();
        }

        #endregion

        #region Helpers

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void CleanupTextures()
        {
            if (_texDarkCard != null) Destroy(_texDarkCard);
            if (_texHeaderBg != null) Destroy(_texHeaderBg);
            if (_texBtnPrimary != null) Destroy(_texBtnPrimary);
            if (_texBtnSecondary != null) Destroy(_texBtnSecondary);
            if (_texBtnSchemeActive != null) Destroy(_texBtnSchemeActive);
            if (_texBtnSchemeNormal != null) Destroy(_texBtnSchemeNormal);
            if (_texCategoryTabActive != null) Destroy(_texCategoryTabActive);
            if (_texCategoryTabNormal != null) Destroy(_texCategoryTabNormal);
            if (_texItemCardBg != null) Destroy(_texItemCardBg);
            if (_texItemCardSelected != null) Destroy(_texItemCardSelected);
        }

        #endregion
    }
}
