using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace FPS.EditorTools
{
    /// <summary>
    /// One-shot project assembly: converts the asset-pack materials to URP, bakes the
    /// NavMesh, then builds the Player and Enemy prefabs from the low-poly SWAT rig and
    /// drops them into the map scene.
    ///
    /// Run via menu: FPS/Setup Game
    /// </summary>
    public static class SetupWizard
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        private const string MapScenePath = "Assets/Scenes/FPS_Map.unity";

        // asset-pack prefabs
        private const string CharPrefab = "Assets/Asset/Low Poly Character Series 1_SWAT/Prefabs/Characters/Character_SWAT_A_1.prefab";
        private const string CharPrefabAlt = "Assets/Asset/Low Poly Character Series 1_SWAT/Prefabs/Characters/Character_SWAT_A_2.prefab";
        private const string RiflePrefab = "Assets/Asset/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/AR_T.prefab";
        private const string SniperPrefab = "Assets/Asset/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/Recon_P.prefab";
        private const string PistolPrefab = "Assets/Asset/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/Pistol_P.prefab";
        private const string KnifePrefab = "Assets/Asset/WeaponsPack/prefabs/knifeModelPrefab.prefab";

        [MenuItem("FPS/Setup Game")]
        public static void Run()
        {
            EnsureFolder("Assets/Prefabs");

            int converted = ConvertMaterialsToUrp();
            Debug.Log("[FPS] Converted " + converted + " material(s) to URP/Lit.");

            var player = BuildPlayerPrefab();
            var enemy = BuildEnemyPrefab();
            Debug.Log("[FPS] Prefabs built: player=" + (player != null) + " enemy=" + (enemy != null));

            BakeNavMesh();
            PopulateScene(player, enemy);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FPS] Setup complete.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", leaf);
            else AssetDatabase.CreateFolder(parent, leaf);
        }

        // ------------------------------------------------------------------ URP materials

        /// <summary>
        /// The asset packs ship Built-in pipeline materials, which render magenta in URP.
        /// Re-point them at URP/Lit and move the albedo texture to _BaseMap.
        /// </summary>
        private static int ConvertMaterialsToUrp()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) { Debug.LogWarning("[FPS] URP/Lit shader not found; skipping material conversion."); return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Asset", "Assets/WeaponsPack", "Assets/Low Poly Character Series 1_SWAT", "Assets/Low Poly Weapon Pack 4_MW_1" });
            int n = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                if (m.shader == urpLit) continue;

                var old = m.shader;
                Texture albedo = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color baseColor = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                float smoothness = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.2f;

                // capture emission before the shader swap (Shader has no HasProperty)
                Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;

                m.shader = urpLit;
                if (albedo != null)
                {
                    m.SetTexture("_BaseMap", albedo);
                    m.SetTexture("_MainTex", albedo);
                }
                m.SetColor("_BaseColor", baseColor);
                m.SetColor("_Color", baseColor);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Glossiness", smoothness);

                if (emission.maxColorComponent > 0.001f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(m);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject Load(string[] candidates)
        {
            foreach (var p in candidates)
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (a != null) return a;
            }
            return null;
        }

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            root.AddComponent<FPS.Core.Health>().Configure(100f, true);
            root.AddComponent<FPS.Player.PlayerController>();
            var loadout = root.AddComponent<FPS.Player.PlayerLoadout>();
            root.AddComponent<FPS.UI.HUD>();

            // ---- camera ----
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<FPS.Player.PlayerCamera>();

            // URP needs its per-camera data component. Without it the camera clears to a
            // flat colour and draws no geometry at all.
            if (camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            // The local player's own body must not be drawn by the first-person camera,
            // otherwise the rig clips through the near plane. Shadows still render because
            // shadow casting is independent of the camera culling mask.
            int bodyLayer = EnsureLayer("PlayerBody");
            cam.cullingMask &= ~(1 << bodyLayer);

            // ---- third-person visual (the SWAT rig) ----
            var charSrc = Load(new[] { CharPrefab, CharPrefabAlt });
            if (charSrc != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(charSrc);
                vis.name = "Visuals";
                vis.transform.SetParent(root.transform, false);
                // strip colliders so the CharacterController owns collision
                foreach (var col in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                vis.AddComponent<FPS.Player.ProceduralAnimator>();
                SetLayerRecursive(vis, bodyLayer);
                foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            else
            {
                Debug.LogWarning("[FPS] SWAT character prefab not found - player will be invisible.");
            }

            // ---- weapon anchors under the camera ----
            var primaryAnchor = NewAnchor(camGo.transform, "PrimaryAnchor", new Vector3(0.22f, -0.16f, 0.42f));
            var sniperAnchor = NewAnchor(camGo.transform, "SniperAnchor", new Vector3(0.20f, -0.14f, 0.40f));
            var sidearmAnchor = NewAnchor(camGo.transform, "SidearmAnchor", new Vector3(0.20f, -0.18f, 0.36f));
            var meleeAnchor = NewAnchor(camGo.transform, "MeleeAnchor", new Vector3(0.26f, -0.22f, 0.34f));

            // ---- weapon models + muzzles ----
            var rifle = Load(new[] { RiflePrefab });
            var sniper = Load(new[] { SniperPrefab });
            var pistol = Load(new[] { PistolPrefab });
            var knife = Load(new[] { KnifePrefab });

            var primaryModel = AttachModel(primaryAnchor, rifle, "PrimaryModel");
            var sniperModel = AttachModel(sniperAnchor, sniper, "SniperModel");
            var sidearmModel = AttachModel(sidearmAnchor, pistol, "SidearmModel");
            var meleeModel = AttachModel(meleeAnchor, knife, "MeleeModel");

            // ---- scope overlay driven by the sniper weapon ----
            var scope = new GameObject("SniperScope");
            scope.transform.SetParent(camGo.transform, false);

            // ---- wire the loadout ----
            var so = new SerializedObject(loadout);
            so.FindProperty("primaryAnchor").objectReferenceValue = primaryAnchor;
            so.FindProperty("sniperAnchor").objectReferenceValue = sniperAnchor;
            so.FindProperty("sidearmAnchor").objectReferenceValue = sidearmAnchor;
            so.FindProperty("meleeAnchor").objectReferenceValue = meleeAnchor;
            so.FindProperty("primaryModel").objectReferenceValue = primaryModel;
            so.FindProperty("sniperModel").objectReferenceValue = sniperModel;
            so.FindProperty("sidearmModel").objectReferenceValue = sidearmModel;
            so.FindProperty("knifeModel").objectReferenceValue = meleeModel;
            so.FindProperty("sniperScopeOverlay").objectReferenceValue = scope;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("playerCamera").objectReferenceValue = camGo.GetComponent<FPS.Player.PlayerCamera>();
            so.FindProperty("movement").objectReferenceValue = root.GetComponent<FPS.Player.PlayerController>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform NewAnchor(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>Finds the named layer, adding it to the project when missing.</summary>
        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return 0;
            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            // user layers start at 8
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[FPS] Added layer '" + layerName + "' at slot " + i + ".");
                    return i;
                }
            }
            Debug.LogWarning("[FPS] No free user layer slot for '" + layerName + "'.");
            return 0;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        private static GameObject AttachModel(Transform anchor, GameObject prefab, string name)
        {
            if (prefab == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.SetParent(anchor, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // muzzle marker used as the ray origin for firing
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(inst.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);

            return inst;
        }

        private static GameObject BuildEnemyPrefab()
        {
            var root = new GameObject("Enemy");

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 4.6f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 10f;
            agent.angularSpeed = 520f;

            root.AddComponent<FPS.Core.Health>().Configure(100f, false);
            var ai = root.AddComponent<FPS.AI.EnemyAI>();
            root.AddComponent<FPS.Combat.HitboxRig>();

            var charSrc = Load(new[] { CharPrefabAlt, CharPrefab });
            if (charSrc != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(charSrc);
                vis.name = "Visuals";
                vis.transform.SetParent(root.transform, false);
                foreach (var col in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                vis.AddComponent<FPS.Player.ProceduralAnimator>();
            }
            else
            {
                Debug.LogWarning("[FPS] SWAT character prefab not found for the enemy; using a capsule stand-in.");
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "Body";
                cap.transform.SetParent(root.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
            }

            // eye used for line of sight / muzzle
            var eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);

            var so = new SerializedObject(ai);
            so.FindProperty("eye").objectReferenceValue = eye.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ navmesh

        private static void BakeNavMesh()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "FPS_Map")
            {
                if (!EditorSceneManager.OpenScene(MapScenePath).IsValid()) return;
            }

            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                var go = new GameObject("NavMesh");
                surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;
                surface.defaultArea = 0;
            }
            surface.BuildNavMesh();
            Debug.Log("[FPS] NavMesh baked.");
        }

        // ------------------------------------------------------------------ scene

        private static void PopulateScene(GameObject playerPrefab, GameObject enemyPrefab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            // remove previous placements for idempotency
            foreach (var n in new[] { "Player", "Systems" })
            {
                var existing = GameObject.Find(n);
                if (existing != null) Object.DestroyImmediate(existing);
            }

            // ---- player at the west start ----
            if (playerPrefab != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                p.name = "Player";
                var start = GameObject.Find("PlayerStart");
                p.transform.position = start != null ? start.transform.position : new Vector3(-62f, 0.2f, 0f);
                p.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // face east, down the lane
            }

            // ---- systems: spawner + wave director ----
            var systems = new GameObject("Systems");
            var spawner = systems.AddComponent<FPS.AI.EnemySpawner>();
            if (!systems.GetComponent<FPS.Core.GameDirector>()) systems.AddComponent<FPS.Core.GameDirector>();
            var so = new SerializedObject(spawner);
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;

            var points = new List<Object>();
            foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("EnemySpawn_")) points.Add(t);
            }
            var arr = new Object[points.Count];
            for (int i = 0; i < points.Count; i++) arr[i] = points[i];
            so.FindProperty("spawnPoints").arraySize = arr.Length;
            for (int i = 0; i < arr.Length; i++)
                so.FindProperty("spawnPoints").GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FPS] Scene populated with player and spawner (" + points.Count + " spawn points).");
        }
    }
}
