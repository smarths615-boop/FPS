using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(CharacterController), typeof(FpsOwnerNetworkTransform))]
public sealed class FpsNetworkPlayer : NetworkBehaviour
{
    [SerializeField] private GameObject[] primaryWeaponPrefabs;
    [SerializeField] private GameObject[] secondaryWeaponPrefabs;
    private readonly NetworkVariable<float> health = new NetworkVariable<float>(100f);
    private readonly NetworkVariable<int> weaponState = new NetworkVariable<int>(0);
    private readonly NetworkVariable<byte> motionState = new NetworkVariable<byte>(8);
    private FpsCombatController combat;
    private FpsPlayerMotor motor;
    private FpsCharacterMotion characterMotion;
    private FpsWorldWeaponRig worldRig;
    private float nextMotionSend;
    private float lastServerFire = -10f;
    private float lastServerPunch = -10f;
    private float serverBloom;

    public void Configure(GameObject[] primary, GameObject[] secondary)
    {
        primaryWeaponPrefabs = primary;
        secondaryWeaponPrefabs = secondary;
    }

    public override void OnNetworkSpawn()
    {
        worldRig = GetComponent<FpsWorldWeaponRig>();
        combat = GetComponent<FpsCombatController>();
        motor = GetComponent<FpsPlayerMotor>();
        characterMotion = GetComponentInChildren<FpsCharacterMotion>(true);
        weaponState.OnValueChanged += OnWeaponChanged;
        motionState.OnValueChanged += OnMotionChanged;
        GetComponent<CharacterController>().enabled = IsOwner;
        if (IsOwner)
        {
            CreateOwnerCamera();
            if (characterMotion != null) characterMotion.Initialize(motor, combat);
        }
        else
        {
            if (combat != null) combat.enabled = false;
            if (motor != null) motor.enabled = false;
            if (characterMotion != null) characterMotion.SetNetworkState(motionState.Value);
        }
        OnWeaponChanged(weaponState.Value, weaponState.Value);
    }

    public override void OnNetworkDespawn()
    {
        weaponState.OnValueChanged -= OnWeaponChanged;
        motionState.OnValueChanged -= OnMotionChanged;
    }

