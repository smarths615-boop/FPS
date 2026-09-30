using UnityEngine;
using UnityEngine.UI;
using FPS.Combat;
using FPS.Core;
using FPS.Player;

namespace FPS.UI
{
    /// <summary>
    /// Runtime HUD: health bar, ammo readout, weapon name, crosshair that reacts to
    /// movement accuracy, hit markers for zone damage, wave counter and the sniper scope.
    /// Built entirely from code so no prefab authoring is required.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        private PlayerLoadout _loadout;
        private PlayerController _player;
        private Health _health;

        private Text _ammoText, _weaponText, _waveText, _hitText;
        private Image _healthFill, _healthBg;
        private RectTransform _crosshair;
        private Image _scope;
        private float _hitMarkerTime;
        private Color _hitColor;

        private void Awake()
        {
            _loadout = FindFirstObjectByType<PlayerLoadout>();
            _player = FindFirstObjectByType<PlayerController>();
            if (_player != null) _health = _player.GetComponent<Health>();
            BuildUi();
        }

        private void OnEnable()
        {
            if (_loadout != null && _loadout.Active != null)
            {
                _loadout.Active.AmmoChanged += OnAmmo;
                OnAmmo(_loadout.Active.AmmoInMag, _loadout.Active.ReserveAmmo);
            }
        }

        private void OnDisable()
        {
            if (_loadout != null && _loadout.Active != null)
                _loadout.Active.AmmoChanged -= OnAmmo;
        }

        private void OnAmmo(int mag, int reserve)
        {
            if (_ammoText == null) return;
            _ammoText.text = _loadout != null && _loadout.Active != null && _loadout.Active.Definition.isMelee
                ? "--"
                : mag + " / " + reserve;
        }

        private void Update()
        {
            // health
            if (_health != null && _healthFill != null)
            {
                _healthFill.fillAmount = _health.Normalized;
                _healthFill.color = _health.Normalized < 0.3f
                    ? new Color(0.9f, 0.22f, 0.2f)
                    : new Color(0.25f, 0.85f, 0.45f);
            }

            // crosshair opens up as the cone widens
            if (_crosshair != null && _loadout != null && _loadout.Active != null)
            {
                float spread = _loadout.Active.CurrentSpread;
                float px = Mathf.Clamp(6f + spread * 7f, 6f, 90f);
                _crosshair.sizeDelta = new Vector2(px, px);

                var w = _loadout.Active;
                if (_scope != null) _scope.enabled = w.IsAiming && w.Definition.isSniper;
            }

            // hit marker
            if (_hitText != null)
            {
                if (_hitMarkerTime > 0f)
                {
                    _hitMarkerTime -= Time.deltaTime;
                    var c = _hitColor;
                    c.a = Mathf.Clamp01(_hitMarkerTime * 4f);
                    _hitText.color = c;
                }
            }
        }

        public void OnShotResolved(HitZone zone, float damage, bool lethal)
        {
            if (_hitText == null) return;
            _hitColor = zone == HitZone.Head
                ? new Color(1f, 0.35f, 0.15f, 1f)
                : new Color(1f, 1f, 1f, 1f);
            _hitMarkerTime = lethal ? 0.6f : 0.3f;

            string label = zone == HitZone.Head ? "HEADSHOT" : zone == HitZone.Legs ? "LEG" : "HIT";
            if (lethal) label += "  + ELIMINATED";
            _hitText.text = label;
        }

        public void SetWave(int wave, int alive) { if (_waveText != null) _waveText.text = "WAVE " + wave + "   ENEMIES " + alive; }

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        private static Font GetFont()
        {
            // Unity renamed the built-in font to LegacyRuntime.ttf in 2022+.
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var font = GetFont();

            // ---- health bar (bottom left) ----
            _healthBg = NewImage(canvasGo.transform, "HealthBg", new Color(0, 0, 0, 0.45f));
            SetRect(_healthBg.rectTransform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(320, 26));
            _healthFill = NewImage(_healthBg.transform, "Fill", new Color(0.25f, 0.85f, 0.45f));
            SetRect(_healthFill.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), fill: true);

            // ---- ammo (bottom right) ----
            _ammoText = NewText(canvasGo.transform, "Ammo", font, 30, TextAnchor.MiddleRight, Color.white);
            SetRect(_ammoText.rectTransform, new Vector2(1, 0), new Vector2(-40, 34), new Vector2(420, 40));

            _weaponText = NewText(canvasGo.transform, "Weapon", font, 18, TextAnchor.MiddleRight, new Color(0.8f, 0.85f, 0.9f));
            SetRect(_weaponText.rectTransform, new Vector2(1, 0), new Vector2(-40, 66), new Vector2(420, 24));

            // ---- wave (top left) ----
            _waveText = NewText(canvasGo.transform, "Wave", font, 20, TextAnchor.UpperLeft, Color.white);
            SetRect(_waveText.rectTransform, new Vector2(0, 1), new Vector2(40, -34), new Vector2(420, 30));

            // ---- crosshair ----
            _crosshair = NewImage(canvasGo.transform, "Crosshair", new Color(1f, 1f, 1f, 0.9f)).rectTransform;
            _crosshair.anchorMin = _crosshair.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshair.anchoredPosition = Vector2.zero;
            _crosshair.sizeDelta = new Vector2(12, 12);
            var crossRing = NewImage(_crosshair, "Ring", new Color(1, 1, 1, 0.9f));
            crossRing.rectTransform.anchorMin = Vector2.zero; crossRing.rectTransform.anchorMax = Vector2.one;
            crossRing.rectTransform.offsetMin = new Vector2(1, 1); crossRing.rectTransform.offsetMax = new Vector2(-1, -1);

            // ---- hit marker ----
            _hitText = NewText(canvasGo.transform, "HitMarker", font, 26, TextAnchor.MiddleCenter, Color.white);
            SetRect(_hitText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(700, 40));

            // ---- sniper scope ----
            _scope = NewImage(canvasGo.transform, "Scope", new Color(0f, 0f, 0f, 0.97f));
            _scope.rectTransform.anchorMin = Vector2.zero;
            _scope.rectTransform.anchorMax = Vector2.one;
            _scope.rectTransform.offsetMin = Vector2.zero;
            _scope.rectTransform.offsetMax = Vector2.zero;
            _scope.enabled = false;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text NewText(Transform parent, string name, Font font, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size, bool fill = false)
        {
            rt.anchorMin = new Vector2(anchor.x, anchor.y);
            rt.anchorMax = new Vector2(anchor.x, anchor.y);
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            if (fill) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        }
    }
}
