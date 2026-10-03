using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponSlot
{
    Primary,
    Secondary,
    Fists
}

public sealed class FpsCombatController : MonoBehaviour
{
    private sealed class WeaponDefinition
    {
        public string Name;
        public float Damage;
        public float RoundsPerMinute;
        public float Range;
        public float MoveMultiplier;
        public int Pellets;
        public bool Automatic;
        public int Magazine;
        public Color Color;
        public float HipSpread;
        public float AdsSpread;
        public float Recoil;
        public float Spray;
        public float AdsFov;

        public WeaponDefinition(string name, float damage, float rpm, float range, float moveMultiplier, int pellets, bool automatic, int magazine, Color color,
            float hipSpread, float adsSpread, float recoil, float spray, float adsFov)
        {
            Name = name;
            Damage = damage;
            RoundsPerMinute = rpm;
            Range = range;
            MoveMultiplier = moveMultiplier;
            Pellets = pellets;
            Automatic = automatic;
            Magazine = magazine;
            Color = color;
            HipSpread = hipSpread;
            AdsSpread = adsSpread;
            Recoil = recoil;
            Spray = spray;
            AdsFov = adsFov;
        }
    }

    private readonly List<WeaponDefinition> primaryWeapons = new List<WeaponDefinition>
    {
        new WeaponDefinition("AR-T Assault Rifle", 25f, 720f, 110f, 0.93f, 1, true, 30, new Color(0.18f, 0.28f, 0.22f), 1.2f, 0.35f, 0.30f, 0.13f, 52f),
        new WeaponDefinition("AR-U Assault Rifle", 27f, 650f, 120f, 0.90f, 1, true, 30, new Color(0.27f, 0.29f, 0.23f), 1.0f, 0.30f, 0.40f, 0.14f, 49f),
        new WeaponDefinition("AR-W Black", 29f, 610f, 130f, 0.87f, 1, true, 30, new Color(0.12f, 0.13f, 0.15f), 0.9f, 0.25f, 0.47f, 0.15f, 46f),
        new WeaponDefinition("SMG-P", 18f, 960f, 70f, 1.02f, 1, true, 36, new Color(0.18f, 0.30f, 0.40f), 1.9f, 0.65f, 0.20f, 0.18f, 56f),
        new WeaponDefinition("SMG-Q", 20f, 850f, 80f, 0.99f, 1, true, 32, new Color(0.20f, 0.22f, 0.25f), 1.6f, 0.56f, 0.25f, 0.17f, 54f),
        new WeaponDefinition("ShotGun-P", 11f, 95f, 35f, 0.78f, 9, false, 8, new Color(0.36f, 0.23f, 0.12f), 5.5f, 3.5f, 2.5f, 0f, 58f),
        new WeaponDefinition("Recon-P", 72f, 55f, 220f, 0.72f, 1, false, 5, new Color(0.25f, 0.25f, 0.28f), 4.0f, 0.05f, 3.2f, 0f, 28f),
        new WeaponDefinition("Recon-M", 68f, 65f, 200f, 0.75f, 1, false, 7, new Color(0.28f, 0.29f, 0.25f), 3.6f, 0.09f, 2.8f, 0f, 32f)
    };

    private readonly List<WeaponDefinition> secondaryWeapons = new List<WeaponDefinition>
    {
        new WeaponDefinition("Pistol-P", 32f, 360f, 60f, 1.06f, 1, false, 15, new Color(0.17f, 0.17f, 0.17f), 1.3f, 0.45f, 0.80f, 0f, 59f),
        new WeaponDefinition("Pistol-F", 31f, 420f, 55f, 1.08f, 1, false, 17, new Color(0.24f, 0.23f, 0.20f), 1.5f, 0.55f, 0.68f, 0f, 60f)
    };
    private Camera playerCamera;
    private FpsViewRig viewRig;
    private FpsCharacterMotion characterMotion;
    private FpsWorldWeaponRig worldWeaponRig;
    private FpsPlayerMotor motor;
    private WeaponSlot selectedSlot = WeaponSlot.Primary;
    private int primaryIndex;
    private int secondaryIndex;
    private int currentAmmo;
    private float nextFireTime;
    private bool punchRight;
    private bool aiming;
    private float sprayBloom;
    public System.Action<WeaponSlot, int> OnlineSelection;
    public System.Action<Vector3, Vector3, WeaponSlot, int, bool> OnlineFire;
    public System.Action<Vector3, Vector3> OnlinePunch;

