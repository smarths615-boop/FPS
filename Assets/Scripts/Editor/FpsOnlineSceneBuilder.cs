#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FpsOnlineSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/FPSOnline.unity";
    private const string PrefabPath = "Assets/Prefabs/FPS/NetworkSWATPlayer.prefab";

    [MenuItem("FPS/Create Relay 1v1 Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameObject[] primary = FpsLowPolyDemoSceneBuilder.LoadPrefabs(new[]
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
        GameObject[] secondary = FpsLowPolyDemoSceneBuilder.LoadPrefabs(new[]
        {
            "Assets/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons/Pistol_P.prefab",
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Weapons/Pistol_F.prefab"
        });
        GameObject swat = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Low Poly Character Series 1_SWAT/Prefabs/Characters/Character_SWAT_A_1.prefab");
        GameObject networkPrefab = CreatePlayerPrefab(swat, primary, secondary);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        FpsArenaBuilder.Build(null, FpsLowPolyDemoSceneBuilder.GetPalette());
        GameObject managerObject = new GameObject("Relay 1v1 Network Manager");
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = networkPrefab;
        manager.NetworkConfig.ConnectionApproval = true;
        managerObject.AddComponent<FpsRelayMenu>();
        CreateLighting();
        CreateLobbyCamera();
        EditorSceneManager.SaveScene(scene, ScenePath);
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(item => item.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Relay 1v1 scene ready. Link Unity Services, press Play, then Host room or Join using a code.");
    }

    private static GameObject CreatePlayerPrefab(GameObject swat, GameObject[] primary, GameObject[] secondary)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/FPS")) AssetDatabase.CreateFolder("Assets/Prefabs", "FPS");
        GameObject root = new GameObject("Network SWAT Player");
        root.layer = LayerMask.NameToLayer("Ignore Raycast");
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.6f;
        root.AddComponent<NetworkObject>();
        root.AddComponent<FpsOwnerNetworkTransform>();
        FpsCombatController combat = root.AddComponent<FpsCombatController>();
        combat.enabled = false;
        FpsPlayerMotor motor = root.AddComponent<FpsPlayerMotor>();
        motor.enabled = false;
        root.AddComponent<FpsNetworkPlayer>().Configure(primary, secondary);

        if (swat != null)
        {
            GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(swat);
            avatar.name = "SWAT Body";
            avatar.transform.SetParent(root.transform, false);
            foreach (Collider collider in avatar.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            avatar.AddComponent<FpsCharacterMotion>();
        }
        CreateHitbox(root.transform, "Leg hitbox", HitZone.Leg, 0.7f,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 0.4f));
        CreateHitbox(root.transform, "Body hitbox", HitZone.Body, 1f,
            new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.8f, 0.45f));
        CreateHitbox(root.transform, "Head hitbox", HitZone.Head, 2.4f,
            new Vector3(0f, 1.95f, 0f), new Vector3(0.43f, 0.42f, 0.43f));

        Transform anchor = FpsLowPolyDemoSceneBuilder.CreateWorldWeaponAnchor(root.transform);
        GameObject[] primaryModels = new GameObject[primary.Length];
        GameObject[] secondaryModels = new GameObject[secondary.Length];
        for (int i = 0; i < primary.Length; i++)
            primaryModels[i] = FpsLowPolyDemoSceneBuilder.CreateWorldWeapon(anchor, primary[i]);
        for (int i = 0; i < secondary.Length; i++)
            secondaryModels[i] = FpsLowPolyDemoSceneBuilder.CreateWorldWeapon(anchor, secondary[i]);
        root.AddComponent<FpsWorldWeaponRig>().Configure(primaryModels, secondaryModels);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void CreateHitbox(Transform parent, string name, HitZone zone, float multiplier, Vector3 position, Vector3 scale)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().enabled = false;
        box.AddComponent<FpsNetworkHitbox>().Initialize(zone, multiplier);
    }

    private static void CreateLighting()
    {
        GameObject sun = new GameObject("Arena Sun");
        sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
    }

    private static void CreateLobbyCamera()
    {
        GameObject cameraObject = new GameObject("Arena Lobby Camera");
        cameraObject.transform.position = new Vector3(0f, 53f, -20f);
        cameraObject.transform.LookAt(Vector3.zero);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 22f;
        cameraObject.AddComponent<AudioListener>();
    }
}
#endif
