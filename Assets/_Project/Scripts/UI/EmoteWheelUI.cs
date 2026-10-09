using System;
using UnityEngine;
using SmartSpace.Character;
using SmartSpace.Network;

namespace SmartSpace.UI
{
    /// <summary>
    /// 奥拉星手游 1:1 风格底部动作与表情面板 (示图 1)
    /// - 独立于聊天系统，位于收缩聊天窗口右侧或快捷键 [T] 打开
    /// - 左侧：10 个常用肢体姿势 (圆形按钮，触发人物 3D 姿态动画与动作广播)
    /// - 右侧：6 个伊乐 Mascot 2D 萌宠表情气泡 (点击后在玩家头顶以 2D 气泡呈现)
    /// - 右上角：红色关闭按钮 [✕]
    /// </summary>
    public class EmoteWheelUI : MonoBehaviour
    {
        private static EmoteWheelUI _instance;
        public static EmoteWheelUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<EmoteWheelUI>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        // Poses definition (10 Poses, 2 rows of 5)
        public struct PoseItem
        {
            public string icon;
            public string name;
            public EmoteType type;
            public string desc;
            public PoseItem(string icon, string name, EmoteType type, string desc)
            {
                this.icon = icon;
                this.name = name;
                this.type = type;
                this.desc = desc;
            }
        }

        private readonly PoseItem[] _poses = new PoseItem[]
        {
            // Row 1
            new PoseItem("🙋", "招手", EmoteType.Wave, "热情地挥手问好 👋"),
            new PoseItem("🪄", "欢呼", EmoteType.HighFive, "高高举起双手欢呼 ✨"),
            new PoseItem("🧘", "打坐", EmoteType.Bow, "席地盘坐静心冥想 🧘"),
            new PoseItem("🛌", "躺平", EmoteType.None, "舒适地躺平小憩 💤"),
            new PoseItem("🤷", "思考", EmoteType.None, "困惑地抓耳挠腮 🤔"),
            // Row 2
            new PoseItem("😤", "抱胸", EmoteType.None, "双手抱胸傲娇叉腰 😤"),
            new PoseItem("🛋️", "斜倚", EmoteType.None, "惬意地斜倚放松 🛋️"),
            new PoseItem("👏", "鼓掌", EmoteType.Clap, "兴奋地鼓掌喝彩 👏"),
            new PoseItem("🕺", "摇摆", EmoteType.Dance, "在广场中央欢快跳舞 🕺"),
            new PoseItem("🌹", "示爱", EmoteType.Heart, "飞吻送出一颗闪烁爱心 💖")
        };

        // 2D Mascot Emotes definition (6 Yile Mascot Emotes)
        public struct MascotEmote
        {
            public string name;
            public string symbol;
            public string desc;
            public MascotEmote(string name, string symbol, string desc)
            {
                this.name = name;
                this.symbol = symbol;
                this.desc = desc;
            }
        }

        private readonly MascotEmote[] _mascotEmotes = new MascotEmote[]
        {
            new MascotEmote("伊乐·微笑", "😸", "露出了阳光开朗的微笑"),
            new MascotEmote("伊乐·大哭", "😭", "委屈得哇哇大哭起来"),
            new MascotEmote("伊乐·傲娇", "😏", "轻蔑一笑，傲娇自信"),
            new MascotEmote("伊乐·害羞", "😳", "害羞地红了脸低下头"),
            new MascotEmote("伊乐·委屈", "🥺", "眼含泪花，可怜巴巴"),
            new MascotEmote("伊乐·比心", "😍", "眼里冒出闪耀的小爱心")
        };

        private Texture2D _texDrawerBg;
        private Texture2D _texCircleNormal;
        private Texture2D _texCircleHover;
        private Texture2D _texBubbleNormal;
        private Texture2D _texBubbleHover;
        private Texture2D _texCloseRed;

        private GUIStyle _drawerStyle;
        private GUIStyle _poseCircleStyle;
        private GUIStyle _mascotBubbleStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _closeBtnStyle;
        private bool _stylesReady = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            CleanupTextures();
        }

