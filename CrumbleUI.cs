using Il2CppTMPro;
using MelonLoader;
using RockUI;
using UnityEngine;

namespace Main
{
    // All RockUI types are nested inside RockUI.Main (i.e. Main.RockPanel, Main.RockText, etc.)
    // 'using RockUI;' brings RockUI.Main into scope as 'Main', but that conflicts with our namespace.
    // We use fully-qualified RockUI.Main.RockPanel etc. to avoid the clash.

    internal static class CrumbleUI
    {
        // ── Tunable layout constants ───────────────────────────────────────
        private const float PanelW      = 10.5f;
        private const float PanelH      = 15.5f;

        // anchorOffset Y positions (panel units, panel centre = 0)
        private const float TitleY      =  6.0f;
        private const float ModeLabelY  =  3.7f;  // name label row
        private const float ModeStateY  =  4.5f;  // state text above lever
        private const float ModeLeverY  =  3.0f;  // lever itself
        private const float ChunksLabelY =  0.5f;
        private const float ChunksValueY =  1.3f;  // value text above slider
        private const float SliderY     = -1.2f;
        private const float StyleLabelY = -3.5f;
        private const float StyleStateY = -2.7f;
        private const float StyleLeverY = -4.3f;

        // X positions
        private const float LeftColX   = -3.5f;
        private const float RightColX  =  3.5f;
        private const float CentreX    =  0.0f;

        // ── Placement constants (tunable) ──────────────────────────────────
        private const float Distance   = 3.0f;   // metres in front of player
        private const float HeightDrop = 0.3f;   // metres below eye level

        // ── State ──────────────────────────────────────────────────────────
        private static GameObject _panel;
        public  static bool IsOpen => _panel != null;

        // Live TMP references resolved after CreateFinalisedUI
        private static TextMeshPro _modeTmp;
        private static TextMeshPro _styleTmp;
        private static TextMeshPro _chunksTmp;

        // Sentinel strings used to locate the right TMP objects after build.
        // Must be unique enough that we can find them among all panel children.
        private const string ModeStateTag   = "__MODE_STATE__";
        private const string StyleStateTag  = "__STYLE_STATE__";
        private const string ChunksValueTag = "__CHUNKS_VALUE__";

