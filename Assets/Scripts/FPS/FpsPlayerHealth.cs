using UnityEngine;

public sealed class FpsPlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    private float health;
    public float Health => health;

    private void Awake() => health = maxHealth;

    public void TakeDamage(float amount)
    {
        health = Mathf.Max(0f, health - amount);
        if (health > 0f) return;

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        transform.position = new Vector3(-26f, 0.02f, 0f);
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        if (controller != null) controller.enabled = true;
        health = maxHealth;
        Debug.Log("Blue player defeated and respawned.");
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        GUI.Label(new Rect(18, Screen.height - 39, 220, 30), $"HP {health:0}/{maxHealth:0}", style);
    }
}
