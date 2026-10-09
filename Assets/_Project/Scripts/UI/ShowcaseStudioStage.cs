using System.Collections.Generic;
using UnityEngine;

namespace SmartSpace.UI
{
    /// <summary>
    /// 3D 摄影棚展台系统 (Character Showcase Studio Stage)
    /// 为个人信息、时装衣橱、成就称号界面提供 1:1 风格的梦幻 3D 背景展台：
    /// 1. 星海/深空天幕背景 (Celestial Deep Ocean & Nebula Backdrop)
    /// 2. 游弋的星海巨鲸 (Swimming Celestial Whale)
    /// 3. 立体梦幻云海地台 (Stylized Cloud Pedestal & Cloud Sea Floor)
    /// 4. 角色背部专属旋转光环法阵 (Rotating Sacred Golden Magic Circle)
    /// 5. 梦幻云端小飞船萌宠浮空装饰 (Celestial Mascot Boat Prop)
    /// 6. 3D 摄影棚专业三点光照与高光边缘光 (Studio 3-Point & Rim Lighting)
    /// 7. 漫游星尘漂浮粒子特效 (Floating Stardust Particle System)
    /// </summary>
    public class ShowcaseStudioStage : MonoBehaviour
    {
        public static ShowcaseStudioStage Instance { get; private set; }

        [Header("State")]
        [SerializeField] private bool _isActive = false;
        public bool IsActive => _isActive;

        private Transform _targetCharacter;
        private Camera _targetCamera;

        // Root GameObjects
        private GameObject _studioRoot;
        private GameObject _backdropObj;
        private GameObject _whaleObj;
        private GameObject _cloudPlatformObj;
        private GameObject _cloudFloorObj;
        private GameObject _magicCircleObj;
        private GameObject _mascotBoatObj;
        private GameObject _particleObj;
        private GameObject _lightingObj;

        // Lighting
        private Light _keyLight;
        private Light _fillLight;
        private Light _rimLight;

        // Components & Animators
        private ParticleSystem _stardustParticles;
        private readonly List<Transform> _cloudPuffs = new List<Transform>();
        private readonly List<Vector3> _cloudBaseScales = new List<Vector3>();

        // Procedural Textures & Materials
        private Texture2D _texBackdrop;
        private Texture2D _texMagicCircle;
        private Texture2D _texWhale;
        private Texture2D _texCloud;
        private Texture2D _texCloudFloor;
        private Texture2D _texStardust;
        private Texture2D _texBoat;

        private Material _matBackdrop;
        private Material _matMagicCircle;
        private Material _matWhale;
        private Material _matCloud;
        private Material _matCloudFloor;
        private Material _matBoat;
        private Material _matPedestalRing;

        // Animation Parameters
        private float _whaleSwimProgress = 0.35f;

        public static ShowcaseStudioStage GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("ShowcaseStudioStage");
                Instance = go.AddComponent<ShowcaseStudioStage>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            GenerateProceduralAssets();
            BuildStudioHierarchy();
            SetStudioActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupAssets();
        }

        public void Show(Transform character, Camera cam)
        {
            _targetCharacter = character;
            _targetCamera = cam != null ? cam : Camera.main;

            if (_targetCharacter == null) return;

            UpdateCameraAlignment();
            SetStudioActive(true);
        }

        public void Hide()
        {
            SetStudioActive(false);
            _targetCharacter = null;
            _targetCamera = null;
        }

