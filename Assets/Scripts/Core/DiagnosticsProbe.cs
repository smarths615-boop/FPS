using UnityEngine;

namespace FPS.Core
{
    /// <summary>
    /// Temporary runtime probe used to diagnose camera/player state. Attach to any
    /// scene object; it prints a one-line report every second while in play mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiagnosticsProbe : MonoBehaviour
    {
        public float interval = 1f;
        private float _t;
        private Transform _player;
        private Camera _cam;
        private Player.PlayerController _pc;
        private Player.PlayerCamera _pcam;

        private void Start()
        {
            _pc = FindFirstObjectByType<Player.PlayerController>();
            _pcam = FindFirstObjectByType<Player.PlayerCamera>();
            if (_pc != null) _player = _pc.transform;
            _cam = _pcam != null ? _pcam.Cam : FindFirstObjectByType<Camera>();
            Debug.Log("[DIAG] start player=" + (_player != null ? _player.position.ToString("F2") : "NULL") +
                      " cam=" + (_cam != null ? _cam.name : "NULL") +
                      (_cam != null ? " mask=" + _cam.cullingMask + " enabled=" + _cam.enabled + " fov=" + _cam.fieldOfView : ""));
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t < interval) return;
            _t = 0f;

            if (_player == null) { Debug.Log("[DIAG] no player"); return; }
            var cc = _player.GetComponent<CharacterController>();

            // does geometry actually exist in front of the camera?
            string hitInfo = "none";
            if (_cam != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(_cam.transform.position, _cam.transform.forward, out hit, 300f))
                    hitInfo = hit.collider.name + "@" + hit.distance.ToString("F1") + " layer=" + LayerMask.LayerToName(hit.collider.gameObject.layer);
            }

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int activeR = 0, visibleInCam = 0;
            string sampleMat = "-", sampleShader = "-";
            if (_cam != null)
            {
                foreach (var r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    activeR++;
                    if ((_cam.cullingMask & (1 << r.gameObject.layer)) != 0) visibleInCam++;
                }
            }
            var floor = GameObject.Find("Floor");
            if (floor != null)
            {
                var fr = floor.GetComponent<MeshRenderer>();
                if (fr != null)
                {
                    var m = fr.sharedMaterial;
                    sampleMat = m != null ? m.name : "NULL";
                    sampleShader = (m != null && m.shader != null) ? m.shader.name : "NULLSHADER";
                }
            }

            var srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            string srpName = srp != null ? srp.name : "NULL(builtin fallback)";

            Debug.Log("[DIAG] p=" + _player.position.ToString("F2") +
                      " camPos=" + (_cam != null ? _cam.transform.position.ToString("F2") : "?") +
                      " camFwd=" + (_cam != null ? _cam.transform.forward.ToString("F2") : "?") +
                      " ray=" + hitInfo +
                      " renderers=" + activeR + " inMask=" + visibleInCam +
                      " SRP=" + srpName +
                      " quality=" + UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()] +
                      " floorMat=" + sampleMat + " shader=" + sampleShader +
                      " enemies=" + Object.FindObjectsByType<AI.EnemyAI>(FindObjectsSortMode.None).Length);
        }
    }
}