    public float CurrentMoveMultiplier => (selectedSlot == WeaponSlot.Fists ? 1.12f : CurrentWeapon.MoveMultiplier) * (aiming ? 0.72f : 1f);
    public bool IsAiming => aiming;
    public string ActiveWeaponName => selectedSlot == WeaponSlot.Fists ? "Fists" : CurrentWeapon.Name;
    public WeaponSlot SelectedSlot => selectedSlot;
    public int PrimaryIndex => primaryIndex;
    public int SecondaryIndex => secondaryIndex;
    public Color ActiveColor => selectedSlot == WeaponSlot.Fists ? new Color(0.8f, 0.45f, 0.28f) : CurrentWeapon.Color;
    private WeaponDefinition CurrentWeapon => selectedSlot == WeaponSlot.Secondary ? secondaryWeapons[secondaryIndex] : primaryWeapons[primaryIndex];
    private int ActiveIndex => selectedSlot == WeaponSlot.Secondary ? secondaryIndex : primaryIndex;

    public float GetDamage(WeaponSlot slot, int index) => GetDefinition(slot, index).Damage;
    public float GetRange(WeaponSlot slot, int index) => GetDefinition(slot, index).Range;
    public float GetSpread(WeaponSlot slot, int index, bool ads) => ads ? GetDefinition(slot, index).AdsSpread : GetDefinition(slot, index).HipSpread;
    public int GetPellets(WeaponSlot slot, int index) => GetDefinition(slot, index).Pellets;
    public float GetFireInterval(WeaponSlot slot, int index) => 60f / GetDefinition(slot, index).RoundsPerMinute;
    public bool IsValidWeapon(WeaponSlot slot, int index) => slot == WeaponSlot.Primary ? index >= 0 && index < primaryWeapons.Count
        : slot == WeaponSlot.Secondary && index >= 0 && index < secondaryWeapons.Count;
    private WeaponDefinition GetDefinition(WeaponSlot slot, int index) => slot == WeaponSlot.Secondary ? secondaryWeapons[index] : primaryWeapons[index];

    public void Initialize(Camera camera, FpsViewRig rig)
    {
        playerCamera = camera;
        viewRig = rig;
        currentAmmo = CurrentWeapon.Magazine;
        viewRig.SetWeapon(selectedSlot, ActiveIndex, ActiveColor);
    }

