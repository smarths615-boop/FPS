using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FpsBootstrap : MonoBehaviour
{
    private static bool created;
    [SerializeField] private GameObject[] primaryWeaponPrefabs;
    [SerializeField] private GameObject[] secondaryWeaponPrefabs;
    [SerializeField] private GameObject playerCharacterPrefab;

    public void ConfigureAssets(GameObject[] primary, GameObject[] secondary, GameObject character)
    {
        primaryWeaponPrefabs = primary;
        secondaryWeaponPrefabs = secondary;
        playerCharacterPrefab = character;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePrototype()
    {
        if (created || SceneManager.GetActiveScene().name == "FPSOnline" || FindFirstObjectByType<FpsBootstrap>() != null) return;
        new GameObject("FPS Prototype").AddComponent<FpsBootstrap>();
    }

    private void Awake()
    {
        if (created) { Destroy(gameObject); return; }
        created = true;
        Application.targetFrameRate = 120;
        CreateArena();
        CreatePlayer();
    }

    private void CreateArena()
    {
        if (transform.Find("Blue Red Symmetric Arena") == null)
            FpsArenaBuilder.Build(transform);

        if (transform.Find("SWAT Targets") == null)
            for (int i = -2; i <= 2; i++)
                CreateDummy(new Vector3(26f, 0f, i * 3f));
    }

    private void CreatePlayer()
    {
        foreach (Camera existingCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            existingCamera.enabled = false;
        foreach (AudioListener existingListener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            existingListener.enabled = false;

        Transform placedPlayer = transform.Find("Blue Player");
        GameObject player = placedPlayer != null ? placedPlayer.gameObject : new GameObject("Blue Player");
        player.transform.position = new Vector3(-26f, 0.02f, 0f);
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.6f;

        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.03f;
        int bodyLayer = LayerMask.NameToLayer("PlayerBody");
        if (bodyLayer >= 0) camera.cullingMask &= ~(1 << bodyLayer);
        cameraObject.AddComponent<AudioListener>();

        FpsViewRig viewRig = cameraObject.AddComponent<FpsViewRig>();
        viewRig.Build(primaryWeaponPrefabs, secondaryWeaponPrefabs);
        FpsCombatController combat = player.AddComponent<FpsCombatController>();
        combat.Initialize(camera, viewRig);
        FpsPlayerMotor motor = player.AddComponent<FpsPlayerMotor>();
        motor.Initialize(camera, combat);
        combat.SetMotor(motor);
        player.AddComponent<FpsPlayerHealth>();
        Transform placedAvatar = player.transform.Find("Blue SWAT Body");
        GameObject avatar = placedAvatar != null ? placedAvatar.gameObject : null;
        if (avatar == null && playerCharacterPrefab != null)
        {
            avatar = Instantiate(playerCharacterPrefab, player.transform);
            avatar.name = "Blue SWAT Body";
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.identity;
        }
        if (avatar != null)
        {
            foreach (Collider collider in avatar.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            if (bodyLayer >= 0) SetLayerRecursively(avatar.transform, bodyLayer);
            FpsCharacterMotion motion = avatar.GetComponent<FpsCharacterMotion>() ?? avatar.AddComponent<FpsCharacterMotion>();
            motion.Initialize(motor, combat);
            combat.SetCharacterMotion(motion);
        }
        FpsWorldWeaponRig worldRig = player.GetComponent<FpsWorldWeaponRig>();
        if (worldRig != null)
        {
            if (bodyLayer >= 0) SetLayerRecursively(worldRig.transform.Find("World Weapon Anchor"), bodyLayer);
            combat.SetWorldWeaponRig(worldRig);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void CreateDummy(Vector3 position)
    {
        GameObject dummy = new GameObject("Training Dummy");
        dummy.transform.position = position;
        Damageable damageable = dummy.AddComponent<Damageable>();
        CreateHitbox(dummy.transform, "Legs", new Vector3(0f, 0.45f, 0f), new Vector3(0.65f, 0.9f, 0.38f), new Color(0.24f, 0.30f, 0.36f), damageable, HitZone.Leg, 0.7f);
        CreateHitbox(dummy.transform, "Body", new Vector3(0f, 1.28f, 0f), new Vector3(0.85f, 0.8f, 0.42f), new Color(0.18f, 0.35f, 0.28f), damageable, HitZone.Body, 1f);
        CreateHitbox(dummy.transform, "Head", new Vector3(0f, 1.95f, 0f), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.75f, 0.52f, 0.34f), damageable, HitZone.Head, 2.4f);
    }

    private void CreateHitbox(Transform parent, string hitboxName, Vector3 localPosition, Vector3 scale, Color color, Damageable owner, HitZone zone, float multiplier)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = hitboxName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material.color = color;
        part.AddComponent<Hitbox>().Initialize(owner, zone, multiplier);
    }

}
