using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SmartSpace.Network;
using SmartSpace.Character;
using SmartSpace.UI;

namespace SmartSpace.Editor
{
    public static class NetManagerSceneSetup
    {
        private const string MaterialsPath = "Assets/_Project/Materials";
        private const string PrefabsPath = "Assets/_Project/Prefabs";

        [MenuItem("SmartSpace/Setup NetManager Test Scene")]
        public static void SetupScene()
        {
            EnsureDirectories();

            // 1. Create Materials
            Material localMat = GetOrCreateMaterial("M_LocalPlayer", new Color(0f, 0.85f, 1f)); // Cyan
            Material remoteMat = GetOrCreateMaterial("M_RemotePlayer", new Color(1f, 0.6f, 0.1f)); // Orange
            Material visorMat = GetOrCreateMaterial("M_Visor", new Color(0.12f, 0.15f, 0.2f)); // Dark Visor
            Material groundMat = GetOrCreateMaterial("M_Ground", new Color(0.2f, 0.24f, 0.28f)); // Dark Slate Plaza
            Material pillarMat = GetOrCreateMaterial("M_Pillar", new Color(0.35f, 0.42f, 0.5f)); // Lighter Slate

            // 2. Create Prefabs
            GameObject localPrefab = CreatePlayerPrefab(true, localMat, visorMat);
            GameObject remotePrefab = CreatePlayerPrefab(false, remoteMat, visorMat);

            // 3. Setup NetManager Scene
            BuildSceneHierarchy(groundMat, pillarMat, localPrefab, remotePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SmartSpace] NetManager Scene and Prefabs setup successfully!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(MaterialsPath)) Directory.CreateDirectory(MaterialsPath);
            if (!Directory.Exists(PrefabsPath)) Directory.CreateDirectory(PrefabsPath);
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Diffuse");

                mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static GameObject CreatePlayerPrefab(bool isLocal, Material bodyMat, Material visorMat)
        {
            string prefabName = isLocal ? "LocalPlayer" : "RemotePlayer";
            string path = $"{PrefabsPath}/{prefabName}.prefab";

            GameObject root = new GameObject(prefabName);

            // Visual Root (for animation bobbing/tilting)
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // Body Mesh (Capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(body.GetComponent<Collider>()); // Let root controller handle collision

            // Visor Mesh (Cube showing front direction)
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            visor.transform.SetParent(visual.transform, false);
            visor.transform.localPosition = new Vector3(0, 1.45f, 0.38f);
            visor.transform.localScale = new Vector3(0.55f, 0.22f, 0.3f);
            visor.GetComponent<Renderer>().sharedMaterial = visorMat;
            Object.DestroyImmediate(visor.GetComponent<Collider>());

            // Overhead UI
            GameObject uiObj = new GameObject("OverheadUI");
            uiObj.transform.SetParent(root.transform, false);
            uiObj.transform.localPosition = new Vector3(0, 2.3f, 0);
            var overheadUI = uiObj.AddComponent<PlayerOverheadUI>();

            // Name Text (TextMeshPro 3D)
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(uiObj.transform, false);
            nameObj.transform.localPosition = Vector3.zero;
            var nameTmp = nameObj.AddComponent<TextMeshPro>();
            nameTmp.fontSize = 3.2f;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.text = isLocal ? "[YOU]" : "Guest";
            nameTmp.rectTransform.sizeDelta = new Vector2(5f, 0.8f);

            // Chat Bubble Root
            GameObject bubbleRoot = new GameObject("ChatBubble");
            bubbleRoot.transform.SetParent(uiObj.transform, false);
            bubbleRoot.transform.localPosition = new Vector3(0, 0.5f, 0);

            // Chat Text
            GameObject chatTextObj = new GameObject("ChatText");
            chatTextObj.transform.SetParent(bubbleRoot.transform, false);
            var chatTmp = chatTextObj.AddComponent<TextMeshPro>();
            chatTmp.fontSize = 2.8f;
            chatTmp.alignment = TextAlignmentOptions.Center;
            chatTmp.color = Color.white;
            chatTmp.text = "...";
            chatTmp.rectTransform.sizeDelta = new Vector2(6f, 1.2f);

            // Wire OverheadUI references via SerializedObject
            SerializedObject soUI = new SerializedObject(overheadUI);
            soUI.FindProperty("nameText").objectReferenceValue = nameTmp;
            soUI.FindProperty("chatBubbleText").objectReferenceValue = chatTmp;
            soUI.FindProperty("chatBubbleRoot").objectReferenceValue = bubbleRoot;
            soUI.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<EmoteEffects>();

            if (isLocal)
            {
                var cc = root.AddComponent<CharacterController>();
                cc.center = new Vector3(0, 1f, 0);
                cc.height = 2f;
                cc.radius = 0.5f;

                var controller = root.AddComponent<LocalPlayerController>();
                SerializedObject soCtrl = new SerializedObject(controller);
                soCtrl.FindProperty("overheadUI").objectReferenceValue = overheadUI;
                soCtrl.FindProperty("visualTransform").objectReferenceValue = visual.transform;
                soCtrl.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var remoteCtrl = root.AddComponent<RemotePlayerController>();
                SerializedObject soRemote = new SerializedObject(remoteCtrl);
                soRemote.FindProperty("overheadUI").objectReferenceValue = overheadUI;
                soRemote.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                soRemote.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildSceneHierarchy(Material groundMat, Material pillarMat, GameObject localPrefab, GameObject remotePrefab)
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            // 1. Setup Main Camera with ThirdPersonCamera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var tpCam = mainCam.gameObject.GetComponent<ThirdPersonCamera>();
                if (tpCam == null)
                {
                    tpCam = mainCam.gameObject.AddComponent<ThirdPersonCamera>();
                }
                mainCam.transform.position = new Vector3(0, 4f, -7f);
                mainCam.transform.LookAt(new Vector3(0, 1f, 0));
            }

            // 2. Setup Plaza Environment Ground Platform
            GameObject envRoot = GameObject.Find("Environment");
            if (envRoot == null)
            {
                envRoot = new GameObject("Environment");
            }

            // Ground Plaza Floor
            Transform groundTr = envRoot.transform.Find("PlazaFloor");
            GameObject ground;
            if (groundTr == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "PlazaFloor";
                ground.transform.SetParent(envRoot.transform, false);
            }
            else
            {
                ground = groundTr.gameObject;
            }
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(50f, 1f, 50f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // Decorative Corner Pillars
            Vector3[] pillarPositions = new Vector3[]
            {
                new Vector3(-20f, 2.5f, -20f),
                new Vector3(20f, 2.5f, -20f),
                new Vector3(-20f, 2.5f, 20f),
                new Vector3(20f, 2.5f, 20f),
                new Vector3(0f, 1f, 15f), // Central Stage / Kiosk block
                new Vector3(-10f, 0.5f, 5f)  // Step block
            };

            for (int i = 0; i < pillarPositions.Length; i++)
            {
                string pName = $"Plaza_Prop_{i}";
                Transform propTr = envRoot.transform.Find(pName);
                GameObject prop = propTr != null ? propTr.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = pName;
                prop.transform.SetParent(envRoot.transform, false);
                prop.transform.position = pillarPositions[i];
                prop.transform.localScale = i == 4 ? new Vector3(8f, 2f, 4f) : (i == 5 ? new Vector3(4f, 1f, 4f) : new Vector3(2f, 5f, 2f));
                prop.GetComponent<Renderer>().sharedMaterial = pillarMat;
            }

            // 3. NetworkManager GameObject
            GameObject netManagerObj = GameObject.Find("NetworkManager");
            if (netManagerObj == null)
            {
                netManagerObj = new GameObject("NetworkManager");
            }

            var netManager = netManagerObj.GetComponent<NetworkManager>();
            if (netManager == null) netManager = netManagerObj.AddComponent<NetworkManager>();

            var demoHUD = netManagerObj.GetComponent<DemoHUD>();
            if (demoHUD == null) demoHUD = netManagerObj.AddComponent<DemoHUD>();

            var emoteWheel = netManagerObj.GetComponent<EmoteWheelUI>();
            if (emoteWheel == null) emoteWheel = netManagerObj.AddComponent<EmoteWheelUI>();

            // Spawn Point
            GameObject spawnPointObj = GameObject.Find("SpawnPoint");
            if (spawnPointObj == null)
            {
                spawnPointObj = new GameObject("SpawnPoint");
                spawnPointObj.transform.position = new Vector3(0, 0.1f, 0);
            }

            // Assign prefabs to NetworkManager
            SerializedObject soNet = new SerializedObject(netManager);
            soNet.FindProperty("localPlayerPrefab").objectReferenceValue = localPrefab;
            soNet.FindProperty("remotePlayerPrefab").objectReferenceValue = remotePrefab;
            soNet.FindProperty("spawnPoint").objectReferenceValue = spawnPointObj.transform;
            soNet.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
    }
}
