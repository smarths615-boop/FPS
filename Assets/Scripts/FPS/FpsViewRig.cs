using System.Collections.Generic;
using UnityEngine;

public sealed class FpsViewRig : MonoBehaviour
{
    private readonly List<GameObject> primaryModels = new List<GameObject>();
    private readonly List<GameObject> secondaryModels = new List<GameObject>();
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftHand;
    private Transform rightHand;
    private WeaponSlot slot;
    private int currentIndex;
    private float fireKick;
    private float punchTimer;
    private bool rightPunch;
    private bool aiming;
    private static readonly Vector3 HoldPosition = new Vector3(0.21f, -0.31f, 0.68f);
    private static readonly Vector3 AimPosition = new Vector3(0f, -0.21f, 0.61f);

    public void Build(GameObject[] primaryPrefabs, GameObject[] secondaryPrefabs)
    {
        leftArm = CreateArm("LeftArm", new Vector3(-0.27f, -0.52f, 0.41f), -18f);
        rightArm = CreateArm("RightArm", new Vector3(0.27f, -0.52f, 0.37f), 18f);
        leftHand = CreateHand("Left Hand", new Vector3(0.13f, -0.28f, 0.88f), false);
        rightHand = CreateHand("Right Hand", new Vector3(0.27f, -0.31f, 0.52f), true);

        string[] primaryNames = { "AR-T", "AR-U", "AR-W Black", "SMG-P", "SMG-Q", "ShotGun-P", "Recon-P", "Recon-M" };
        string[] secondaryNames = { "Pistol-P", "Pistol-F" };
        for (int i = 0; i < primaryNames.Length; i++)
            primaryModels.Add(CreateWeapon(primaryNames[i], GetPrefab(primaryPrefabs, i), i < 3 ? new Color(0.22f, 0.27f, 0.25f) : Color.gray));
        for (int i = 0; i < secondaryNames.Length; i++)
            secondaryModels.Add(CreateWeapon(secondaryNames[i], GetPrefab(secondaryPrefabs, i), Color.gray));
        SetWeapon(WeaponSlot.Primary, 0, Color.white);
    }

    public void SetWeapon(WeaponSlot nextSlot, int nextIndex, Color color)
    {
        slot = nextSlot;
        currentIndex = nextIndex;
        foreach (GameObject model in primaryModels) model.SetActive(false);
        foreach (GameObject model in secondaryModels) model.SetActive(false);
        if (slot == WeaponSlot.Primary && currentIndex < primaryModels.Count)
            primaryModels[currentIndex].SetActive(true);
        if (slot == WeaponSlot.Secondary && currentIndex < secondaryModels.Count)
            secondaryModels[currentIndex].SetActive(true);
    }

    public void Fire(Color color) => fireKick = 1f;
    public void SetAiming(bool value) => aiming = value;

    public void Punch()
    {
        punchTimer = 1f;
        rightPunch = !rightPunch;
    }