    public void SetCharacterMotion(FpsCharacterMotion motion) => characterMotion = motion;
    public void SetWorldWeaponRig(FpsWorldWeaponRig rig)
    {
        worldWeaponRig = rig;
        worldWeaponRig.SetWeapon(selectedSlot, ActiveIndex);
    }
    public void SetMotor(FpsPlayerMotor playerMotor) => motor = playerMotor;

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || playerCamera == null)
            return;

        HandleSelection();
        aiming = selectedSlot != WeaponSlot.Fists && Mouse.current.rightButton.isPressed;
        sprayBloom = Mathf.MoveTowards(sprayBloom, 0f, Time.deltaTime * 2.2f);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, aiming ? CurrentWeapon.AdsFov : 70f, Time.deltaTime * 12f);
        viewRig.SetAiming(aiming);
        HandleFire();
    }

    private void HandleSelection()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSlot(WeaponSlot.Primary);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSlot(WeaponSlot.Secondary);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSlot(WeaponSlot.Fists);

        if (selectedSlot != WeaponSlot.Fists && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (selectedSlot == WeaponSlot.Primary)
                primaryIndex = (primaryIndex + primaryWeapons.Count - 1) % primaryWeapons.Count;
            else
                secondaryIndex = (secondaryIndex + secondaryWeapons.Count - 1) % secondaryWeapons.Count;
            currentAmmo = CurrentWeapon.Magazine;
            viewRig.SetWeapon(selectedSlot, ActiveIndex, ActiveColor);
            if (worldWeaponRig != null) worldWeaponRig.SetWeapon(selectedSlot, ActiveIndex);
            OnlineSelection?.Invoke(selectedSlot, ActiveIndex);
        }
        if (selectedSlot != WeaponSlot.Fists && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (selectedSlot == WeaponSlot.Primary)
                primaryIndex = (primaryIndex + 1) % primaryWeapons.Count;
            else
                secondaryIndex = (secondaryIndex + 1) % secondaryWeapons.Count;
            currentAmmo = CurrentWeapon.Magazine;
            viewRig.SetWeapon(selectedSlot, ActiveIndex, ActiveColor);
            if (worldWeaponRig != null) worldWeaponRig.SetWeapon(selectedSlot, ActiveIndex);
            OnlineSelection?.Invoke(selectedSlot, ActiveIndex);
        }
        if (Keyboard.current.rKey.wasPressedThisFrame && selectedSlot != WeaponSlot.Fists)
            currentAmmo = CurrentWeapon.Magazine;
    }

    private void SelectSlot(WeaponSlot slot)
    {
        if (slot == selectedSlot) return;
        selectedSlot = slot;
        currentAmmo = selectedSlot == WeaponSlot.Fists ? 0 : CurrentWeapon.Magazine;
        viewRig.SetWeapon(selectedSlot, ActiveIndex, ActiveColor);
        if (worldWeaponRig != null) worldWeaponRig.SetWeapon(selectedSlot, ActiveIndex);
        OnlineSelection?.Invoke(selectedSlot, ActiveIndex);
    }

    private void HandleFire()
    {
        if (selectedSlot == WeaponSlot.Fists)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextFireTime)
                Punch();
            return;
        }

        bool requested = CurrentWeapon.Automatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
        if (!requested || Time.time < nextFireTime) return;
        if (currentAmmo <= 0)
        {
            nextFireTime = Time.time + 0.2f;
            return;
        }

        currentAmmo--;
        nextFireTime = Time.time + 60f / CurrentWeapon.RoundsPerMinute;
        viewRig.Fire(CurrentWeapon.Color);
        float spread = (aiming ? CurrentWeapon.AdsSpread : CurrentWeapon.HipSpread) + sprayBloom;
        if (OnlineFire != null)
            OnlineFire(playerCamera.transform.position, playerCamera.transform.forward, selectedSlot, ActiveIndex, aiming);
        else
            for (int pellet = 0; pellet < CurrentWeapon.Pellets; pellet++)
                FireRay(CurrentWeapon, spread);
        sprayBloom = Mathf.Min(2.8f, sprayBloom + CurrentWeapon.Spray);
        if (motor != null)
            motor.AddRecoil(CurrentWeapon.Recoil * (aiming ? 0.68f : 1f), Random.Range(-0.45f, 0.45f) * CurrentWeapon.Recoil);
    }

    private void FireRay(WeaponDefinition weapon, float spread)
    {
        Vector3 direction = playerCamera.transform.forward;
        direction = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * direction;
        if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, weapon.Range, ~0, QueryTriggerInteraction.Ignore))
        {
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null) hitbox.Apply(weapon.Damage);
            Debug.DrawLine(playerCamera.transform.position, hit.point, weapon.Color, 0.35f);
        }
        else
            Debug.DrawRay(playerCamera.transform.position, direction * weapon.Range, weapon.Color, 0.35f);
    }

    private void Punch()
    {
        nextFireTime = Time.time + 0.42f;
        viewRig.Punch();
        punchRight = !punchRight;
        if (characterMotion != null) characterMotion.TriggerPunch(punchRight);
        if (OnlinePunch != null)
            OnlinePunch(playerCamera.transform.position, playerCamera.transform.forward);
        else if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, 2.2f))
        {
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null) hitbox.Apply(30f);
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
        GUI.Label(new Rect(18, 18, 900, 26), $"1 Primary: {primaryWeapons[primaryIndex].Name}   2 Secondary: {secondaryWeapons[secondaryIndex].Name}   3 Fists", style);
        GUI.Label(new Rect(18, 43, 700, 26), $"Equipped: {ActiveWeaponName}" + (selectedSlot == WeaponSlot.Fists ? "  |  Left click: alternating punches" : $"  |  Ammo: {currentAmmo}/{CurrentWeapon.Magazine}"), style);
        GUI.Label(new Rect(18, 68, 1000, 26), "Q/E: change gun  |  Right click: aim  |  Shift: sprint  |  C/Ctrl: crouch  |  R: reload", style);
        GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 12f, 20, 30), "+", style);
    }
}
