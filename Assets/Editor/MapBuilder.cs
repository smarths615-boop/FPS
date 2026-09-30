using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPS.EditorTools
{
    /// <summary>
    /// Builds a long, horizontal tactical map in the spirit of Valorant's Bind/Haven:
    /// a west attacker spawn feeding into a split (A Long to the north, a lower route
    /// to the south), a contested centre "Mid" with hard cover, and two elevated bomb
    /// sites at the east end.
    ///
    /// Run via menu: FPS/Build Map
    /// </summary>
    public static class MapBuilder
    {
        private const string Root = "MAP";
        private const string Geo = "MAP/Geometry";
        private const string Sites = "MAP/Sites";
        private const string Spawns = "MAP/Spawns";
        private const string Deco = "MAP/Decoration";
        private const string Mats = "Assets/Materials";

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        // map extents
        private const float HalfLength = 66f;   // X
        private const float HalfWidth = 21f;    // Z
        private const float WallH = 7f;

        [MenuItem("FPS/Build Map")]
        public static void Build()
        {
            EnsureFolders();
            var mats = BuildMaterials();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            BuildLighting();
            BuildGeometry(mats);
            BuildSites(mats);
            BuildCover(mats);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/FPS_Map.unity");

            Debug.Log("[FPS] Map built and saved to Assets/Scenes/FPS_Map.unity");
        }

        // ------------------------------------------------------------------ materials

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Mats)) AssetDatabase.CreateFolder("Assets", "Materials");
        }

        private static Material GetMat(Dictionary<string, Material> cache, string name, Color c, float metallic, float smoothness)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = Mats + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(FindUrpLit());
                m.SetColor("_BaseColor", c);
                m.SetColor("_Color", c);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(m, path);
            }
            cache[name] = m;
            return m;
        }

        private static Shader FindUrpLit()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            return new Dictionary<string, Material>
            {
                { "floor",  GetMat(MatCache, "M_Floor",  new Color(0.42f, 0.43f, 0.46f), 0f, 0.18f) },
                { "wall",   GetMat(MatCache, "M_Wall",   new Color(0.72f, 0.70f, 0.66f), 0f, 0.10f) },
                { "cover",  GetMat(MatCache, "M_Cover",  new Color(0.55f, 0.40f, 0.26f), 0f, 0.12f) },
                { "dark",   GetMat(MatCache, "M_Dark",   new Color(0.20f, 0.21f, 0.24f), 0.1f, 0.30f) },
                { "siteA",  GetMat(MatCache, "M_SiteA",  new Color(0.78f, 0.24f, 0.18f), 0f, 0.20f) },
                { "siteB",  GetMat(MatCache, "M_SiteB",  new Color(0.18f, 0.42f, 0.78f), 0f, 0.20f) },
                { "accent", GetMat(MatCache, "M_Accent", new Color(0.85f, 0.62f, 0.16f), 0.2f, 0.45f) },
            };
        }

        // ------------------------------------------------------------------ lighting

        private static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(48f, 35f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            RenderSettings.sun = l;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.52f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.34f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.17f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.60f, 0.68f);
            // Unity's exponential fog is quadratic in distance, so 0.0075 saturates by
            // ~55m and hides the whole map. Keep it light for long sightlines.
            RenderSettings.fogDensity = 0.0016f;
        }

        // ------------------------------------------------------------------ geometry

        private static void BuildGeometry(Dictionary<string, Material> mats)
        {
            var geo = new GameObject(Geo);

            // ---- floor ----
            Box("Floor", geo.transform, new Vector3(0f, -0.5f, 0f), new Vector3(HalfLength * 2f, 1f, HalfWidth * 2f), mats["floor"]);

            // ---- outer walls ----
            Box("Wall_North", geo.transform, new Vector3(0f, WallH * 0.5f, HalfWidth), new Vector3(HalfLength * 2f, WallH, 0.8f), mats["wall"]);
            Box("Wall_South", geo.transform, new Vector3(0f, WallH * 0.5f, -HalfWidth), new Vector3(HalfLength * 2f, WallH, 0.8f), mats["wall"]);
            Box("Wall_West", geo.transform, new Vector3(-HalfLength, WallH * 0.5f, 0f), new Vector3(0.8f, WallH, HalfWidth * 2f), mats["wall"]);
            Box("Wall_East", geo.transform, new Vector3(HalfLength, WallH * 0.5f, 0f), new Vector3(0.8f, WallH, HalfWidth * 2f), mats["wall"]);

            // ---- A Long: a long north lane walled off from mid ----
            // inner wall running east-west at z = +9, from x=-30 to x=+14, leaving the
            // lane open at the west entrance and the site at the east.
            Box("ALong_InnerWall", geo.transform, new Vector3(-8f, 1.9f, 9f), new Vector3(44f, 3.8f, 0.6f), mats["wall"]);
            Box("ALong_Ceiling", geo.transform, new Vector3(-8f, 4.1f, 15f), new Vector3(44f, 0.5f, 12f), mats["dark"]);

            // mid divider with a gap (the "boathouse" block)
            Box("Mid_Divider_W", geo.transform, new Vector3(-6f, 1.7f, 0.6f), new Vector3(0.6f, 3.4f, 16f), mats["wall"]);
            Box("Mid_Divider_E", geo.transform, new Vector3(14f, 1.7f, -0.4f), new Vector3(0.6f, 3.4f, 18f), mats["wall"]);

            // south lower-route wall, keeping B route distinct from mid
            Box("BRoute_InnerWall", geo.transform, new Vector3(-14f, 1.5f, -11f), new Vector3(38f, 3f, 0.6f), mats["wall"]);

            // ---- A Long connector wall creating a bend ----
            Box("ALong_Bend", geo.transform, new Vector3(-24f, 2.1f, 12f), new Vector3(0.6f, 4.2f, 9f), mats["wall"]);

            // ---- arch / doorway frames for readability ----
            Frame("Arch_A_Long", geo.transform, new Vector3(-30f, 0f, 14f), 3.2f, 3.6f, mats["accent"]);
            Frame("Arch_Mid", geo.transform, new Vector3(4f, 0f, 0f), 3.4f, 3.8f, mats["accent"]);
            Frame("Arch_B", geo.transform, new Vector3(4f, 0f, -12f), 3.0f, 3.4f, mats["accent"]);
        }

        private static void Frame(string name, Transform parent, Vector3 pos, float w, float h, Material mat)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.position = pos;
            Box(name + "_L", t, new Vector3(0f, h * 0.5f, -(w * 0.5f + 0.3f)), new Vector3(0.6f, h, 0.6f), mat);
            Box(name + "_R", t, new Vector3(0f, h * 0.5f, (w * 0.5f + 0.3f)), new Vector3(0.6f, h, 0.6f), mat);
            Box(name + "_T", t, new Vector3(0f, h + 0.3f, 0f), new Vector3(0.6f, 0.6f, w + 1.2f), mat);
        }

        // ------------------------------------------------------------------ sites

        private static void BuildSites(Dictionary<string, Material> mats)
        {
            var sites = new GameObject(Sites);

            // ---- Site A: raised platform in the north-east ----
            Box("SiteA_Platform", sites.transform, new Vector3(34f, 0.75f, 13.5f), new Vector3(26f, 1.5f, 13f), mats["siteA"]);
            // ramp up to A
            var ramp = Box("SiteA_Ramp", sites.transform, new Vector3(20f, 0.5f, 13.5f), new Vector3(9f, 1.0f, 7f), mats["siteA"]);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -9f);

            Marker("SiteA_Center", sites.transform, new Vector3(38f, 1.6f, 13.5f));
            Marker("SiteA_Plant", sites.transform, new Vector3(40f, 1.6f, 10.5f));
            Marker("SiteA_Defend", sites.transform, new Vector3(28f, 1.6f, 16f));

            // ---- Site B: ground level in the south-east ----
            Box("SiteB_Floor", sites.transform, new Vector3(34f, 0.12f, -12.5f), new Vector3(26f, 0.24f, 15f), mats["siteB"]);
            // half-height walls for cover inside B
            Box("SiteB_Wall_N", sites.transform, new Vector3(34f, 0.9f, -5.5f), new Vector3(26f, 1.8f, 0.6f), mats["siteB"]);
            Box("SiteB_Pillar_W", sites.transform, new Vector3(26f, 1.7f, -12.5f), new Vector3(0.9f, 3.4f, 15f), mats["siteB"]);
            Box("SiteB_Pillar_E", sites.transform, new Vector3(44f, 1.7f, -12.5f), new Vector3(0.9f, 3.4f, 15f), mats["siteB"]);

            Marker("SiteB_Center", sites.transform, new Vector3(35f, 0.3f, -12.5f));
            Marker("SiteB_Plant", sites.transform, new Vector3(39f, 0.3f, -15f));
            Marker("SiteB_Defend", sites.transform, new Vector3(30f, 0.3f, -9f));
        }

        private static void Marker(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name);
            t.transform.SetParent(parent);
            t.transform.position = pos;
        }

        // ------------------------------------------------------------------ cover

        private static void BuildCover(Dictionary<string, Material> mats)
        {
            var deco = new GameObject(Deco);

            // --- attacker spawn (west) ---
            Crate(deco.transform, "AS_Crate_1", new Vector3(-58f, 1f, -8f), new Vector3(2f, 2f, 2f), mats["cover"]);
            Crate(deco.transform, "AS_Crate_2", new Vector3(-58f, 1f, -4.5f), new Vector3(2f, 2f, 2f), mats["cover"]);
            Crate(deco.transform, "AS_Crate_3", new Vector3(-53f, 0.6f, 8f), new Vector3(2.4f, 1.2f, 2.4f), mats["cover"]);
            Box("AS_Platform", deco.transform, new Vector3(-50f, 0.5f, -14f), new Vector3(14f, 1f, 6f), mats["dark"]);

            // --- A long: staggered cover down the lane ---
            Crate(deco.transform, "AL_Crate_1", new Vector3(-24f, 0.75f, 12.5f), new Vector3(1.5f, 1.5f, 1.5f), mats["cover"]);
            Crate(deco.transform, "AL_Crate_2", new Vector3(-14f, 1f, 17f), new Vector3(2f, 2f, 2f), mats["cover"]);
            Crate(deco.transform, "AL_Crate_3", new Vector3(-14f, 0.6f, 13.5f), new Vector3(1.2f, 1.2f, 1.2f), mats["cover"]);
            Crate(deco.transform, "AL_Crate_4", new Vector3(-2f, 0.9f, 16.5f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Crate(deco.transform, "AL_Crate_5", new Vector3(6f, 0.75f, 12f), new Vector3(1.5f, 1.5f, 1.5f), mats["cover"]);
            Box("AL_LongBench", deco.transform, new Vector3(-20f, 0.4f, 19f), new Vector3(12f, 0.8f, 2.4f), mats["dark"]);

            // --- mid: hard cover pillars and a raised overlook ---
            Box("Mid_Pillar_1", deco.transform, new Vector3(-12f, 1.8f, 2.5f), new Vector3(1.4f, 3.6f, 1.4f), mats["cover"]);
            Box("Mid_Pillar_2", deco.transform, new Vector3(0f, 1.8f, -3f), new Vector3(1.4f, 3.6f, 1.4f), mats["cover"]);
            Box("Mid_Pillar_3", deco.transform, new Vector3(9f, 1.8f, 2f), new Vector3(1.4f, 3.6f, 1.4f), mats["cover"]);
            Crate(deco.transform, "Mid_Crate_1", new Vector3(-6f, 0.7f, 5.5f), new Vector3(1.4f, 1.4f, 1.4f), mats["cover"]);
            Crate(deco.transform, "Mid_Crate_2", new Vector3(5f, 0.7f, -6f), new Vector3(1.4f, 1.4f, 1.4f), mats["cover"]);
            Box("Mid_Overlook", deco.transform, new Vector3(3f, 1.6f, 5.5f), new Vector3(9f, 0.4f, 4f), mats["dark"]);
            Box("Mid_Overlook_Leg_W", deco.transform, new Vector3(-0.5f, 0.8f, 5.5f), new Vector3(0.5f, 1.6f, 4f), mats["dark"]);
            Box("Mid_Overlook_Leg_E", deco.transform, new Vector3(6.5f, 0.8f, 5.5f), new Vector3(0.5f, 1.6f, 4f), mats["dark"]);
            var midRamp = Box("Mid_Ramp", deco.transform, new Vector3(-4f, 0.7f, 5.5f), new Vector3(4f, 0.3f, 4f), mats["dark"]);
            midRamp.transform.rotation = Quaternion.Euler(0f, 0f, 16f);

            // --- B route / south lane ---
            Crate(deco.transform, "BR_Crate_1", new Vector3(-30f, 0.9f, -15f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Crate(deco.transform, "BR_Crate_2", new Vector3(-18f, 0.9f, -16.5f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Crate(deco.transform, "BR_Crate_3", new Vector3(-8f, 0.6f, -14f), new Vector3(1.2f, 1.2f, 1.2f), mats["cover"]);
            Box("BR_Pillar_1", deco.transform, new Vector3(2f, 1.6f, -16f), new Vector3(1.2f, 3.2f, 1.2f), mats["cover"]);
            Box("BR_Pillar_2", deco.transform, new Vector3(12f, 1.6f, -14.5f), new Vector3(1.2f, 3.2f, 1.2f), mats["cover"]);

            // --- sites cover (parented under the Sites group created in BuildSites) ---
            var sitesGo = GameObject.Find(Sites);
            Transform sites = sitesGo != null ? sitesGo.transform : deco.transform;

            Crate(sites, "A_Crate_1", new Vector3(31f, 2.2f, 11f), new Vector3(1.6f, 1.6f, 1.6f), mats["cover"]);
            Crate(sites, "A_Crate_2", new Vector3(31f, 2.2f, 16f), new Vector3(1.6f, 1.6f, 1.6f), mats["cover"]);
            Crate(sites, "A_Crate_3", new Vector3(44f, 2.2f, 15.5f), new Vector3(1.6f, 1.6f, 1.6f), mats["cover"]);
            Box("A_Bench", sites, new Vector3(38f, 1.95f, 18.5f), new Vector3(10f, 0.9f, 2f), mats["dark"]);

            Crate(sites, "B_Crate_1", new Vector3(30f, 0.9f, -10f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Crate(sites, "B_Crate_2", new Vector3(30f, 0.9f, -16f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Crate(sites, "B_Crate_3", new Vector3(40f, 0.9f, -10f), new Vector3(1.8f, 1.8f, 1.8f), mats["cover"]);
            Box("B_Bench", sites, new Vector3(35f, 0.65f, -19f), new Vector3(10f, 0.9f, 2f), mats["dark"]);

            // --- defender spawn (east) ---
            Crate(deco.transform, "DS_Crate_1", new Vector3(56f, 1f, 4f), new Vector3(2f, 2f, 2f), mats["cover"]);
            Crate(deco.transform, "DS_Crate_2", new Vector3(56f, 1f, 7.5f), new Vector3(2f, 2f, 2f), mats["cover"]);
            Box("DS_Platform", deco.transform, new Vector3(58f, 0.5f, -4f), new Vector3(10f, 1f, 8f), mats["dark"]);

            BuildSpawns();
        }

        // ------------------------------------------------------------------ spawns

        private static void BuildSpawns()
        {
            var spawns = new GameObject(Spawns);

            // player start - west end, facing east
            Marker("PlayerStart", spawns.transform, new Vector3(-62f, 0.2f, 0f));

            // player start - east end (so you can practise defending too)
            Marker("PlayerStart_Defend", spawns.transform, new Vector3(62f, 0.2f, 0f));

            // enemy spawns spread across the eastern half, behind cover and on both sites
            var enemyPoints = new[]
            {
                new Vector3( 52f, 0.2f,  12f),
                new Vector3( 52f, 0.2f,  -6f),
                new Vector3( 46f, 0.2f,  18f),
                new Vector3( 46f, 0.2f, -18f),
                new Vector3( 40f, 1.7f,  13f),   // on A platform
                new Vector3( 36f, 0.3f, -13f),   // in B
                new Vector3( 58f, 0.2f,   0f),
                new Vector3( 30f, 0.3f,   0f),   // mid approach
            };
            for (int i = 0; i < enemyPoints.Length; i++)
                Marker("EnemySpawn_" + (i + 1), spawns.transform, enemyPoints[i]);
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.localScale = size;
            var col = go.GetComponent<BoxCollider>();
            if (col != null) col.size = Vector3.one; // scale is on the transform
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static GameObject Crate(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.localScale = size;
            go.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-12f, 12f), 0f);
            var col = go.GetComponent<BoxCollider>();
            if (col != null) col.size = Vector3.one;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }
    }
}