    private void Update()
    {
        fireKick = Mathf.MoveTowards(fireKick, 0f, Time.deltaTime * 9f);
        punchTimer = Mathf.MoveTowards(punchTimer, 0f, Time.deltaTime * 5f);

        GameObject activeGun = slot == WeaponSlot.Primary && currentIndex < primaryModels.Count ? primaryModels[currentIndex]
            : slot == WeaponSlot.Secondary && currentIndex < secondaryModels.Count ? secondaryModels[currentIndex] : null;
        if (activeGun != null)
            activeGun.transform.localPosition = Vector3.Lerp(activeGun.transform.localPosition,
                (aiming ? AimPosition : HoldPosition) + Vector3.back * (fireKick * 0.09f), Time.deltaTime * 17f);

        float punchOffset = Mathf.Sin((1f - punchTimer) * Mathf.PI) * 0.38f;
        bool fists = slot == WeaponSlot.Fists;
        Vector3 leftTarget = fists ? new Vector3(-0.18f, -0.23f, 0.68f) : aiming ? new Vector3(-0.07f, -0.27f, 0.82f) : new Vector3(0.13f, -0.28f, 0.88f);
        Vector3 rightTarget = fists ? new Vector3(0.18f, -0.23f, 0.68f) : aiming ? new Vector3(0.08f, -0.27f, 0.50f) : new Vector3(0.27f, -0.31f, 0.52f);
        if (punchTimer > 0f)
        {
            if (rightPunch) rightTarget.z += punchOffset;
            else leftTarget.z += punchOffset;
        }
        leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, leftTarget, Time.deltaTime * 17f);
        rightHand.localPosition = Vector3.Lerp(rightHand.localPosition, rightTarget, Time.deltaTime * 17f);
        PositionArm(leftArm, new Vector3(-0.38f, -0.77f, 0.14f), leftHand.localPosition);
        PositionArm(rightArm, new Vector3(0.40f, -0.77f, 0.13f), rightHand.localPosition);
    }

    private static GameObject GetPrefab(GameObject[] prefabs, int index)
    {
        return prefabs != null && index < prefabs.Length ? prefabs[index] : null;
    }

    private Transform CreateArm(string name, Vector3 position, float roll)
    {
        GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arm.name = name;
        arm.transform.SetParent(transform, false);
        arm.transform.localPosition = position;
        arm.transform.localRotation = Quaternion.Euler(58f, 0f, roll);
        arm.transform.localScale = new Vector3(0.105f, 0.27f, 0.105f);
        Destroy(arm.GetComponent<Collider>());
        arm.GetComponent<Renderer>().material.color = new Color(0.16f, 0.24f, 0.22f);
        return arm.transform;
    }

    private Transform CreateHand(string name, Vector3 position, bool right)
    {
        GameObject hand = new GameObject(name);
        hand.transform.SetParent(transform, false);
        hand.transform.localPosition = position;
        Color glove = new Color(0.34f, 0.36f, 0.32f);
        CreatePart(hand.transform, "Palm", Vector3.zero, new Vector3(0.095f, 0.063f, 0.105f), glove);
        for (int finger = 0; finger < 4; finger++)
        {
            float x = (finger - 1.5f) * 0.021f;
            CreatePart(hand.transform, "Gloved Finger " + finger, new Vector3(x, -0.014f, 0.079f),
                new Vector3(0.018f, 0.038f, 0.075f), glove);
        }
        CreatePart(hand.transform, "Thumb", new Vector3(right ? -0.055f : 0.055f, -0.025f, 0.005f),
            new Vector3(0.037f, 0.045f, 0.062f), glove);
        return hand.transform;
    }

    private static void CreatePart(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().material.color = color;
    }

    private static void PositionArm(Transform arm, Vector3 shoulder, Vector3 wrist)
    {
        Vector3 segment = wrist - shoulder;
        arm.localPosition = (shoulder + wrist) * 0.5f;
        arm.localRotation = Quaternion.FromToRotation(Vector3.up, segment.normalized);
        arm.localScale = new Vector3(0.105f, segment.magnitude * 0.5f, 0.105f);
    }

    private GameObject CreateWeapon(string name, GameObject prefab, Color fallbackColor)
    {
        GameObject holder = new GameObject(name + " View Model");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = HoldPosition;

        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, holder.transform);
            model.name = prefab.name;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            FitModel(model.transform, holder.transform);
        }
        else
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = name + " Placeholder";
            fallback.transform.SetParent(holder.transform, false);
            fallback.transform.localScale = new Vector3(0.14f, 0.12f, 0.60f);
            fallback.GetComponent<Renderer>().material.color = fallbackColor;
            Destroy(fallback.GetComponent<Collider>());
        }
        return holder;
    }

    private static void FitModel(Transform model, Transform holder)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.localRotation = Quaternion.identity;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest < 0.001f) return;
        model.localScale *= 0.82f / longest;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.localPosition -= holder.InverseTransformPoint(bounds.center);
    }
}
