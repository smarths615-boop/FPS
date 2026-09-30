using UnityEngine;
using FPS.Core;

namespace FPS.Combat
{
    /// <summary>
    /// Builds head / body / leg hitboxes on a rigged character at runtime and wires them
    /// to the owner's <see cref="Health"/>. Because the SWAT model is a skinned mesh with
    /// no authored colliders, this is what makes zone damage possible.
    ///
    /// Zone sizes are expressed as fractions of the character height so the same rig
    /// works for the player and every AI unit.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitboxRig : MonoBehaviour
    {
        [Header("Source bones (optional - auto-detected)")]
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform chestBone;
        [SerializeField] private Transform thighBoneL;
        [SerializeField] private Transform thighBoneR;

        [Header("Owner")]
        [SerializeField] private Health owner;

        [Header("Tuning (fractions of total height)")]
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float headRadius = 0.13f;
        [SerializeField] private float headCenterFromTop = 0.10f;
        [SerializeField] private float chestRadius = 0.26f;
        [SerializeField] private float legRadius = 0.12f;
        [SerializeField] private float legSplit = 0.52f; // hips height where legs begin

        private void Awake()
        {
            owner = owner != null ? owner : GetComponentInParent<Health>();
            if (owner == null) owner = gameObject.AddComponent<Health>();
            AutoDetect();
        }

        private void AutoDetect()
        {
            headBone = headBone ?? Find("Head");
            chestBone = chestBone ?? Find("Spine_02") ?? Find("Spine_01") ?? Find("Hips");
            thighBoneL = thighBoneL ?? Find("thigh_stretch.l")?.parent;
            thighBoneR = thighBoneR ?? Find("thigh_stretch.r")?.parent;
        }

        private Transform Find(string n)
        {
            foreach (Transform c in GetComponentsInChildren<Transform>(true))
                if (c.name == n) return c;
            return null;
        }

        /// <summary>
        /// Call after Awake to actually create the zones. Kept separate so the layout can
        /// be rebuilt in the editor without duplicating colliders.
        /// </summary>
        public void Build()
        {
            ClearExisting();

            Vector3 hipsPos = hipsPosition();
            float total = height;

            // ---- head ----
            Vector3 headPos = headBone != null
                ? headBone.position
                : transform.position + Vector3.up * total * (1f - headCenterFromTop);
            CreateZone("Hitbox_Head", headPos, headRadius, HitZone.Head, true);

            // ---- body (torso) ----
            Vector3 chestPos = chestBone != null ? chestBone.position : hipsPos + Vector3.up * total * 0.22f;
            float bodyH = Mathf.Max(0.25f, total * 0.34f);
            CreateCapsuleZone("Hitbox_Body", chestPos, chestRadius, bodyH, HitZone.Body);

            // ---- legs ----
            float legTop = hipsPos.y;
            float legH = total * legSplit;
            Vector3 legCenter = new Vector3(transform.position.x, legTop - legH * 0.5f, transform.position.z);
            CreateCapsuleZone("Hitbox_Legs", legCenter, legRadius, legH, HitZone.Legs);
        }

        private Vector3 hipsPosition()
        {
            var hips = Find("Hips");
            return hips != null ? hips.position : transform.position + Vector3.up * height * 0.52f;
        }

        private void CreateZone(string name, Vector3 center, float radius, HitZone zone, bool sphere)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = center;

            var col = go.AddComponent<CapsuleCollider>();
            if (sphere)
            {
                col.direction = 1;
                col.height = radius * 2f;
                col.radius = radius;
            }
            else
            {
                col.direction = 1;
                col.height = 0.01f;
                col.radius = radius;
            }
            col.isTrigger = true;

            go.AddComponent<DamageZone>().Configure(zone, owner);
        }

        private void CreateCapsuleZone(string name, Vector3 center, float radius, float heightY, HitZone zone)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = center;

            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;      // vertical
            col.height = heightY;
            col.radius = radius;
            col.isTrigger = true;

            go.AddComponent<DamageZone>().Configure(zone, owner);
        }

        private void ClearExisting()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name.StartsWith("Hitbox_")) Destroy(c.gameObject);
            }
        }

        private void Start()
        {
            Build();
        }
    }
}