        // ── Public API ─────────────────────────────────────────────────────
        public static void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public static void Close()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }
            _modeTmp   = null;
            _styleTmp  = null;
            _chunksTmp = null;
        }

        public static void Open()
        {
            if (IsOpen) return;
            try
            {
                BuildPanel();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Crumble] CrumbleUI.Open failed: {ex}");
            }
        }

        // ── Internal builder ───────────────────────────────────────────────
        private static void BuildPanel()
        {
            bool persist    = Preferences.PersistUntilSceneChange.Value;
            bool minecraft  = Preferences.ChunkStyleMinecraft.Value;
            int  chunks     = Preferences.ChunksPerBreak.Value;

            string modeState   = persist   ? "Chaos"      : "Default";
            string styleState  = minecraft ? "Minecraft"  : "Real slices";
            string chunksState = chunks.ToString();

            var panel = new RockUI.Main.RockPanel();
            panel.size = new Vector2(PanelW, PanelH);

            // ── Title ─────────────────────────────────────────────────────
            var title = new RockUI.Main.RockText();
            title.text      = "Crumble";
            title.fontSize  = 2;
            title.fontColor = Color.white;
            title.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            title.anchorOffset = new Vector2(CentreX, TitleY);
            panel.AddChildUI(title);

            // ── Mode row ──────────────────────────────────────────────────
            // Left: "Mode:" label
            var modeLabel = new RockUI.Main.RockText();
            modeLabel.text      = "Mode:";
            modeLabel.fontSize  = 1;
            modeLabel.fontColor = Color.white;
            modeLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            modeLabel.anchorOffset = new Vector2(LeftColX, ModeLabelY);
            panel.AddChildUI(modeLabel);

            // Above lever: state text (sentinel prefix so we can find it)
            var modeStateText = new RockUI.Main.RockText();
            modeStateText.text      = ModeStateTag + modeState;
            modeStateText.fontSize  = 1;
            modeStateText.fontColor = Color.cyan;
            modeStateText.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            modeStateText.anchorOffset = new Vector2(RightColX, ModeStateY);
            panel.AddChildUI(modeStateText);

            // Lever
            var modeLever = new RockUI.Main.RockLever();
            modeLever.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            modeLever.anchorOffset = new Vector2(RightColX, ModeLeverY);
            modeLever.leverToggledAction = OnModeLever;
            panel.AddChildUI(modeLever);

            // ── Chunks Per Break row ──────────────────────────────────────
            var chunksLabel = new RockUI.Main.RockText();
            chunksLabel.text      = "Chunks:";
            chunksLabel.fontSize  = 1;
            chunksLabel.fontColor = Color.white;
            chunksLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            chunksLabel.anchorOffset = new Vector2(LeftColX, ChunksLabelY);
            panel.AddChildUI(chunksLabel);

            // Above slider: value readout (sentinel prefix)
            var chunksValue = new RockUI.Main.RockText();
            chunksValue.text      = ChunksValueTag + chunksState;
            chunksValue.fontSize  = 1;
            chunksValue.fontColor = Color.yellow;
            chunksValue.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            chunksValue.anchorOffset = new Vector2(RightColX, ChunksValueY);
            panel.AddChildUI(chunksValue);

            var chunksSlider = new RockUI.Main.RockSlider();
            chunksSlider.useSteps  = true;
            chunksSlider.stepCount = 10;
            chunksSlider.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            chunksSlider.anchorOffset = new Vector2(CentreX, SliderY);
            chunksSlider.stepReachedAction = OnChunksSlider;
            panel.AddChildUI(chunksSlider);

            // ── Style row ─────────────────────────────────────────────────
            var styleLabel = new RockUI.Main.RockText();
            styleLabel.text      = "Style:";
            styleLabel.fontSize  = 1;
            styleLabel.fontColor = Color.white;
            styleLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            styleLabel.anchorOffset = new Vector2(LeftColX, StyleLabelY);
            panel.AddChildUI(styleLabel);

            // Above lever: state text (sentinel prefix)
            var styleStateText = new RockUI.Main.RockText();
            styleStateText.text      = StyleStateTag + styleState;
            styleStateText.fontSize  = 1;
            styleStateText.fontColor = Color.cyan;
            styleStateText.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            styleStateText.anchorOffset = new Vector2(RightColX, StyleStateY);
            panel.AddChildUI(styleStateText);

            // Lever
            var styleLever = new RockUI.Main.RockLever();
            styleLever.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            styleLever.anchorOffset = new Vector2(RightColX, StyleLeverY);
            styleLever.leverToggledAction = OnStyleLever;
            panel.AddChildUI(styleLever);

            // ── Finalise (followPlayer:false — we position manually) ──────
            _panel = RockUI.Main.RockUI.CreateFinalisedUI(panel, followPlayer: false);
            MelonLogger.Msg("[Crumble] Settings panel built.");

            // ── Resolve live TMP references by sentinel prefix ────────────
            ResolveTMPRefs();

            // ── Strip sentinels from the displayed text ───────────────────
            SetText(_modeTmp,   modeState);
            SetText(_styleTmp,  styleState);
            SetText(_chunksTmp, chunksState);

            // ── Position in front of the player's headset ─────────────────
            PositionPanel();
        }

        // After CreateFinalisedUI the RockText GameObjects exist in the scene as
        // children of _panel. We created them with sentinel-prefixed text so we can
        // find the exact TMP component among all panel children.
        //
        // NOTE: RockUI.Main.RockText has no GetGameObject() — that is a RumbleModdingAPI
        // scene-path helper for hardcoded in-game objects. For elements we build ourselves
        // we must search the panel's children by initial TMP text content.
        private static void ResolveTMPRefs()
        {
            if (_panel == null) return;
            var tmps = _panel.GetComponentsInChildren<TextMeshPro>(true);
            if (tmps == null) return;
            foreach (var tmp in tmps)
            {
                if (tmp == null) continue;
                string t = tmp.text ?? string.Empty;
                if      (t.StartsWith(ModeStateTag))   _modeTmp   = tmp;
                else if (t.StartsWith(StyleStateTag))  _styleTmp  = tmp;
                else if (t.StartsWith(ChunksValueTag)) _chunksTmp = tmp;
            }
            if (_modeTmp   == null) MelonLogger.Warning("[Crumble] Could not find mode-state TMP.");
            if (_styleTmp  == null) MelonLogger.Warning("[Crumble] Could not find style-state TMP.");
            if (_chunksTmp == null) MelonLogger.Warning("[Crumble] Could not find chunks-value TMP.");
        }

        private static void PositionPanel()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Flatten forward vector to horizontal plane so the panel doesn't tilt with head pitch.
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;

            Vector3 pos = cam.transform.position + fwd * Distance;
            pos.y -= HeightDrop;

            _panel.transform.position = pos;

            // Face the panel toward the player.
            // CreateFinalisedUI with followPlayer:false does NOT rotate the object, so we
            // point its local forward in the same direction the camera looks (panel faces player).
            // If text appears mirrored/backwards in-game, add * Quaternion.Euler(0, 180, 0) here.
            _panel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // ── Input handlers ─────────────────────────────────────────────────
        private static void OnModeLever(int v)
        {
            bool chaos = (v == 1);
            Preferences.PersistUntilSceneChange.Value = chaos;
            SetText(_modeTmp, chaos ? "Chaos" : "Default");
            MelonPreferences.Save();
        }

        private static void OnChunksSlider(int step)
        {
            int count = step + 1; // step 0..9 → 1..10
            Preferences.ChunksPerBreak.Value = count;
            SetText(_chunksTmp, count.ToString());
            MelonPreferences.Save();
        }

        private static void OnStyleLever(int v)
        {
            bool mc = (v == 1);
            Preferences.ChunkStyleMinecraft.Value = mc;
            SetText(_styleTmp, mc ? "Minecraft" : "Real slices");
            MelonPreferences.Save();
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static void SetText(TextMeshPro tmp, string s)
        {
            if (tmp == null) return;
            try { tmp.text = s; }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Crumble] SetText failed: {ex.Message}");
            }
        }
    }
}