        public void SetStudioActive(bool active)
        {
            _isActive = active;
            if (_studioRoot != null)
            {
                _studioRoot.SetActive(active);
            }

            if (_stardustParticles != null)
            {
                if (active) _stardustParticles.Play();
                else _stardustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateCameraAlignment()
        {
            if (_studioRoot == null) return;

            Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
            if (cam == null) return;

            // Anchor studio root precisely to camera's position & orientation
            // This guarantees 0% tilt and 100% viewport coverage for backdrop and distant layers
            _studioRoot.transform.position = cam.transform.position;
            _studioRoot.transform.rotation = cam.transform.rotation;
        }

        private void LateUpdate()
        {
            if (!_isActive || _targetCharacter == null) return;

            // 1. Keep backdrop and camera studio root locked with camera
            UpdateCameraAlignment();

            // 2. Animate and keep Cloud Platform anchored at character's feet in world space
            if (_cloudPlatformObj != null)
            {
                _cloudPlatformObj.transform.position = _targetCharacter.position;
                // Keep cloud platform leveled with ground
                _cloudPlatformObj.transform.rotation = Quaternion.identity;
            }

            // 3. Animate Magic Circle (法阵缓慢自转 + 呼吸律动，贴合角色上半身背面)
            if (_magicCircleObj != null)
            {
                Vector3 charTorso = _targetCharacter.position + Vector3.up * 1.35f - _targetCharacter.forward * 0.28f;
                _magicCircleObj.transform.position = charTorso;
                _magicCircleObj.transform.rotation = _targetCharacter.rotation * Quaternion.Euler(0, 180f, 0);

                // Slow roll on Z axis
                _magicCircleObj.transform.Rotate(0, 0, -18f * Time.deltaTime, Space.Self);

                // Breathing scale pulse
                float pulse = 1.95f + Mathf.Sin(Time.time * 2.2f) * 0.08f;
                _magicCircleObj.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            // 4. Animate Swimming Whale (星海巨鲸在深空背景前缓缓游弋)
            if (_whaleObj != null)
            {
                _whaleSwimProgress += Time.deltaTime * 0.035f;
                if (_whaleSwimProgress > 1f) _whaleSwimProgress -= 1f;

                // Swim horizontally across camera view from left (-3.2) to right (+3.2)
                float swimX = Mathf.Lerp(-3.2f, 3.2f, _whaleSwimProgress);
                float swimY = 0.95f + Mathf.Sin(Time.time * 0.8f) * 0.25f;
                float swimZ = 5.8f;

                _whaleObj.transform.localPosition = new Vector3(swimX, swimY, swimZ);
                float pitch = Mathf.Sin(Time.time * 0.8f) * 4f;
                _whaleObj.transform.localRotation = Quaternion.Euler(-pitch, 0, 0);
            }

            // 5. Animate Cloud Platform Puffs (云海地台呼吸浮动)
            for (int i = 0; i < _cloudPuffs.Count; i++)
            {
                if (_cloudPuffs[i] != null && i < _cloudBaseScales.Count)
                {
                    float offset = i * 0.6f;
                    float puffBob = Mathf.Sin(Time.time * 1.6f + offset) * 0.035f;
                    _cloudPuffs[i].localScale = _cloudBaseScales[i] * (1f + puffBob);
                }
            }

            // 6. Animate Mascot Boat (萌宠飞船浮动)
            if (_mascotBoatObj != null)
            {
                float boatBob = Mathf.Sin(Time.time * 1.3f + 1f) * 0.045f;
                float boatRock = Mathf.Sin(Time.time * 1.0f) * 3.5f;
                Vector3 boatBasePos = _targetCharacter.position + _targetCharacter.right * 1.35f + Vector3.up * (0.35f + boatBob) + _targetCharacter.forward * 0.25f;
                _mascotBoatObj.transform.position = boatBasePos;
                _mascotBoatObj.transform.rotation = _targetCharacter.rotation * Quaternion.Euler(0, -35f, boatRock);
            }
        }

        #region Stage Assembly

        private void BuildStudioHierarchy()
        {
            _studioRoot = new GameObject("ShowcaseStudioRoot");
            _studioRoot.transform.SetParent(this.transform);

            Shader unlitTexture = Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            Shader unlitTransparent = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            Shader additiveShader = Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");

            // A. Deep Ocean & Nebula Backdrop (全屏无缝星海天幕，直接挂载于相机坐标空间)
            _backdropObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _backdropObj.name = "CelestialBackdrop";
            _backdropObj.transform.SetParent(_studioRoot.transform);
            _backdropObj.transform.localPosition = new Vector3(0f, 0f, 7.5f);
            _backdropObj.transform.localRotation = Quaternion.identity;
            _backdropObj.transform.localScale = new Vector3(22f, 13f, 1f);
            Destroy(_backdropObj.GetComponent<Collider>());

            _matBackdrop = new Material(unlitTexture);
            _matBackdrop.mainTexture = _texBackdrop;
            _matBackdrop.color = Color.white;
            _backdropObj.GetComponent<MeshRenderer>().material = _matBackdrop;

            // B. Cosmic Whale (星海游鲸)
            _whaleObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _whaleObj.name = "CelestialWhale";
            _whaleObj.transform.SetParent(_studioRoot.transform);
            _whaleObj.transform.localPosition = new Vector3(-0.8f, 0.95f, 5.8f);
            _whaleObj.transform.localRotation = Quaternion.identity;
            _whaleObj.transform.localScale = new Vector3(4.8f, 2.4f, 1f);
            Destroy(_whaleObj.GetComponent<Collider>());

            _matWhale = new Material(unlitTransparent);
            _matWhale.mainTexture = _texWhale;
            _matWhale.color = Color.white;
            _whaleObj.GetComponent<MeshRenderer>().material = _matWhale;

            // C. Cloud Platform (梦幻立体云海地台与云海平原)
            _cloudPlatformObj = new GameObject("CloudPlatform");
            _cloudPlatformObj.transform.SetParent(this.transform); // Separate from camera root to stay at character feet
            _cloudPlatformObj.transform.localPosition = Vector3.zero;
            _cloudPlatformObj.transform.localRotation = Quaternion.identity;

            // C1. Cloud Sea Floor (遮挡下方原本广场地面的云海平原)
            _cloudFloorObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cloudFloorObj.name = "CloudSeaFloor";
            _cloudFloorObj.transform.SetParent(_cloudPlatformObj.transform);
            _cloudFloorObj.transform.localPosition = new Vector3(0f, 0.01f, 1.8f);
            _cloudFloorObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            _cloudFloorObj.transform.localScale = new Vector3(18f, 12f, 1f);
            Destroy(_cloudFloorObj.GetComponent<Collider>());

            _matCloudFloor = new Material(unlitTransparent);
            _matCloudFloor.mainTexture = _texCloudFloor;
            _matCloudFloor.color = Color.white;
            _cloudFloorObj.GetComponent<MeshRenderer>().material = _matCloudFloor;

            // C2. Pedestal Aura Ring (脚底发光图腾)
            var groundRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundRing.name = "PedestalAuraRing";
            groundRing.transform.SetParent(_cloudPlatformObj.transform);
            groundRing.transform.localPosition = new Vector3(0, 0.03f, 0);
            groundRing.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            groundRing.transform.localScale = new Vector3(3.6f, 3.6f, 1f);
            Destroy(groundRing.GetComponent<Collider>());

            _matPedestalRing = new Material(additiveShader);
            _matPedestalRing.mainTexture = _texMagicCircle;
            _matPedestalRing.color = new Color(0.25f, 0.85f, 1.0f, 0.70f);
            groundRing.GetComponent<MeshRenderer>().material = _matPedestalRing;

            // C3. Fluffy Cloud Puffs (脚下蓬松云团)
            _matCloud = new Material(unlitTransparent);
            _matCloud.mainTexture = _texCloud;
            _matCloud.color = new Color(1f, 1f, 1f, 0.96f);

            _cloudPuffs.Clear();
            _cloudBaseScales.Clear();

            Vector3[] cloudOffsets = new Vector3[]
            {
                // Near feet
                new Vector3(0f, 0.05f, 0.6f),
                new Vector3(-0.9f, 0.08f, 0.35f),
                new Vector3(0.9f, 0.06f, 0.25f),
                new Vector3(-1.5f, 0.12f, -0.1f),
                new Vector3(1.4f, 0.10f, -0.1f),
                new Vector3(-0.6f, 0.04f, -0.4f),
                new Vector3(0.6f, 0.05f, -0.35f),
                new Vector3(0f, 0.12f, 1.1f),

                // Horizon fluffy clouds (rise up along the horizon, softening the seam into heaven)
                new Vector3(-2.2f, 0.45f, 2.2f),
                new Vector3(-0.8f, 0.55f, 2.8f),
                new Vector3(0.8f, 0.50f, 2.7f),
                new Vector3(2.2f, 0.42f, 2.3f),
                new Vector3(-1.4f, 0.65f, 3.4f),
                new Vector3(1.5f, 0.60f, 3.2f)
            };

            Vector2[] cloudSizes = new Vector2[]
            {
                new Vector2(3.6f, 1.9f),
                new Vector2(2.8f, 1.5f),
                new Vector2(3.0f, 1.6f),
                new Vector2(2.4f, 1.3f),
                new Vector2(2.6f, 1.4f),
                new Vector2(2.8f, 1.5f),
                new Vector2(2.8f, 1.5f),
                new Vector2(4.2f, 2.0f),

                // Horizon cloud sizes
                new Vector2(4.5f, 2.2f),
                new Vector2(5.2f, 2.6f),
                new Vector2(4.8f, 2.4f),
                new Vector2(4.2f, 2.0f),
                new Vector2(5.8f, 2.8f),
                new Vector2(5.5f, 2.7f)
            };

            for (int i = 0; i < cloudOffsets.Length; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puff.name = $"CloudPuff_{i}";
                puff.transform.SetParent(_cloudPlatformObj.transform);
                puff.transform.localPosition = cloudOffsets[i];
                puff.transform.localRotation = Quaternion.Euler(75f, 0, 0); // Facing camera
                puff.transform.localScale = new Vector3(cloudSizes[i].x, cloudSizes[i].y, 1f);
                Destroy(puff.GetComponent<Collider>());
                puff.GetComponent<MeshRenderer>().material = _matCloud;

                _cloudPuffs.Add(puff.transform);
                _cloudBaseScales.Add(puff.transform.localScale);
            }

            // D. Golden Sacred Magic Circle (角色背部旋转法阵)
            _magicCircleObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _magicCircleObj.name = "SacredMagicCircle";
            _magicCircleObj.transform.SetParent(this.transform);
            _magicCircleObj.transform.localPosition = new Vector3(0, 1.35f, -0.28f);
            _magicCircleObj.transform.localRotation = Quaternion.identity;
            _magicCircleObj.transform.localScale = new Vector3(1.95f, 1.95f, 1f);
            Destroy(_magicCircleObj.GetComponent<Collider>());

            _matMagicCircle = new Material(additiveShader);
            _matMagicCircle.mainTexture = _texMagicCircle;
            _matMagicCircle.color = new Color(1f, 0.88f, 0.35f, 0.98f);
            _magicCircleObj.GetComponent<MeshRenderer>().material = _matMagicCircle;

            // E. Mascot Boat Prop (萌宠云海飞船)
            _mascotBoatObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _mascotBoatObj.name = "MascotBoat";
            _mascotBoatObj.transform.SetParent(this.transform);
            _mascotBoatObj.transform.localScale = new Vector3(1.4f, 1.25f, 1f);
            Destroy(_mascotBoatObj.GetComponent<Collider>());

            _matBoat = new Material(unlitTransparent);
            _matBoat.mainTexture = _texBoat;
            _matBoat.color = Color.white;
            _mascotBoatObj.GetComponent<MeshRenderer>().material = _matBoat;

            // F. Particle System (梦幻星尘与浮光微粒)
            _particleObj = new GameObject("StardustParticleSystem");
            _particleObj.transform.SetParent(_cloudPlatformObj.transform);
            _particleObj.transform.localPosition = new Vector3(0, 0.35f, 0);

            _stardustParticles = _particleObj.AddComponent<ParticleSystem>();
            var pMain = _stardustParticles.main;
            pMain.startLifetime = 4.5f;
            pMain.startSpeed = 0.5f;
            pMain.startSize = 0.18f;
            pMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.5f, 0.85f), new Color(0.4f, 0.92f, 1f, 0.85f));
            pMain.maxParticles = 90;
            pMain.simulationSpace = ParticleSystemSimulationSpace.World;
            pMain.loop = true;

            var pEmission = _stardustParticles.emission;
            pEmission.rateOverTime = 20f;

            var pShape = _stardustParticles.shape;
            pShape.shapeType = ParticleSystemShapeType.Box;
            pShape.scale = new Vector3(4.0f, 0.4f, 3.0f);

            var pVelocity = _stardustParticles.velocityOverLifetime;
            pVelocity.enabled = true;
            pVelocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            pVelocity.y = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            pVelocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var pSize = _stardustParticles.sizeOverLifetime;
            pSize.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.3f);
            sizeCurve.AddKey(0.4f, 1f);
            sizeCurve.AddKey(1f, 0f);
            pSize.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var pRenderer = _stardustParticles.GetComponent<ParticleSystemRenderer>();
            pRenderer.material = new Material(additiveShader) { mainTexture = _texStardust };

            // G. 3-Point Studio Lighting (专属摄影棚灯光)
            _lightingObj = new GameObject("StudioLightingRig");
            _lightingObj.transform.SetParent(_studioRoot.transform);
            _lightingObj.transform.localPosition = Vector3.zero;

            // 1. Key Light (主光：前上方柔和暖白光)
            var keyGo = new GameObject("StudioKeyLight");
            keyGo.transform.SetParent(_lightingObj.transform);
            keyGo.transform.localPosition = new Vector3(-0.5f, 1.8f, 0.5f);
            keyGo.transform.localRotation = Quaternion.Euler(30f, 15f, 0);
            _keyLight = keyGo.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.color = new Color(1.0f, 0.97f, 0.92f);
            _keyLight.intensity = 1.35f;

            // 2. Fill Light (补光：右下方冷蓝光)
            var fillGo = new GameObject("StudioFillLight");
            fillGo.transform.SetParent(_lightingObj.transform);
            fillGo.transform.localPosition = new Vector3(1.5f, 0.8f, 0.5f);
            fillGo.transform.localRotation = Quaternion.Euler(15f, -30f, 0);
            _fillLight = fillGo.AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.color = new Color(0.60f, 0.85f, 1.0f);
            _fillLight.intensity = 0.65f;

            // 3. Rim Light (边缘高光：正后方强逆光，照出二次元轮廓金边)
            var rimGo = new GameObject("StudioRimLight");
            rimGo.transform.SetParent(_lightingObj.transform);
            rimGo.transform.localPosition = new Vector3(0f, 1.5f, 4.5f);
            rimGo.transform.localRotation = Quaternion.Euler(165f, 0f, 0f);
            _rimLight = rimGo.AddComponent<Light>();
            _rimLight.type = LightType.Directional;
            _rimLight.color = new Color(1.0f, 0.90f, 0.70f);
            _rimLight.intensity = 2.0f;
        }

