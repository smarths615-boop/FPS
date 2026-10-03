using UnityEngine;

public sealed class FpsCharacterMotion : MonoBehaviour
{
    private enum Pose { Idle, Walk, Sprint, Crouch, Aim, PunchLeft, PunchRight }

    [SerializeField] private bool previewCycle;
    private FpsPlayerMotor motor;
    private FpsCombatController combat;
    private bool botMoving;
    private bool botAiming;
    private byte networkState;
    private bool networkControlled;
    private Transform hips;
    private Transform leftShoulder;
    private Transform rightShoulder;
    private Transform leftElbow;
    private Transform rightElbow;
    private Transform leftThigh;
    private Transform rightThigh;
    private Quaternion hipsRest;
    private Quaternion leftShoulderRest;
    private Quaternion rightShoulderRest;
    private Quaternion leftElbowRest;
    private Quaternion rightElbowRest;
    private Quaternion leftThighRest;
    private Quaternion rightThighRest;
    private Vector3 hipsPosition;
    private float punchUntil;
    private bool punchRight;

    public void Initialize(FpsPlayerMotor playerMotor, FpsCombatController playerCombat)
    {
        motor = playerMotor;
        combat = playerCombat;
        CacheBones();
    }

    public void EnablePreviewCycle()
    {
        previewCycle = true;
        CacheBones();
    }

    public void SetBotState(bool moving, bool aiming)
    {
        botMoving = moving;
        botAiming = aiming;
    }

    public void SetNetworkState(byte flags)
    {
        networkControlled = true;
        networkState = flags;
    }

    public void TriggerPunch(bool right)
    {
        punchRight = right;
        punchUntil = Time.time + 0.35f;
    }

    private void Awake() => CacheBones();

    private void CacheBones()
    {
        hips = FindBone("Hips");
        leftShoulder = FindBone("Shoulder_L");
        rightShoulder = FindBone("Shoulder_R");
        leftElbow = FindBone("Elbow_L");
        rightElbow = FindBone("Elbow_R");
        leftThigh = FindBone("thigh_stretch.l");
        rightThigh = FindBone("thigh_stretch.r");
        if (hips != null) { hipsRest = hips.localRotation; hipsPosition = hips.localPosition; }
        if (leftShoulder != null) leftShoulderRest = leftShoulder.localRotation;
        if (rightShoulder != null) rightShoulderRest = rightShoulder.localRotation;
        if (leftElbow != null) leftElbowRest = leftElbow.localRotation;
        if (rightElbow != null) rightElbowRest = rightElbow.localRotation;
        if (leftThigh != null) leftThighRest = leftThigh.localRotation;
        if (rightThigh != null) rightThighRest = rightThigh.localRotation;
    }

    private Transform FindBone(string boneName)
    {
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            if (bone.name == boneName) return bone;
        return null;
    }

    private void LateUpdate()
    {
        Pose pose = GetPose();
        float swing = 0f;
        float bob = 0f;
        if (pose == Pose.Walk || pose == Pose.Sprint)
        {
            float speed = pose == Pose.Sprint ? 12f : 8f;
            swing = Mathf.Sin(Time.time * speed) * (pose == Pose.Sprint ? 35f : 22f);
            bob = Mathf.Abs(Mathf.Sin(Time.time * speed)) * (pose == Pose.Sprint ? 0.07f : 0.035f);
        }

        float crouch = pose == Pose.Crouch ? 0.42f : 0f;
        if (hips != null)
        {
            hips.localPosition = hipsPosition + new Vector3(0f, bob - crouch, 0f);
            hips.localRotation = hipsRest * Quaternion.Euler(pose == Pose.Crouch ? 15f : 0f, 0f, 0f);
        }
        if (leftThigh != null) leftThigh.localRotation = leftThighRest * Quaternion.Euler(swing + crouch * 40f, 0f, 0f);
        if (rightThigh != null) rightThigh.localRotation = rightThighRest * Quaternion.Euler(-swing + crouch * 40f, 0f, 0f);

        bool aiming = pose == Pose.Aim || pose == Pose.Walk || pose == Pose.Sprint;
        float leftAim = aiming ? -28f : 0f;
        float rightAim = aiming ? -35f : 0f;
        if (pose == Pose.PunchLeft) leftAim = -95f;
        if (pose == Pose.PunchRight) rightAim = -95f;
        if (leftShoulder != null) leftShoulder.localRotation = leftShoulderRest * Quaternion.Euler(leftAim - swing * 0.25f, 0f, 0f);
        if (rightShoulder != null) rightShoulder.localRotation = rightShoulderRest * Quaternion.Euler(rightAim + swing * 0.25f, 0f, 0f);
        if (leftElbow != null) leftElbow.localRotation = leftElbowRest * Quaternion.Euler(pose == Pose.PunchLeft ? -35f : -12f, 0f, 0f);
        if (rightElbow != null) rightElbow.localRotation = rightElbowRest * Quaternion.Euler(pose == Pose.PunchRight ? -35f : -12f, 0f, 0f);
    }

    private Pose GetPose()
    {
        if (previewCycle)
            return (Pose)((int)(Time.time / 2.4f) % 7);
        if (Time.time < punchUntil)
            return punchRight ? Pose.PunchRight : Pose.PunchLeft;
        if (networkControlled)
        {
            if ((networkState & 4) != 0) return Pose.Crouch;
            if ((networkState & 2) != 0) return Pose.Sprint;
            if ((networkState & 1) != 0) return Pose.Walk;
            return (networkState & 8) != 0 ? Pose.Aim : Pose.Idle;
        }
        if (motor == null) return botMoving ? Pose.Walk : botAiming ? Pose.Aim : Pose.Idle;
        if (motor.IsCrouching) return Pose.Crouch;
        if (motor.IsSprinting) return Pose.Sprint;
        if (motor.IsMoving) return Pose.Walk;
        return combat != null && combat.SelectedSlot != WeaponSlot.Fists ? Pose.Aim : Pose.Idle;
    }

}
