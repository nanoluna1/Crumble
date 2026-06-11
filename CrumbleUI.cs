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
        private const float PanelW      = 13.0f;
        private const float PanelH      = 23.0f;

        // Six rows evenly spread across the panel.
        // Title near top; six rows at ~8, 5, 2, -1, -4, -7.
        //
        // Row structure (lever rows): label at rowY, state above (+0.8), lever below (-0.8).
        // Row structure (slider rows): label at rowY, value above (+0.8), slider below (-0.8).

        private const float TitleY       = 10.5f;

        // Row 1 — Mode (lever)
        private const float ModeLabelY   =  8.0f;
        private const float ModeStateY   =  8.8f;
        private const float ModeLeverY   =  7.2f;

        // Row 2 — Slices Per Structure (slider)
        private const float ChunksLabelY =  5.0f;
        private const float ChunksValueY =  5.8f;
        private const float ChunksSliderY=  4.2f;

        // Row 3 — Chunk Style (lever)
        private const float StyleLabelY  =  2.0f;
        private const float StyleStateY  =  2.8f;
        private const float StyleLeverY  =  1.2f;

        // Row 4 — Rock Cam Visibility (lever)
        private const float RockLabelY   = -1.0f;
        private const float RockStateY   = -0.2f;
        private const float RockLeverY   = -1.8f;

        // Row 5 — Chunk Size (slider)
        private const float SizeLabelY   = -4.0f;
        private const float SizeValueY   = -3.2f;
        private const float SizeSliderY  = -4.8f;

        // Row 6 — Time Till Despawn (slider)
        private const float TimeLabelY   = -7.0f;
        private const float TimeValueY   = -6.2f;
        private const float TimeSliderY  = -7.8f;

        // X positions
        private const float LeftColX   = -3.5f;
        private const float RightColX  =  3.5f;
        private const float CentreX    =  0.0f;

        // ── Chunk Limit slab (second panel, to the right) ──────────────────
        // Landscape: wider than tall, and smaller than the main 13x23 panel.
        private const float LimitPanelW  = 10.0f;
        private const float LimitPanelH  =  6.0f;
        private const float LimitTitleY  =  1.8f;   // "Chunk Limit" title near top
        private const float LimitValueY  =  0.1f;   // numeric readout
        private const float LimitSliderY = -1.6f;   // slider below
        private const float LimitYaw     = 20.0f;   // degrees yawed toward the player (flip sign to reverse)

        // ── Placement constants (tunable) ──────────────────────────────────
        private const float Distance   = 3.0f;   // metres in front of player
        private const float HeightDrop = 0.3f;   // metres below eye level

        // ── State ──────────────────────────────────────────────────────────
        private static GameObject _panel;
        public  static bool IsOpen => _panel != null;

        // True while the panel is being built AND during the deferred control sync. Handlers
        // ignore fires while suppressed, so programmatic SetStep / init can never overwrite a
        // saved pref (this is what corrupted ChunksPerBreak to 1) or trigger spurious pre-warms.
        private static bool _suppress;

        // Live TMP references resolved after CreateFinalisedUI
        private static TextMeshPro _modeTmp;
        private static TextMeshPro _styleTmp;
        private static TextMeshPro _chunksTmp;
        private static TextMeshPro _rockTmp;
        private static TextMeshPro _sizeTmp;
        private static TextMeshPro _timeTmp;

        // The Style lever, cached so Slices=1 can physically flip it to Minecraft (and back).
        private static Il2CppRUMBLE.Interactions.InteractionBase.InteractionLever _styleLever;

        // Style-lever-specific suppression. Moving the lever with SetStep fires its handler a FEW
        // frames later (not synchronously), after a plain _suppress wrapper has already reset — which
        // is what corrupted the style pref. So when we flip the lever to a value that differs from
        // the saved pref, we hold this flag for a window of frames to swallow that late callback.
        // It's separate from _suppress so it doesn't block the other controls.
        private static bool _suppressStyle;
        private static int  _styleSyncToken;       // newest deferred-flip owns the suppression window
        private static int  _lastStyleStep = -1;   // avoid redundant SetSteps / flip churn

        // Second slab: the "Chunk Limit" panel (its own RockPanel, placed to the right of _panel).
        private static GameObject  _limitPanel;
        private static TextMeshPro _limitTmp;      // numeric readout
        private static int         _lastLimit = -1; // debounce disk writes while dragging

        // Sentinel strings used to locate the right TMP objects after build.
        // Must be unique enough that we can find them among all panel children.
        private const string ModeStateTag      = "__MODE_STATE__";
        private const string StyleStateTag     = "__STYLE_STATE__";
        private const string ChunksValueTag    = "__CHUNKS_VALUE__";
        private const string RockCamStateTag   = "__ROCKCAM_STATE__";
        private const string ChunkSizeValueTag = "__CHUNKSIZE_VALUE__";
        private const string TimeDespawnValueTag = "__TIMEDESPAWN_VALUE__";
        private const string ChunkLimitValueTag = "__CHUNKLIMIT_VALUE__";

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
            if (_limitPanel != null)
            {
                UnityEngine.Object.Destroy(_limitPanel);
                _limitPanel = null;
            }
            _limitTmp  = null;
            _lastLimit = -1;
            _modeTmp   = null;
            _styleTmp  = null;
            _chunksTmp = null;
            _rockTmp   = null;
            _sizeTmp   = null;
            _timeTmp   = null;
            _styleLever    = null;
            _suppressStyle = false;
            _lastStyleStep = -1;
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
                Main.Logger.Error($"CrumbleUI.Open failed: {ex}");
            }
        }

        // ── Internal builder ───────────────────────────────────────────────
        private static void BuildPanel()
        {
            _suppress = true; // block handlers until the deferred sync finishes

            bool persist    = Preferences.PersistUntilSceneChange.Value;
            bool minecraft  = Preferences.ChunkStyleMinecraft.Value;
            int  chunks     = Preferences.ChunksPerBreak.Value;
            bool rockCam    = Preferences.RockCamVisibility.Value;
            int  chunkSize  = Preferences.ChunkSize.Value;
            int  timeLife   = (int)Preferences.ChunkLifetime.Value;

            // Slices=1 can't produce real slices (TrySlice needs >=2), so it always falls back to
            // Minecraft shards. Show that honestly without overwriting the saved style pref.
            bool effMinecraft = minecraft || chunks == 1;

            string modeState   = persist      ? "Chaos"      : "Default";
            string styleState  = effMinecraft ? "Minecraft"  : "Real slices";
            string chunksState = chunks.ToString();
            string rockState   = rockCam   ? "True"       : "False";
            string sizeState   = chunkSize.ToString();
            string timeState   = timeLife.ToString();

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

            // ── Slices Per Structure row ──────────────────────────────────
            var chunksLabel = new RockUI.Main.RockText();
            chunksLabel.text      = "Slices Per Structure";
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
            chunksSlider.anchorOffset = new Vector2(CentreX, ChunksSliderY);
            chunksSlider.stepReachedAction = OnChunksSlider;
            panel.AddChildUI(chunksSlider);

            // ── Style row ─────────────────────────────────────────────────
            var styleLabel = new RockUI.Main.RockText();
            styleLabel.text      = "Chunk Style";
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

            // ── Rock Cam row ──────────────────────────────────────────────
            var rockLabel = new RockUI.Main.RockText();
            rockLabel.text      = "Rock Cam Visibility";
            rockLabel.fontSize  = 1;
            rockLabel.fontColor = Color.white;
            rockLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            rockLabel.anchorOffset = new Vector2(LeftColX, RockLabelY);
            panel.AddChildUI(rockLabel);

            // Above lever: state text (sentinel prefix)
            var rockStateText = new RockUI.Main.RockText();
            rockStateText.text      = RockCamStateTag + rockState;
            rockStateText.fontSize  = 1;
            rockStateText.fontColor = Color.cyan;
            rockStateText.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            rockStateText.anchorOffset = new Vector2(RightColX, RockStateY);
            panel.AddChildUI(rockStateText);

            // Lever
            var rockLever = new RockUI.Main.RockLever();
            rockLever.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            rockLever.anchorOffset = new Vector2(RightColX, RockLeverY);
            rockLever.leverToggledAction = OnRockCamLever;
            panel.AddChildUI(rockLever);

            // ── Chunk Size row ────────────────────────────────────────────
            var sizeLabel = new RockUI.Main.RockText();
            sizeLabel.text      = "Chunk Size";
            sizeLabel.fontSize  = 1;
            sizeLabel.fontColor = Color.white;
            sizeLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            sizeLabel.anchorOffset = new Vector2(LeftColX, SizeLabelY);
            panel.AddChildUI(sizeLabel);

            // Above slider: value readout (sentinel prefix)
            var sizeValue = new RockUI.Main.RockText();
            sizeValue.text      = ChunkSizeValueTag + sizeState;
            sizeValue.fontSize  = 1;
            sizeValue.fontColor = Color.yellow;
            sizeValue.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            sizeValue.anchorOffset = new Vector2(RightColX, SizeValueY);
            panel.AddChildUI(sizeValue);

            var sizeSlider = new RockUI.Main.RockSlider();
            sizeSlider.useSteps  = true;
            sizeSlider.stepCount = 10;
            sizeSlider.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            sizeSlider.anchorOffset = new Vector2(CentreX, SizeSliderY);
            sizeSlider.stepReachedAction = OnChunkSizeSlider;
            panel.AddChildUI(sizeSlider);

            // ── Time Till Despawn row (Default mode only) ─────────────────
            // In Chaos chunks never despawn on a timer, so the row is meaningless there. We BUILD it
            // conditionally rather than hide it after the fact: the continuous slider's visual mesh
            // lives in a different part of the hierarchy than its interaction component, so no
            // SetActive on a single GameObject reliably hides it. Toggling Mode rebuilds the panel.
            if (!persist)
            {
                var timeLabel = new RockUI.Main.RockText();
                timeLabel.text      = "Time Till Despawn";
                timeLabel.fontSize  = 1;
                timeLabel.fontColor = Color.white;
                timeLabel.anchor    = RockUI.Main.UIElement.AnchorType.Center;
                timeLabel.anchorOffset = new Vector2(LeftColX, TimeLabelY);
                panel.AddChildUI(timeLabel);

                // Above slider: value readout (sentinel prefix)
                var timeValue = new RockUI.Main.RockText();
                timeValue.text      = TimeDespawnValueTag + timeState;
                timeValue.fontSize  = 1;
                timeValue.fontColor = Color.yellow;
                timeValue.anchor    = RockUI.Main.UIElement.AnchorType.Center;
                timeValue.anchorOffset = new Vector2(RightColX, TimeValueY);
                panel.AddChildUI(timeValue);

                // CONTINUOUS (no steps): 20 discrete detents are crammed into the slider's fixed
                // rotation arc and become impossible to land on. A smooth 0..1 value mapped to 1..20
                // moves freely.
                var timeSlider = new RockUI.Main.RockSlider();
                timeSlider.useSteps  = false;
                timeSlider.anchor    = RockUI.Main.UIElement.AnchorType.Center;
                timeSlider.anchorOffset = new Vector2(CentreX, TimeSliderY);
                timeSlider.valueChangedAction = OnTimeDespawnValue;
                panel.AddChildUI(timeSlider);
            }

            // ── Finalise (followPlayer:false — we position manually) ──────
            _panel = RockUI.Main.RockUI.CreateFinalisedUI(panel, followPlayer: false);
            Main.Logger.Msg("Settings panel built.");

            // ── Resolve live TMP references by sentinel prefix ────────────
            ResolveTMPRefs();

            // ── Resolve control objects we manipulate after build ─────────
            ResolveControlRefs();

            // ── Strip sentinels from the displayed text ───────────────────
            SetText(_modeTmp,   modeState);
            SetText(_styleTmp,  styleState);
            SetText(_chunksTmp, chunksState);
            SetText(_rockTmp,   rockState);
            SetText(_sizeTmp,   sizeState);
            SetText(_timeTmp,   timeState); // null in Chaos (row not built) — SetText no-ops on null

            // ── Build the Chunk Limit slab (separate panel, placed to the right) ──
            BuildLimitPanel();

            // ── Defer sync so it runs AFTER lever/slider Start() resets ───
            MelonLoader.MelonCoroutines.Start(SyncControlStatesDeferred());

            // ── Position in front of the player's headset (both slabs) ────
            PositionPanel();
        }

        // The "Chunk Limit" slab is its own RockPanel (CreateFinalisedUI builds an independent UI
        // root), so it has no levers/sliders to step-sync. Like the despawn slider it's a CONTINUOUS
        // slider (0..1 → 1..500); its handle starts at min while the readout shows the saved value.
        private static void BuildLimitPanel()
        {
            int    limit      = Preferences.MaxConcurrentDebris.Value;
            string limitState = limit.ToString();

            var lp = new RockUI.Main.RockPanel();
            lp.size = new Vector2(LimitPanelW, LimitPanelH);

            var title = new RockUI.Main.RockText();
            title.text      = "Chunk Limit";
            title.fontSize  = 1;
            title.fontColor = Color.white;
            title.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            title.anchorOffset = new Vector2(CentreX, LimitTitleY);
            lp.AddChildUI(title);

            var value = new RockUI.Main.RockText();
            value.text      = ChunkLimitValueTag + limitState;
            value.fontSize  = 1;
            value.fontColor = Color.yellow;
            value.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            value.anchorOffset = new Vector2(CentreX, LimitValueY);
            lp.AddChildUI(value);

            var slider = new RockUI.Main.RockSlider();
            slider.useSteps  = false;
            slider.anchor    = RockUI.Main.UIElement.AnchorType.Center;
            slider.anchorOffset = new Vector2(CentreX, LimitSliderY);
            slider.valueChangedAction = OnChunkLimitValue;
            lp.AddChildUI(slider);

            _limitPanel = RockUI.Main.RockUI.CreateFinalisedUI(lp, followPlayer: false);

            // Resolve the readout TMP within THIS panel (ResolveTMPRefs only searches _panel).
            var tmps = _limitPanel != null ? _limitPanel.GetComponentsInChildren<TextMeshPro>(true) : null;
            if (tmps != null)
            {
                foreach (var tmp in tmps)
                {
                    if (tmp == null) continue;
                    tmp.enableWordWrapping = false;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    if ((tmp.text ?? string.Empty).StartsWith(ChunkLimitValueTag)) _limitTmp = tmp;
                }
            }
            if (_limitTmp == null) Main.Logger.Warning("Could not find chunk-limit-value TMP.");
            SetText(_limitTmp, limitState);

            int rendCount = _limitPanel != null ? _limitPanel.GetComponentsInChildren<Renderer>(true).Length : 0;
            Main.Logger.Msg($"Limit panel built: created={_limitPanel != null}, tmp={_limitTmp != null}, renderers={rendCount}, value={limitState}.");
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
                // The default text boxes are narrow, so "10" and longer words wrapped onto two
                // lines. Disable wrapping so text uses the (now wider) panel space on one line.
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                string t = tmp.text ?? string.Empty;
                if      (t.StartsWith(ModeStateTag))        _modeTmp   = tmp;
                else if (t.StartsWith(StyleStateTag))       _styleTmp  = tmp;
                else if (t.StartsWith(ChunksValueTag))      _chunksTmp = tmp;
                else if (t.StartsWith(RockCamStateTag))     _rockTmp   = tmp;
                else if (t.StartsWith(ChunkSizeValueTag))   _sizeTmp   = tmp;
                else if (t.StartsWith(TimeDespawnValueTag)) _timeTmp   = tmp;
            }
            if (_modeTmp   == null) Main.Logger.Warning("Could not find mode-state TMP.");
            if (_styleTmp  == null) Main.Logger.Warning("Could not find style-state TMP.");
            if (_chunksTmp == null) Main.Logger.Warning("Could not find chunks-value TMP.");
            if (_rockTmp   == null) Main.Logger.Warning("Could not find rock-cam-state TMP.");
            if (_sizeTmp   == null) Main.Logger.Warning("Could not find chunk-size-value TMP.");
            // _timeTmp is intentionally absent in Chaos mode (the Time row isn't built), so no warning.
        }

        // Cache the Style lever (creation order: levers = [Mode, Style, RockCam]) so we can flip it.
        private static void ResolveControlRefs()
        {
            if (_panel == null) return;
            try
            {
                var levers = _panel.GetComponentsInChildren<Il2CppRUMBLE.Interactions.InteractionBase.InteractionLever>(true);
                _styleLever = (levers != null && levers.Length > 1) ? levers[1] : null;
                Preferences.Log($"ResolveControlRefs: styleLever={(_styleLever != null)}.");
            }
            catch (System.Exception ex) { Main.Logger.Warning($"ResolveControlRefs failed: {ex.Message}"); }
        }

        // Effective style: Minecraft if the user picked it OR slices==1 (real slices need >=2).
        private static bool EffectiveMinecraft()
            => Preferences.ChunkStyleMinecraft.Value || Preferences.ChunksPerBreak.Value <= 1;

        // Update the Style text AND physically flip the lever to the effective style — WITHOUT
        // touching the saved ChunkStyleMinecraft pref. The flip uses a held suppression window so the
        // lever's late callback can't write the pref, so dropping to Slices=1 shows Minecraft and
        // returning to >=2 restores the user's real choice, lever and text together.
        private static void RefreshStyleDisplay()
        {
            bool eff = EffectiveMinecraft();
            SetText(_styleTmp, eff ? "Minecraft" : "Real slices");
            MoveStyleLever(eff ? 1 : 0);
        }

        // Flip the Style lever to a target step. Skips redundant flips, and holds _suppressStyle for a
        // window of frames so the InteractionLever's delayed toggle callback (which fires a few frames
        // AFTER SetStep) is swallowed instead of overwriting the saved style pref.
        private static void MoveStyleLever(int step)
        {
            if (_styleLever == null || step == _lastStyleStep) return;
            _lastStyleStep = step;
            MelonLoader.MelonCoroutines.Start(FlipStyleLeverDeferred(step));
        }

        private static System.Collections.IEnumerator FlipStyleLeverDeferred(int step)
        {
            int token = ++_styleSyncToken;
            _suppressStyle = true;
            SetStepSafe(_styleLever, step);
            // Hold long enough to cover the lever's delayed callback (well beyond the ~1-2 frame lag).
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                if (_styleSyncToken != token) yield break; // a newer flip owns the window now
            }
            if (_styleSyncToken == token) _suppressStyle = false;
        }

        // Rebuild the panel in place (same world pose) so the Time row can be added/removed when Mode
        // changes. Deferred one frame so the Mode lever's own callback finishes before we destroy it.
        private static System.Collections.IEnumerator RebuildInPlace()
        {
            yield return null;
            if (_panel == null) yield break; // user closed the panel during the wait — don't reopen
            Vector3 pos = _panel.transform.position;
            Quaternion rot = _panel.transform.rotation;
            Close();
            Open();
            if (_panel != null)
            {
                _panel.transform.position = pos;
                _panel.transform.rotation = rot;
                PositionLimitPanel(); // re-attach the limit slab to the restored main-panel pose
            }
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

            PositionLimitPanel();
        }

        // Place the Chunk Limit slab just to the right of the main panel, same facing. We measure the
        // ACTUAL rendered half-width of each panel along the panel's right axis (RockUI bakes size into
        // the mesh, so transform.lossyScale is unreliable) and offset by mainHalf + gap + limitHalf.
        // If it lands on the wrong side, negate `offset`.
        private static void PositionLimitPanel()
        {
            if (_limitPanel == null || _panel == null) return;
            try
            {
                Vector3 right = _panel.transform.right;
                float mainHalf  = HalfWidthAlong(_panel,      right);
                float limitHalf = HalfWidthAlong(_limitPanel, right);
                const float gapM = 0.04f; // metres between the two slabs
                float offset = mainHalf + gapM + limitHalf;

                // Position beside the main panel, then yaw the slab toward the player (cockpit wrap).
                _limitPanel.transform.position = _panel.transform.position + right * offset;
                _limitPanel.transform.rotation = _panel.transform.rotation * Quaternion.Euler(0f, LimitYaw, 0f);

                Main.Logger.Msg($"LimitPos: mainHalf={mainHalf:0.000} limitHalf={limitHalf:0.000} " +
                                $"offset={offset:0.000} lossyScale={_panel.transform.lossyScale.x:0.000} " +
                                $"limitPos={_limitPanel.transform.position}");
            }
            catch (System.Exception ex) { Main.Logger.Warning($"PositionLimitPanel failed: {ex.Message}"); }
        }

        // Largest distance from a panel's origin to its renderers, measured along `axis` (world space).
        // Projects every renderer-bounds corner onto the axis so it's correct regardless of facing.
        private static float HalfWidthAlong(GameObject go, Vector3 axis)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return 0f;
            Vector3 origin = go.transform.position;
            float max = 0f;
            foreach (var r in rends)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                Vector3 c = b.center, e = b.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                     (i & 2) == 0 ? -e.y : e.y,
                                                     (i & 4) == 0 ? -e.z : e.z);
                    float proj = Mathf.Abs(Vector3.Dot(corner - origin, axis));
                    if (proj > max) max = proj;
                }
            }
            return max;
        }

        // ── Input handlers ─────────────────────────────────────────────────
        private static void OnModeLever(int v)
        {
            if (_suppress) return;
            bool chaos = (v == 1);
            // Ignore no-op fires (e.g. the lever-sync echo after a rebuild) — otherwise the rebuild
            // below would re-trigger this handler and loop forever.
            if (chaos == Preferences.PersistUntilSceneChange.Value) return;
            Preferences.PersistUntilSceneChange.Value = chaos;
            SetText(_modeTmp, chaos ? "Chaos" : "Default");
            MelonPreferences.Save();
            // The Time Till Despawn row only exists in Default mode, so rebuild the panel in place to
            // add/remove it (the continuous slider can't be reliably hidden via SetActive).
            MelonLoader.MelonCoroutines.Start(RebuildInPlace());
        }

        private static void OnChunksSlider(int step)
        {
            if (_suppress) return;
            int count = step + 1; // step 0..9 → 1..10
            Preferences.ChunksPerBreak.Value = count;
            SetText(_chunksTmp, count.ToString());
            // Slices=1 forces Minecraft (no real slicing possible); reflect that on the Style
            // lever/text, and restore the user's real choice when they go back to >=2.
            RefreshStyleDisplay();
            MelonPreferences.Save();
            MelonLoader.MelonCoroutines.Start(ChunkCache.PrewarmCurrent());
        }

        private static void OnStyleLever(int v)
        {
            // _suppressStyle gates the lever's own delayed callback during a programmatic flip, so it
            // can't write the pref. _suppress gates build/sync.
            if (_suppress || _suppressStyle) return;
            bool mc = (v == 1);
            // Record the user's genuine choice. The DISPLAY still honours the Slices=1 override:
            // at Slices=1 real slices are impossible, so RefreshStyleDisplay snaps the lever back to
            // Minecraft; at >=2 it shows exactly what the user picked.
            Preferences.ChunkStyleMinecraft.Value = mc;
            _lastStyleStep = v; // the lever is physically here now; only re-flip if effective differs
            RefreshStyleDisplay();
            MelonPreferences.Save();
            MelonLoader.MelonCoroutines.Start(ChunkCache.PrewarmCurrent());
        }

        private static void OnRockCamLever(int v)
        {
            if (_suppress) return;
            Preferences.RockCamVisibility.Value = (v == 1);
            SetText(_rockTmp, v == 1 ? "True" : "False");
            DebrisLayers.EnsureCamerasSeeDebris();
            MelonPreferences.Save();
        }

        private static void OnChunkSizeSlider(int step)
        {
            if (_suppress) return;
            int size = step + 1; // step 0..9 → 1..10
            Preferences.ChunkSize.Value = size;
            SetText(_sizeTmp, size.ToString());
            MelonPreferences.Save();
        }

        private static int _lastDespawn = -1;
        private static void OnTimeDespawnValue(float v)
        {
            if (_suppress) return;
            int seconds = Mathf.Clamp(Mathf.RoundToInt(1f + v * 19f), 1, 20); // 0..1 → 1..20
            Preferences.ChunkLifetime.Value = seconds;
            SetText(_timeTmp, seconds.ToString());
            // onNormalizedValueChanged fires continuously while dragging — only write to disk
            // when the whole-second value actually changes, to avoid hammering Save().
            if (seconds != _lastDespawn) { _lastDespawn = seconds; MelonPreferences.Save(); }
        }

        private static void OnChunkLimitValue(float v)
        {
            if (_suppress) return;
            int limit = Mathf.Clamp(Mathf.RoundToInt(1f + v * 499f), 1, 500); // 0..1 → 1..500
            Preferences.MaxConcurrentDebris.Value = limit;
            SetText(_limitTmp, limit.ToString());
            // Debounced save: fires continuously while dragging, write only on a whole-value change.
            if (limit != _lastLimit) { _lastLimit = limit; MelonPreferences.Save(); }
        }

        // ── Lever/slider sync ──────────────────────────────────────────────

        // Deferred so it runs after lever/slider Start() methods reset to step 0.
        public static System.Collections.IEnumerator SyncControlStatesDeferred()
        {
            // Wait for the lever/slider components to finish their own Start() (which resets
            // them to step 0) before we set them to the saved values.
            for (int i = 0; i < 15; i++) yield return null;
            if (_panel == null) { _suppress = false; yield break; }
            SyncControlStates();
            // Re-resolve (the Style lever) now that controls are instantiated, then set the Style
            // text + flip the lever to the EFFECTIVE style via the suppressed flip. We start the flip
            // BEFORE clearing _suppress so _suppressStyle is already up to swallow the sync's own
            // delayed lever echo (which would otherwise write the pref).
            ResolveControlRefs();
            RefreshStyleDisplay();
            _suppress = false; // real user interaction can now write prefs
        }

        // Levers/sliders always spawn at step 0; set them to match the saved settings so the
        // physical handle matches the state text after a relaunch. Controls are found in
        // creation order: levers = [Mode, Style, RockCam]; sliders = [Slices, ChunkSize, TimeDespawn].
        private static void SyncControlStates()
        {
            try
            {
                var levers = _panel.GetComponentsInChildren<Il2CppRUMBLE.Interactions.InteractionBase.InteractionLever>(true);
                if (levers != null)
                {
                    if (levers.Length > 0) SetStepSafe(levers[0], Preferences.PersistUntilSceneChange.Value ? 1 : 0);
                    // levers[1] (Style) is intentionally NOT set here — RefreshStyleDisplay() flips it to
                    // the EFFECTIVE style right after, via the suppressed flip that can't corrupt the pref.
                    if (levers.Length > 2) SetStepSafe(levers[2], Preferences.RockCamVisibility.Value       ? 1 : 0);
                }
                var sliders = _panel.GetComponentsInChildren<Il2CppRUMBLE.Interactions.InteractionBase.InteractionSlider>(true);
                if (sliders != null)
                {
                    // index 0 = Slices Per Structure (ChunksPerBreak, 1..10, step 0..9)
                    if (sliders.Length > 0) SetStepSafe(sliders[0], Mathf.Clamp(Preferences.ChunksPerBreak.Value - 1, 0, 9));
                    // index 1 = Chunk Size (1..10, step 0..9)
                    if (sliders.Length > 1) SetStepSafe(sliders[1], Mathf.Clamp(Preferences.ChunkSize.Value      - 1, 0, 9));
                    // index 2 = Time Till Despawn is now a CONTINUOUS slider (no steps), so it
                    // is not step-synced here; its value readout shows the saved seconds.
                }
                int _levCount = levers != null ? levers.Length : 0;
                int _sldCount = sliders != null ? sliders.Length : 0;
                Preferences.Log($"Synced {_levCount} levers / {_sldCount} sliders to saved settings.");
            }
            catch (System.Exception ex) { Main.Logger.Warning($"SyncControlStates failed: {ex.Message}"); }
        }

        private static void SetStepSafe(Il2CppRUMBLE.Interactions.InteractionBase.InteractionNumericalBase ctrl, int step)
        {
            if (ctrl == null) return;
            try { ctrl.SetStep(step, false, false); }
            catch (System.Exception ex) { Main.Logger.Warning($"SetStep failed: {ex.Message}"); }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static void SetText(TextMeshPro tmp, string s)
        {
            if (tmp == null) return;
            try { tmp.text = s; }
            catch (System.Exception ex)
            {
                Main.Logger.Error($"SetText failed: {ex.Message}");
            }
        }
    }
}
