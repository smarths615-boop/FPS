#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FpsLowPolyDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/FPSPrototype.unity";
    private const string MaterialFolder = "Assets/Materials/FPSArena";

    [MenuItem("FPS/Create Blue Red Arena Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureLowPolyMaterialsUseUrp();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject bootstrap = new GameObject("FPS Prototype Bootstrap");
        GameObject[] primaryPrefabs = LoadPrefabs(new[]
        {
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/AR_T.prefab",
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/AR_U.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/AR_W_Black.prefab",
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/SMG_P.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/SMG_Q.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/ShotGun_P.prefab",
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/Recon_P.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/Recon_M.prefab"
        });
        GameObject[] secondaryPrefabs = LoadPrefabs(new[]
        {
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/Pistol_P.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/Pistol_F.prefab"
        });
        string swatPath = "Assets/Low Poly Character Series 1_SWAT/Prefabs/Characters/Character_SWAT_A_1.prefab";
        GameObject swatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swatPath);
        bootstrap.AddComponent<FpsBootstrap>().ConfigureAssets(primaryPrefabs, secondaryPrefabs, swatPrefab);
        FpsArenaBuilder.Build(bootstrap.transform, GetPalette());
        CreateBluePlayer(bootstrap.transform, swatPrefab, primaryPrefabs, secondaryPrefabs);
        CreateSwatOpponent(bootstrap.transform);
        CreateLight();
        CreatePreviewCamera();

        EditorSceneManager.SaveScene(scene, ScenePath);
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(item => item.path == ScenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("Blue/Red low-poly FPS arena created and added to Build Settings. Press Play to test movement and weapons.");
    }

    private static void EnsureLowPolyMaterialsUseUrp()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found. Low-poly materials could not be converted.");
            return;
        }

        string[] assetFolders =
        {
            "Assets/Low Poly Character Series 1_SWAT/Materials",
            "Assets/Low Poly Weapon Pack 4_MW_1/Materials"
        };
        foreach (string guid in AssetDatabase.FindAssets("t:Material", assetFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == urpLit) continue;
            Texture texture = material.mainTexture;
            Color color = material.color;
            material.shader = urpLit;
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }

    internal static GameObject[] LoadPrefabs(string[] paths)
    {
        GameObject[] prefabs = new GameObject[paths.Length];
        for (int index = 0; index < paths.Length; index++)
        {
            prefabs[index] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]);
            if (prefabs[index] == null) Debug.LogWarning("Missing low-poly weapon: " + paths[index]);
        }
        return prefabs;
    }

    private static void CreateBluePlayer(Transform bootstrap, GameObject swatPrefab, GameObject[] primary, GameObject[] secondary)
    {
        GameObject player = new GameObject("Blue Player");
        player.transform.SetParent(bootstrap, false);
        player.transform.localPosition = new Vector3(-26f, 0.02f, 0f);
        player.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        if (swatPrefab != null)
        {
            GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(swatPrefab);
            avatar.name = "Blue SWAT Body";
            avatar.transform.SetParent(player.transform, false);
            foreach (Collider collider in avatar.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            avatar.AddComponent<FpsCharacterMotion>();
        }
        Transform anchor = CreateWorldWeaponAnchor(player.transform);
        GameObject[] primaryModels = new GameObject[primary.Length];
        GameObject[] secondaryModels = new GameObject[secondary.Length];
        for (int i = 0; i < primary.Length; i++) primaryModels[i] = CreateWorldWeapon(anchor, primary[i]);
        for (int i = 0; i < secondary.Length; i++) secondaryModels[i] = CreateWorldWeapon(anchor, secondary[i]);
        player.AddComponent<FpsWorldWeaponRig>().Configure(primaryModels, secondaryModels);
    }

    internal static Transform CreateWorldWeaponAnchor(Transform parent)
    {
        GameObject anchor = new GameObject("World Weapon Anchor");
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = new Vector3(0.14f, 1.37f, 0.47f);
        return anchor.transform;
    }

    internal static GameObject CreateWorldWeapon(Transform anchor, GameObject prefab)
    {
        if (prefab == null) return null;
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = prefab.name + " Held";
        model.transform.SetParent(anchor, false);
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (length > 0.001f) model.transform.localScale *= 0.85f / length;
        }
        return model;
    }

    private static void CreateSwatOpponent(Transform bootstrap)
    {
        GameObject targets = new GameObject("SWAT Targets");
        targets.transform.SetParent(bootstrap, false);
        string path = "Assets/Low Poly Character Series 1_SWAT/Prefabs/Characters/Character_SWAT_A_2.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;
        GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        target.name = "Red SWAT Opponent";
        target.transform.SetParent(targets.transform);
        target.transform.localPosition = new Vector3(27f, 0f, 0f);
        target.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        Damageable damageable = target.AddComponent<Damageable>();
        CharacterController controller = target.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.6f;
        target.AddComponent<FpsCharacterMotion>();
        target.AddComponent<FpsEnemyBot>();
        Transform weaponAnchor = CreateWorldWeaponAnchor(target.transform);
        GameObject redRifle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/AR_W_Black.prefab");
        CreateWorldWeapon(weaponAnchor, redRifle);
        TargetHitbox(target.transform, damageable, "Leg hitbox", HitZone.Leg, 0.7f, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 0.40f));
        TargetHitbox(target.transform, damageable, "Body hitbox", HitZone.Body, 1f, new Vector3(0f, 1.30f, 0f), new Vector3(0.70f, 0.8f, 0.45f));
        TargetHitbox(target.transform, damageable, "Head hitbox", HitZone.Head, 2.4f, new Vector3(0f, 1.95f, 0f), new Vector3(0.43f, 0.42f, 0.43f));
    }

    private static void TargetHitbox(Transform parent, Damageable owner, string name, HitZone zone, float multiplier, Vector3 position, Vector3 scale)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().enabled = false;
        box.AddComponent<Hitbox>().Initialize(owner, zone, multiplier);
    }

    internal static Material[] GetPalette()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "FPSArena");

        Material[] palette = new Material[9];
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        for (int index = 0; index < palette.Length; index++)
        {
            FpsArenaBuilder.Tone tone = (FpsArenaBuilder.Tone)index;
            string path = MaterialFolder + "/" + tone + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = tone + " Arena Material" };
                material.color = FpsArenaBuilder.GetColor(tone);
                AssetDatabase.CreateAsset(material, path);
            }
            palette[index] = material;
        }
        AssetDatabase.SaveAssets();
        return palette;
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Arena Sun");
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        Light sunlight = lightObject.AddComponent<Light>();
        sunlight.type = LightType.Directional;
        sunlight.intensity = 1.35f;
        sunlight.color = new Color(1f, 0.95f, 0.84f);
    }

    private static void CreatePreviewCamera()
    {
        GameObject cameraObject = new GameObject("Arena Preview Camera");
        cameraObject.transform.position = new Vector3(0f, 53f, -20f);
        cameraObject.transform.LookAt(Vector3.zero);
        Camera preview = cameraObject.AddComponent<Camera>();
        preview.orthographic = true;
        preview.orthographicSize = 22f;
        preview.clearFlags = CameraClearFlags.SolidColor;
        preview.backgroundColor = new Color(0.10f, 0.13f, 0.18f);
    }
}
#endif
