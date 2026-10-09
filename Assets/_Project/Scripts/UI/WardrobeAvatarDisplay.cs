using System;
using UnityEngine;
using TMPro;

namespace SmartSpace.UI
{
    /// <summary>
    /// 独立衣橱场景专属角色展示模型 (Wardrobe Avatar Display Controller)
    /// 负责：
    /// 1. 3D 角色材质与时装配色实时换装渲染
    /// 2. 角色头顶专属成就称号与昵称发光展示
    /// 3. 360° 平滑阻尼鼠标横向拖拽旋转与待机律动
    /// </summary>
    public class WardrobeAvatarDisplay : MonoBehaviour
    {
        [Header("Model References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer visorRenderer;
        [SerializeField] private TMP_Text overheadText;
        [SerializeField] private Transform overheadRoot;

        [Header("Rotation & Animation")]
        [SerializeField] private float rotationSpeed = 240f;
        [SerializeField] private float rotationDamping = 5f;
        private float _currentYaw = 180f; // 初始正对摄像机 (面向 -Z)
        private float _targetYaw = 180f;
        private float _angularVelocity = 0f;
        private Vector3 _initialLocalPos;

        // 12 套时装主色调定义 (1:1 匹配 DemoHUD 与 LocalPlayerController)
        public static readonly Color[] CostumePalette = new Color[]
        {
            new Color(1.0f, 0.85f, 0.20f), // 0: 虎龙誓印 (金黄神圣)
            new Color(1.0f, 0.20f, 0.25f), // 1: 新春风旅人 (绯红庆典)
            new Color(0.95f, 0.95f, 0.98f),// 2: 单身东京狗 (极夜皓白)
            new Color(0.0f, 0.90f, 0.85f), // 3: 森罗灵弓 (翠灵青翠)
            new Color(1.0f, 0.40f, 0.70f), // 4: 周年卫衣 (浪漫粉霞)
            new Color(0.95f, 0.75f, 0.20f),// 5: 梦中花海 (流光幻金)
            new Color(0.50f, 0.25f, 0.95f),// 6: 周年庆典 (幻紫星空)
            new Color(0.25f, 0.60f, 0.95f),// 7: 经典蓝灰 (湛蓝探险)
            new Color(1.0f, 0.25f, 0.15f), // 8: 烈焰战甲 (炽热熔岩)
            new Color(0.15f, 0.85f, 0.45f),// 9: 灵溪法袍 (翡翠清泉)
            new Color(0.45f, 0.15f, 0.95f),// 10: 暗夜行者 (暗夜潜行)
            new Color(1.0f, 0.85f, 0.15f)  // 11: 黄金神圣 (炽阳战甲)
        };

        private Material _bodyMatInstance;
        private Material _visorMatInstance;

        private void Awake()
        {
            _initialLocalPos = transform.localPosition;
            InitVisualReferences();
        }

        public void InitVisualReferences()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual");
                if (visualRoot == null) visualRoot = transform;
            }

            if (bodyRenderer == null && visualRoot != null)
            {
                var bodyTrans = visualRoot.Find("Body");
                if (bodyTrans != null) bodyRenderer = bodyTrans.GetComponent<MeshRenderer>();
            }

            if (visorRenderer == null && visualRoot != null)
            {
                var visorTrans = visualRoot.Find("Visor");
                if (visorTrans != null) visorRenderer = visorTrans.GetComponent<MeshRenderer>();
            }

            if (overheadRoot == null)
            {
                overheadRoot = transform.Find("OverheadUI");
            }

            if (overheadText == null && overheadRoot != null)
            {
                overheadText = overheadRoot.GetComponentInChildren<TMP_Text>();
            }

            // Create material instances so changes don't leak to assets
            if (bodyRenderer != null && bodyRenderer.material != null)
            {
                _bodyMatInstance = new Material(bodyRenderer.material);
                bodyRenderer.material = _bodyMatInstance;
            }

            if (visorRenderer != null && visorRenderer.material != null)
            {
                _visorMatInstance = new Material(visorRenderer.material);
                visorRenderer.material = _visorMatInstance;
            }
        }

