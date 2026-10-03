using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FpsPlayerMotor : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintMultiplier = 1.45f;
    [SerializeField] private float jumpHeight = 1.15f;
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float mouseSensitivity = 0.12f;

    private CharacterController controller;
    private Camera playerCamera;
    private FpsCombatController combat;
    private float verticalVelocity;
    private float pitch;
    private float standingHeight;

    public void AddRecoil(float vertical, float horizontal)
    {
        pitch = Mathf.Clamp(pitch - vertical, -80f, 80f);
        transform.Rotate(Vector3.up * horizontal);
    }

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }

    public void Initialize(Camera camera, FpsCombatController combatController)
    {
        playerCamera = camera;
        combat = combatController;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || playerCamera == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState == CursorLockMode.Locked) Look();
        Move();
    }

    private void Look()
    {
        Vector2 delta = Mouse.current.delta.ReadValue();
        transform.Rotate(Vector3.up * (delta.x * mouseSensitivity));
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -80f, 80f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        IsCrouching = keyboard.cKey.isPressed || keyboard.leftCtrlKey.isPressed;
        IsSprinting = input.y > 0f && keyboard.leftShiftKey.isPressed && !IsCrouching && (combat == null || !combat.IsAiming);
        IsMoving = input.sqrMagnitude > 0.01f;

        float targetHeight = IsCrouching ? standingHeight * 0.58f : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * 14f);
        controller.center = Vector3.up * controller.height * 0.5f;

        float weaponSpeed = combat != null ? combat.CurrentMoveMultiplier : 1f;
        float speed = walkSpeed * weaponSpeed * (IsSprinting ? sprintMultiplier : 1f) * (IsCrouching ? 0.58f : 1f);
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (keyboard.spaceKey.wasPressedThisFrame && !IsCrouching)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);

        Vector3 cameraPosition = playerCamera.transform.localPosition;
        cameraPosition.y = Mathf.Lerp(cameraPosition.y, IsCrouching ? 1.08f : 1.65f, Time.deltaTime * 14f);
        playerCamera.transform.localPosition = cameraPosition;
    }
}
