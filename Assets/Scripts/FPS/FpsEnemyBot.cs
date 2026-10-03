using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Damageable))]
public sealed class FpsEnemyBot : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.1f;
    [SerializeField] private float fireInterval = 0.72f;
    [SerializeField] private float bulletDamage = 11f;
    [SerializeField] private float engagementRange = 32f;
    private readonly Vector3 spawn = new Vector3(27f, 0f, 0f);
    private CharacterController controller;
    private Damageable damageable;
    private FpsCharacterMotion motion;
    private FpsPlayerHealth player;
    private float nextShot;
    private float respawnAt;
    private bool dead;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        damageable = GetComponent<Damageable>();
        motion = GetComponent<FpsCharacterMotion>();
        damageable.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (damageable != null) damageable.Died -= OnDied;
    }

    private void Update()
    {
        if (dead)
        {
            if (Time.time >= respawnAt) Respawn();
            return;
        }

        if (player == null) player = FindFirstObjectByType<FpsPlayerHealth>();
        if (player == null) return;

        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 7f);

        bool visible = CanSeePlayer(distance);
        bool moving = distance > 11f || !visible;
        if (moving)
        {
            Vector3 destination = player.transform.position;
            if (transform.position.x > 18.5f)
                destination = new Vector3(16f, 0f, 0f);
            else if (!visible && transform.position.x > 4f)
                destination = new Vector3(3f, 0f, 8f);
            Vector3 move = destination - transform.position;
            move.y = 0f;
            if (move.sqrMagnitude > 0.5f)
                controller.Move((move.normalized * moveSpeed + Vector3.down * 2f) * Time.deltaTime);
        }
        else
            controller.Move(Vector3.down * (2f * Time.deltaTime));

        if (motion != null) motion.SetBotState(moving, visible);
        if (visible && distance < engagementRange && Time.time >= nextShot)
        {
            nextShot = Time.time + fireInterval;
            if (Random.value < 0.72f) player.TakeDamage(bulletDamage);
            Debug.DrawLine(transform.position + Vector3.up * 1.6f, player.transform.position + Vector3.up * 1.4f, Color.red, 0.35f);
        }
    }

    private bool CanSeePlayer(float distance)
    {
        if (distance > engagementRange) return false;
        Vector3 origin = transform.position + Vector3.up * 1.6f;
        Vector3 target = player.transform.position + Vector3.up * 1.4f;
        if (!Physics.Raycast(origin, (target - origin).normalized, out RaycastHit hit, distance + 2f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        return hit.collider.GetComponentInParent<FpsPlayerHealth>() == player;
    }

    private void OnDied()
    {
        dead = true;
        respawnAt = Time.time + 3f;
        controller.enabled = false;
        transform.position += Vector3.down * 4f;
        if (motion != null) motion.SetBotState(false, false);
    }

    private void Respawn()
    {
        transform.position = spawn;
        controller.enabled = true;
        damageable.ResetHealth();
        dead = false;
        nextShot = Time.time + 0.7f;
    }

    private void OnGUI()
    {
        if (damageable == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = new Color(1f, 0.45f, 0.45f) } };
        GUI.Label(new Rect(Screen.width - 245, 18, 235, 30), $"RED SWAT HP {damageable.Health:0}", style);
    }
}