        private void Update()
        {
            // Toggle with T key when not typing in chat
            if (Input.GetKeyDown(toggleKey) && !DemoHUD.IsTyping)
            {
                ToggleOpen();
            }

            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWheel();
            }
        }

        public void ToggleOpen()
        {
            if (_isOpen) CloseWheel();
            else OpenWheel();
        }

        public void OpenWheel()
        {
            _isOpen = true;
            if (DemoHUD.Instance != null && DemoHUD.Instance.IsExpanded)
            {
                DemoHUD.Instance.ToggleExpand();
            }
        }

        public void CloseWheel()
        {
            _isOpen = false;
        }

        public void TriggerPose(PoseItem pose)
        {
            var localPlayer = FindAnyObjectByType<LocalPlayerController>();
            if (localPlayer != null)
            {
                if (pose.type != EmoteType.None)
                {
                    localPlayer.TriggerEmote((sbyte)pose.type);
                }
                else
                {
                    localPlayer.TriggerEmote((sbyte)EmoteType.Dance);
                }
            }
        }

        public void Trigger2DEmote(MascotEmote emote)
        {
            var localPlayer = FindAnyObjectByType<LocalPlayerController>();
            if (localPlayer != null)
            {
                localPlayer.ShowOverhead2DEmote(emote.name, emote.symbol);
            }
        }

        private Texture2D MakeSolidTex(int w, int h, Color col)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeBorderedTex(int w, int h, Color bg, Color border, int bWidth)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pix = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < bWidth || x >= w - bWidth || y < bWidth || y >= h - bWidth)
                        pix[y * w + x] = border;
                    else
                        pix[y * w + x] = bg;
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCircleTex(int size, Color fill, Color border, float bWidth)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pix = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius)
                    {
                        pix[y * size + x] = Color.clear;
                    }
                    else if (dist >= radius - bWidth)
                    {
                        pix[y * size + x] = border;
                    }
                    else
                    {
                        pix[y * size + x] = fill;
                    }
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            // 1. Textures
            _texDrawerBg = MakeBorderedTex(32, 32, new Color(0.05f, 0.10f, 0.22f, 0.94f), new Color(0.20f, 0.60f, 1.0f, 0.90f), 2);
            _texCircleNormal = MakeCircleTex(46, new Color(0.10f, 0.28f, 0.55f, 0.92f), new Color(0.40f, 0.80f, 1.0f, 0.90f), 2f);
            _texCircleHover = MakeCircleTex(46, new Color(0.15f, 0.45f, 0.88f, 1.0f), new Color(1.0f, 0.88f, 0.25f, 1.0f), 2.5f);
            _texBubbleNormal = MakeBorderedTex(48, 48, new Color(0.08f, 0.20f, 0.40f, 0.92f), new Color(0.30f, 0.70f, 1.0f, 0.85f), 2);
            _texBubbleHover = MakeBorderedTex(48, 48, new Color(0.14f, 0.35f, 0.70f, 1.0f), new Color(1.0f, 0.85f, 0.20f, 1.0f), 2);
            _texCloseRed = MakeBorderedTex(16, 16, new Color(0.85f, 0.15f, 0.20f, 1.0f), new Color(1.0f, 0.40f, 0.45f, 1.0f), 1);

            // 2. Styles
            _drawerStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texDrawerBg },
                padding = new RectOffset(12, 12, 8, 8)
            };

            _poseCircleStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = _texCircleNormal },
                hover = { textColor = new Color(1f, 0.88f, 0.25f), background = _texCircleHover },
                active = { textColor = Color.white, background = _texCircleHover },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _mascotBubbleStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                richText = true,
                normal = { textColor = Color.white, background = _texBubbleNormal },
                hover = { textColor = new Color(1f, 0.88f, 0.25f), background = _texBubbleHover },
                active = { textColor = Color.white, background = _texBubbleHover },
                padding = new RectOffset(1, 1, 1, 1)
            };

            _sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(0.60f, 0.85f, 1.0f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _texCloseRed },
                hover = { textColor = Color.white, background = _texCloseRed }
            };

            _stylesReady = true;
        }

        private void CleanupTextures()
        {
            if (_texDrawerBg != null) Destroy(_texDrawerBg);
            if (_texCircleNormal != null) Destroy(_texCircleNormal);
            if (_texCircleHover != null) Destroy(_texCircleHover);
            if (_texBubbleNormal != null) Destroy(_texBubbleNormal);
            if (_texBubbleHover != null) Destroy(_texBubbleHover);
            if (_texCloseRed != null) Destroy(_texCloseRed);
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            InitStyles();

            float panelW = Mathf.Clamp(Screen.width * 0.80f, 760f, 920f);
            float panelH = 152f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = Screen.height - panelH - 14f;

            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUILayout.BeginArea(panelRect, _drawerStyle);

            // 1. Top Bar: Titles + Red Close Button
            GUILayout.BeginHorizontal(GUILayout.Height(20));
            GUILayout.Label("<color=#40C4FF><b>💃 肢体姿势</b></color> <size=11><color=#90CAF9>(点击执行3D动作)</color></size>", _sectionTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#FFD54F><b>🐱 2D萌宠表情</b></color> <size=11><color=#FFE082>(点击弹显在玩家头顶)</color></size>", _sectionTitleStyle);
            GUILayout.Space(24);
            if (GUILayout.Button("✕", _closeBtnStyle, GUILayout.Width(24), GUILayout.Height(20)))
            {
                CloseWheel();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Main Content Split: Left Poses (10) | Divider | Right 2D Emotes (6)
            GUILayout.BeginHorizontal();

            // -------------------------------------------------------------
            // LEFT: 10 POSES (2 Rows x 5 Circles)
            // -------------------------------------------------------------
            float leftW = panelW * 0.54f;
            GUILayout.BeginVertical(GUILayout.Width(leftW));

            // Row 1 (Poses 0..4)
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 5; i++)
            {
                var pose = _poses[i];
                if (GUILayout.Button(pose.name, _poseCircleStyle, GUILayout.Width(44), GUILayout.Height(44)))
                {
                    TriggerPose(pose);
                }
                if (i < 4) GUILayout.Space(12);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Row 2 (Poses 5..9)
            GUILayout.BeginHorizontal();
            for (int i = 5; i < 10; i++)
            {
                var pose = _poses[i];
                if (GUILayout.Button(pose.name, _poseCircleStyle, GUILayout.Width(44), GUILayout.Height(44)))
                {
                    TriggerPose(pose);
                }
                if (i < 9) GUILayout.Space(12);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // Divider
            GUILayout.Space(10);
            Rect divRect = GUILayoutUtility.GetRect(2, 90, GUILayout.Width(2), GUILayout.Height(90));
            GUI.DrawTexture(divRect, _texCircleNormal);
            GUILayout.Space(10);

            // -------------------------------------------------------------
            // RIGHT: 6 2D MASCOT EMOTE BUBBLES (5 in Row 1, 1 in Row 2 - matching Fig 1)
            // -------------------------------------------------------------
            GUILayout.BeginVertical();

            // Row 1 (Emotes 0..4)
            GUILayout.BeginHorizontal();
            for (int j = 0; j < 5 && j < _mascotEmotes.Length; j++)
            {
                var emote = _mascotEmotes[j];
                string kaomoji = emote.name switch
                {
                    "伊乐·微笑" => "(*^▽^*)",
                    "伊乐·大哭" => "( ╥﹏╥ )",
                    "伊乐·傲娇" => "( ￣^￣ )",
                    "伊乐·害羞" => "(*/ω＼*)",
                    "伊乐·委屈" => "(っ˘̩╭╮˘̩)っ",
                    _ => "^_^"
                };
                string shortName = emote.name.Replace("伊乐·", "");

                if (GUILayout.Button($"<size=11><b>{kaomoji}</b></size>\n<size=9><color=#80D8FF>{shortName}</color></size>", _mascotBubbleStyle, GUILayout.Width(50), GUILayout.Height(44)))
                {
                    Trigger2DEmote(emote);
                }

                if (j < 4) GUILayout.Space(6);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Row 2 (Emote 5: 比心)
            GUILayout.BeginHorizontal();
            if (_mascotEmotes.Length > 5)
            {
                var emote = _mascotEmotes[5];
                string kaomoji = "(づ￣ ³￣)づ";
                string shortName = emote.name.Replace("伊乐·", "");

                if (GUILayout.Button($"<size=11><b>{kaomoji}</b></size>\n<size=9><color=#80D8FF>{shortName}</color></size>", _mascotBubbleStyle, GUILayout.Width(50), GUILayout.Height(44)))
                {
                    Trigger2DEmote(emote);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