        #endregion

        #region Procedural Asset Generators

        private void GenerateProceduralAssets()
        {
            _texBackdrop = MakeBackdropTexture(1024, 1024);
            _texMagicCircle = MakeMagicCircleTexture(512);
            _texWhale = MakeWhaleTexture(512, 256);
            _texCloud = MakeCloudTexture(512);
            _texCloudFloor = MakeCloudFloorTexture(512, 512);
            _texStardust = MakeStardustTexture(64);
            _texBoat = MakeBoatTexture(256, 256);
        }

        private Texture2D MakeBackdropTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height;
                Color baseCol;
                if (v < 0.25f)
                    baseCol = Color.Lerp(new Color(0.01f, 0.08f, 0.25f), new Color(0.03f, 0.22f, 0.58f), v / 0.25f);
                else if (v < 0.65f)
                    baseCol = Color.Lerp(new Color(0.03f, 0.22f, 0.58f), new Color(0.08f, 0.52f, 0.92f), (v - 0.25f) / 0.40f);
                else
                    baseCol = Color.Lerp(new Color(0.08f, 0.52f, 0.92f), new Color(0.22f, 0.82f, 1.0f), (v - 0.65f) / 0.35f);

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    Color c = baseCol;

                    // 1. Water Caustics / Sun Rays streaming from top-left
                    float ray1 = Mathf.Sin((u * 4.5f + v * 3.0f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float ray2 = Mathf.Sin((u * 8.5f - v * 2.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float caustics = Mathf.Pow((ray1 + ray2) * 0.5f, 2.0f) * 0.55f * Mathf.Clamp01(v * 1.5f);
                    c += new Color(0.35f, 0.85f, 1.0f) * caustics;

                    // 2. Soft Purple Nebula Cloud in upper-mid
                    float distNebula = Vector2.Distance(new Vector2(u, v), new Vector2(0.65f, 0.58f));
                    if (distNebula < 0.45f)
                    {
                        float nebAlpha = Mathf.SmoothStep(0.45f, 0f, distNebula) * 0.45f;
                        c = Color.Lerp(c, new Color(0.55f, 0.25f, 0.85f), nebAlpha);
                    }

                    // 3. Dense Twinkling Stars
                    if (((x * 47 + y * 83) % 193) == 0 && v > 0.12f)
                    {
                        float starIntensity = 0.7f + ((x + y) % 5) * 0.08f;
                        c += Color.white * starIntensity;
                    }

                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCloudFloorTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float billow1 = Mathf.Sin(u * 14f) * Mathf.Cos(v * 10f) * 0.16f;
                    float billow2 = Mathf.Sin(u * 28f + 1f) * Mathf.Sin(v * 18f) * 0.08f;
                    float factor = Mathf.Clamp01(0.82f + billow1 + billow2);

                    Color c = Color.Lerp(new Color(0.82f, 0.90f, 1.0f), Color.white, factor);
                    // Fade out at far horizon (v -> 1) and near feet (v -> 0)
                    float alpha = Mathf.SmoothStep(0f, 0.18f, v) * Mathf.SmoothStep(1f, 0.75f, v) * 0.96f;

                    pixels[y * width + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeMagicCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.48f;

            Color goldCore = new Color(1.0f, 0.95f, 0.65f, 1f);
            Color goldMid = new Color(1.0f, 0.80f, 0.20f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float r = Vector2.Distance(pos, center);
                    float normR = r / maxR;
                    float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);

                    float alpha = 0f;
                    Color c = goldMid;

                    if (normR <= 1.0f)
                    {
                        // 1. Outer Runes Ring (0.88 ~ 0.98)
                        if (normR >= 0.88f && normR <= 0.98f)
                        {
                            alpha = Mathf.Sin((normR - 0.88f) / 0.10f * Mathf.PI);
                            if (Mathf.Sin(angle * 16f) > 0.2f) alpha *= 1.4f;
                        }
                        // 2. Middle Ring (0.74 ~ 0.82)
                        else if (normR >= 0.74f && normR <= 0.82f)
                        {
                            alpha = Mathf.Sin((normR - 0.74f) / 0.08f * Mathf.PI) * 0.9f;
                            if (Mathf.Sin(angle * 24f) > 0.4f) alpha = 1.1f;
                        }
                        // 3. Tiger Crest / Solar Petals (0.38 ~ 0.68)
                        else if (normR >= 0.38f && normR <= 0.68f)
                        {
                            float petal = Mathf.Abs(Mathf.Sin(angle * 6f));
                            float crestR = 0.38f + petal * 0.26f;
                            if (Mathf.Abs(normR - crestR) < 0.07f)
                            {
                                alpha = 0.95f;
                                c = goldCore;
                            }
                        }
                        // 4. Inner Ring (0.20 ~ 0.28)
                        else if (normR >= 0.20f && normR <= 0.28f)
                        {
                            alpha = Mathf.Sin((normR - 0.20f) / 0.08f * Mathf.PI);
                            c = goldCore;
                        }
                        // 5. Central Core
                        else if (normR < 0.16f)
                        {
                            alpha = Mathf.SmoothStep(0.16f, 0f, normR) * 0.9f;
                            c = goldCore;
                        }

                        // Radiant 12 rays
                        float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 7f);
                        if (normR > 0.22f && normR < 0.96f)
                        {
                            alpha = Mathf.Max(alpha, ray * 0.7f);
                        }
                    }

                    alpha = Mathf.Clamp01(alpha);
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeWhaleTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    float bodyCenterY = 0.5f + Mathf.Sin(u * Mathf.PI * 1.5f) * 0.08f;
                    float bodyThickness = 0f;

                    if (u >= 0.12f && u <= 0.76f)
                    {
                        bodyThickness = Mathf.Sin((u - 0.12f) / 0.64f * Mathf.PI) * 0.26f;
                    }
                    else if (u > 0.76f && u <= 0.96f)
                    {
                        float tailProgress = (u - 0.76f) / 0.20f;
                        bodyThickness = 0.05f + Mathf.Sin(tailProgress * Mathf.PI) * 0.22f;
                    }

                    float dist = Mathf.Abs(v - bodyCenterY);
                    float alpha = 0f;
                    Color col = new Color(0.04f, 0.25f, 0.58f);

                    if (dist < bodyThickness)
                    {
                        float edge = 1f - (dist / bodyThickness);
                        alpha = Mathf.SmoothStep(0f, 1f, edge) * 0.95f;

                        // Luminous bioluminescent belly & back with glowing constellation stars
                        if (edge > 0.82f)
                        {
                            col = Color.Lerp(new Color(0.25f, 0.90f, 1.0f), Color.white, (edge - 0.82f) / 0.18f);
                        }
                        else if (((x * 19 + y * 41) % 47) == 0 && edge > 0.35f)
                        {
                            col = Color.white;
                            alpha = 1f;
                        }
                        else
                        {
                            col = Color.Lerp(new Color(0.03f, 0.18f, 0.45f), new Color(0.12f, 0.65f, 0.95f), edge);
                        }
                    }

                    pixels[y * width + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCloudTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];

            Vector2[] centers = new Vector2[]
            {
                new Vector2(0.50f, 0.44f),
                new Vector2(0.30f, 0.40f),
                new Vector2(0.70f, 0.40f),
                new Vector2(0.18f, 0.35f),
                new Vector2(0.82f, 0.35f),
                new Vector2(0.42f, 0.55f),
                new Vector2(0.58f, 0.55f)
            };
            float[] radii = new float[] { 0.34f, 0.28f, 0.28f, 0.20f, 0.20f, 0.26f, 0.26f };

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    Vector2 uv = new Vector2(u, v);

                    float maxMask = 0f;
                    for (int i = 0; i < centers.Length; i++)
                    {
                        float dist = Vector2.Distance(uv, centers[i]);
                        if (dist < radii[i])
                        {
                            float m = Mathf.SmoothStep(radii[i], radii[i] * 0.15f, dist);
                            if (m > maxMask) maxMask = m;
                        }
                    }

                    Color topShade = Color.white;
                    Color btmShade = new Color(0.78f, 0.90f, 1.0f);
                    Color cloudCol = Color.Lerp(btmShade, topShade, Mathf.Clamp01(v * 1.4f));

                    pixels[y * size + x] = new Color(cloudCol.r, cloudCol.g, cloudCol.b, Mathf.Clamp01(maxMask * 0.98f));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeStardustTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float r = Vector2.Distance(pos, center);
                    float normR = r / maxR;

                    float alpha = 0f;
                    if (normR < 1f)
                    {
                        float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);
                        float star = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 6f);
                        float glow = Mathf.SmoothStep(1f, 0f, normR);
                        alpha = Mathf.Max(glow * 0.65f, star * glow);
                    }

                    pixels[y * size + x] = new Color(1f, 0.96f, 0.85f, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeBoatTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;

                    float alpha = 0f;
                    Color c = Color.white;

                    // 1. Boat Hull (bottom half)
                    if (v >= 0.15f && v <= 0.45f && u >= 0.1f && u <= 0.9f)
                    {
                        float hullW = (0.45f - v) / 0.3f * 0.15f;
                        if (u >= 0.1f + hullW && u <= 0.9f - hullW)
                        {
                            alpha = 0.95f;
                            c = new Color(0.95f, 0.80f, 0.55f);
                            if (v >= 0.40f) c = new Color(1f, 0.92f, 0.35f);
                        }
                    }
                    // 2. Cute Mascot Ears / Sail (top half)
                    else if (v > 0.45f && v <= 0.85f && u >= 0.3f && u <= 0.7f)
                    {
                        float earR = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.6f));
                        if (earR < 0.22f)
                        {
                            alpha = 0.95f;
                            c = new Color(1f, 0.95f, 0.95f);
                        }
                        if (v > 0.70f && (u < 0.42f || u > 0.58f))
                        {
                            c = new Color(1f, 0.65f, 0.75f);
                        }
                    }

                    pixels[y * width + x] = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void CleanupAssets()
        {
            if (_matBackdrop != null) Destroy(_matBackdrop);
            if (_matMagicCircle != null) Destroy(_matMagicCircle);
            if (_matWhale != null) Destroy(_matWhale);
            if (_matCloud != null) Destroy(_matCloud);
            if (_matCloudFloor != null) Destroy(_matCloudFloor);
            if (_matBoat != null) Destroy(_matBoat);
            if (_matPedestalRing != null) Destroy(_matPedestalRing);

            if (_texBackdrop != null) Destroy(_texBackdrop);
            if (_texMagicCircle != null) Destroy(_texMagicCircle);
            if (_texWhale != null) Destroy(_texWhale);
            if (_texCloud != null) Destroy(_texCloud);
            if (_texCloudFloor != null) Destroy(_texCloudFloor);
            if (_texStardust != null) Destroy(_texStardust);
            if (_texBoat != null) Destroy(_texBoat);
        }

        #endregion
    }
}