        private void Update()
        {
            // 1. Smooth rotation toward target yaw
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _angularVelocity, 0.08f);
            transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            // 2. Subtle idle breathing float
            float idleBob = Mathf.Sin(Time.time * 2.0f) * 0.015f;
            transform.localPosition = _initialLocalPos + new Vector3(0, idleBob, 0);

            // 3. Overhead title billboards to current camera
            if (overheadRoot != null)
            {
                Camera activeCam = (WardrobeSceneController.Instance != null && WardrobeSceneController.Instance.WardrobeCamera != null && WardrobeSceneController.Instance.WardrobeCamera.isActiveAndEnabled)
                    ? WardrobeSceneController.Instance.WardrobeCamera
                    : (Camera.main != null ? Camera.main : Camera.current);
                if (activeCam != null)
                {
                    overheadRoot.rotation = activeCam.transform.rotation;
                }
            }
        }

        /// <summary>
        /// 接收拖拽输入旋转角色
        /// </summary>
        public void AddRotationInput(float deltaX)
        {
            _targetYaw -= deltaX * 0.75f;
        }

        public void ResetRotation()
        {
            _targetYaw = 180f;
            _currentYaw = 180f;
            _angularVelocity = 0f;
        }

        /// <summary>
        /// 实时应用时装配色与视觉
        /// </summary>
        public void ApplyCostumeVisual(int costumeId)
        {
            int safeIdx = Mathf.Clamp(costumeId, 0, CostumePalette.Length - 1);
            Color col = CostumePalette[safeIdx];

            if (bodyRenderer != null)
            {
                if (_bodyMatInstance == null && bodyRenderer.material != null)
                {
                    _bodyMatInstance = new Material(bodyRenderer.material);
                    bodyRenderer.material = _bodyMatInstance;
                }
                if (_bodyMatInstance != null)
                {
                    _bodyMatInstance.color = col;
                    // Boost metallic / smoothness for deluxe costumes
                    if (costumeId == 0 || costumeId == 11)
                    {
                        if (_bodyMatInstance.HasProperty("_Glossiness")) _bodyMatInstance.SetFloat("_Glossiness", 0.85f);
                        if (_bodyMatInstance.HasProperty("_Metallic")) _bodyMatInstance.SetFloat("_Metallic", 0.5f);
                    }
                    else
                    {
                        if (_bodyMatInstance.HasProperty("_Glossiness")) _bodyMatInstance.SetFloat("_Glossiness", 0.5f);
                        if (_bodyMatInstance.HasProperty("_Metallic")) _bodyMatInstance.SetFloat("_Metallic", 0.1f);
                    }
                }
            }

            if (visorRenderer != null)
            {
                if (_visorMatInstance == null && visorRenderer.material != null)
                {
                    _visorMatInstance = new Material(visorRenderer.material);
                    visorRenderer.material = _visorMatInstance;
                }
                if (_visorMatInstance != null)
                {
                    // Tint visor subtly to match costume accent
                    Color visorCol = Color.Lerp(Color.black, col, 0.25f);
                    _visorMatInstance.color = visorCol;
                }
            }
        }

        private static string FormatTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            title = title.Trim();
            if (title.StartsWith("【") && title.EndsWith("】")) return title;
            return $"【{title}】";
        }

        /// <summary>
        /// 实时应用头顶称号与昵称
        /// </summary>
        public void ApplyTitleAndName(string username, string title)
        {
            if (overheadText != null)
            {
                string formattedTitle = FormatTitle(title);
                string titlePrefix = !string.IsNullOrEmpty(formattedTitle)
                    ? $"<size=75%><color=#FFD54F><b>{formattedTitle}</b></color></size>\n"
                    : "";
                overheadText.text = $"{titlePrefix}<color=#00E5FF>{username}</color>";
            }
        }

        private void OnDestroy()
        {
            if (_bodyMatInstance != null) Destroy(_bodyMatInstance);
            if (_visorMatInstance != null) Destroy(_visorMatInstance);
        }
    }
}