    private void CreateOwnerCamera()
    {
        foreach (Camera existing in FindObjectsByType<Camera>(FindObjectsSortMode.None)) existing.enabled = false;
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) listener.enabled = false;
        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.03f;
        cameraObject.AddComponent<AudioListener>();
        int bodyLayer = LayerMask.NameToLayer("PlayerBody");
        if (bodyLayer >= 0)
        {
            camera.cullingMask &= ~(1 << bodyLayer);
            SetLayerRecursively(transform.Find("SWAT Body"), bodyLayer);
            SetLayerRecursively(transform.Find("World Weapon Anchor"), bodyLayer);
        }
        FpsViewRig view = cameraObject.AddComponent<FpsViewRig>();
        view.Build(primaryWeaponPrefabs, secondaryWeaponPrefabs);
        combat.Initialize(camera, view);
        combat.SetMotor(motor);
        if (worldRig != null) combat.SetWorldWeaponRig(worldRig);
        combat.OnlineSelection = (slot, index) => ChangeWeaponServerRpc((int)slot, index);
        combat.OnlineFire = (origin, direction, slot, index, ads) => FireServerRpc(direction, (int)slot, index, ads);
        combat.OnlinePunch = (origin, direction) => PunchServerRpc(direction);
        combat.enabled = true;
        motor.Initialize(camera, combat);
        motor.enabled = true;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || motor == null || Time.time < nextMotionSend) return;
        nextMotionSend = Time.time + 0.1f;
        byte state = 0;
        if (motor.IsMoving) state |= 1;
        if (motor.IsSprinting) state |= 2;
        if (motor.IsCrouching) state |= 4;
        if (combat != null && combat.SelectedSlot != WeaponSlot.Fists) state |= 8;
        if (motionState.Value != state) SetMotionServerRpc(state);
    }

    [ServerRpc]
    private void SetMotionServerRpc(byte state) => motionState.Value = state;

    private void OnMotionChanged(byte previous, byte current)
    {
        if (!IsOwner && characterMotion != null) characterMotion.SetNetworkState(current);
    }

    [ServerRpc]
    private void ChangeWeaponServerRpc(int slot, int index)
    {
        if (slot == (int)WeaponSlot.Fists) weaponState.Value = -1;
        else if (combat != null && combat.IsValidWeapon((WeaponSlot)slot, index))
            weaponState.Value = slot == (int)WeaponSlot.Secondary ? 100 + index : index;
    }

    private void OnWeaponChanged(int previous, int current)
    {
        if (worldRig == null) return;
        if (current < 0) worldRig.SetWeapon(WeaponSlot.Fists, 0);
        else if (current >= 100) worldRig.SetWeapon(WeaponSlot.Secondary, current - 100);
        else worldRig.SetWeapon(WeaponSlot.Primary, current);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 direction, int slotValue, int index, bool ads)
    {
        WeaponSlot slot = (WeaponSlot)slotValue;
        if (combat == null || !combat.IsValidWeapon(slot, index) || direction.sqrMagnitude < 0.5f) return;
        float interval = combat.GetFireInterval(slot, index);
        if (Time.time < lastServerFire + interval * 0.9f) return;
        serverBloom = Mathf.Max(0f, serverBloom - (Time.time - lastServerFire) * 2.2f);
        lastServerFire = Time.time;
        float spread = combat.GetSpread(slot, index, ads) + serverBloom;
        serverBloom = Mathf.Min(2.8f, serverBloom + 0.13f);
        Vector3 origin = transform.position + Vector3.up * 1.55f;
        for (int pellet = 0; pellet < combat.GetPellets(slot, index); pellet++)
        {
            Vector3 shot = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * direction.normalized;
            if (!Physics.Raycast(origin, shot, out RaycastHit hit, combat.GetRange(slot, index), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            FpsNetworkHitbox box = hit.collider.GetComponent<FpsNetworkHitbox>();
            FpsNetworkPlayer target = hit.collider.GetComponentInParent<FpsNetworkPlayer>();
            if (box != null && target != null && target != this)
                target.ApplyDamage(combat.GetDamage(slot, index) * box.Multiplier);
        }
    }

    [ServerRpc]
    private void PunchServerRpc(Vector3 direction)
    {
        if (Time.time < lastServerPunch + 0.4f) return;
        lastServerPunch = Time.time;
        if (!Physics.Raycast(transform.position + Vector3.up * 1.5f, direction.normalized, out RaycastHit hit, 2.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        FpsNetworkPlayer target = hit.collider.GetComponentInParent<FpsNetworkPlayer>();
        if (target != null && target != this) target.ApplyDamage(30f);
    }

    private void ApplyDamage(float amount)
    {
        if (!IsServer || health.Value <= 0f) return;
        health.Value = Mathf.Max(0f, health.Value - amount);
        if (health.Value > 0f) return;
        health.Value = 100f;
        Vector3 spawn = OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? new Vector3(-26f, 0.1f, 0f) : new Vector3(26f, 0.1f, 0f);
        Quaternion facing = Quaternion.Euler(0f, OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? 90f : -90f, 0f);
        RespawnClientRpc(spawn, facing);
    }

    [ClientRpc]
    private void RespawnClientRpc(Vector3 position, Quaternion facing)
    {
        if (!IsOwner) return;
        CharacterController controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.SetPositionAndRotation(position, facing);
        controller.enabled = true;
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        GUI.Label(new Rect(18, Screen.height - 42, 300, 30), $"HP {health.Value:0}/100", style);
        foreach (FpsNetworkPlayer other in FindObjectsByType<FpsNetworkPlayer>(FindObjectsSortMode.None))
            if (other != this)
                GUI.Label(new Rect(Screen.width - 240, 18, 230, 30), $"OPPONENT HP {other.health.Value:0}", style);
    }
}
