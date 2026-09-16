using UnityEngine;
using UnityEngine.UI;

namespace CreatureClimb
{
    /// <summary>
    /// Main-menu screen: mode list, Easy/Hard toggle, Race opponent, Time Trial durations.
    /// </summary>
    public class GameModeMenu : MonoBehaviour
    {
        static readonly Color EasyTrack = new Color(0.22f, 0.78f, 0.40f, 1f);
        static readonly Color HardTrack = new Color(0.90f, 0.28f, 0.28f, 1f);
        static readonly Color KnobFace = Color.white;
        static readonly Color LabelOn = Color.white;

        // Classic phone toggle proportions (oval + sliding dot).
        const float TrackWidth = 280f;
        const float TrackHeight = 108f;
        const float KnobSize = 88f;
        const float KnobPad = 10f;
        const int LabelFont = 48;

        static Sprite s_circleSprite;
        static Sprite s_pixelSprite;

        [SerializeField] GameObject root;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Button classicButton;
        [SerializeField] Button blackoutButton;
        [SerializeField] Button raceButton;
        [SerializeField] Button combatantButton;
        [SerializeField] Button timeTrialButton;
        [SerializeField] Button tempoButton;
        [SerializeField] Button closeButton;

        [Header("Difficulty oval toggle")]
        [SerializeField] Button difficultySlide;
        [SerializeField] Image slideTrack;
        [SerializeField] Image slideTrackLeft;
        [SerializeField] Image slideTrackRight;
        [SerializeField] Image slideKnob;
        [SerializeField] Text slideLabel;
        [SerializeField] RectTransform slideKnobRect;

        [Header("Race opponent")]
        [SerializeField] GameObject opponentRow;
        [SerializeField] Button opponentPrev;
        [SerializeField] Button opponentNext;
        [SerializeField] Image opponentIcon;
        [SerializeField] Text opponentLabel;

        [Header("Time Trial duration")]
        [SerializeField] GameObject durationRow;
        [SerializeField] Button duration90Button;
        [SerializeField] Button duration120Button;
        [SerializeField] Button duration200Button;

        bool open;
        bool hardSelected;
        float knobT;

        public bool IsOpen => open;

        public bool IsConfigured =>
            classicButton != null &&
            blackoutButton != null &&
            raceButton != null &&
            combatantButton != null &&
            timeTrialButton != null &&
            tempoButton != null &&
            difficultySlide != null &&
            slideTrack != null &&
            slideTrackLeft != null &&
            slideTrackRight != null &&
            slideKnob != null &&
            slideLabel != null &&
            durationRow != null &&
            duration90Button != null &&
            duration120Button != null &&
            duration200Button != null;

        /// <summary>
        /// True when duration chips are still full-width menu buttons that steal clicks.
        /// </summary>
        public bool NeedsDurationHitBoxFix()
        {
            if (!IsConfigured) return true;
            return IsOversizedDurationHitBox(duration90Button)
                || IsOversizedDurationHitBox(duration120Button)
                || IsOversizedDurationHitBox(duration200Button);
        }

        static bool IsOversizedDurationHitBox(Button button)
        {
            if (button == null) return true;
            var rect = button.transform as RectTransform;
            if (rect == null) return true;
            // Compact chips are ~160 wide; leftover CreateButton defaults are 560.
            return rect.sizeDelta.x > 220f;
        }

        public void Setup(
            GameObject panelRoot,
            Text title,
            Text subtitle,
            Button classic,
            Button blackout,
            Button race,
            Button combatant,
            Button timeTrial,
            Button close,
            Button difficultyToggle,
            Image trackMid,
            Image trackLeft,
            Image trackRight,
            Image knob,
            Text label,
            GameObject opponentRoot = null,
            Button oppPrev = null,
            Button oppNext = null,
            Image oppIcon = null,
            Text oppLabel = null,
            GameObject durationRoot = null,
            Button dur90 = null,
            Button dur120 = null,
            Button dur200 = null,
            Button tempo = null)
        {
            root = panelRoot;
            titleText = title;
            subtitleText = subtitle;
            classicButton = classic;
            blackoutButton = blackout;
            raceButton = race;
            combatantButton = combatant;
            timeTrialButton = timeTrial;
            tempoButton = tempo;
            closeButton = close;
            difficultySlide = difficultyToggle;
            slideTrack = trackMid;
            slideTrackLeft = trackLeft;
            slideTrackRight = trackRight;
            slideKnob = knob;
            slideLabel = label;
            slideKnobRect = knob != null ? knob.rectTransform : null;
            opponentRow = opponentRoot;
            opponentPrev = oppPrev;
            opponentNext = oppNext;
            opponentIcon = oppIcon;
            opponentLabel = oppLabel;
            durationRow = durationRoot;
            duration90Button = dur90;
            duration120Button = dur120;
            duration200Button = dur200;

            WireButtons();
            if (root != null)
                root.SetActive(false);
            open = false;
        }

        void WireButtons()
        {
            if (classicButton != null)
            {
                classicButton.onClick.RemoveAllListeners();
                classicButton.onClick.AddListener(() => SelectMode(GameMode.Classic));
            }

            if (blackoutButton != null)
            {
                blackoutButton.onClick.RemoveAllListeners();
                blackoutButton.onClick.AddListener(() => SelectMode(GameMode.Blackout));
            }

            if (raceButton != null)
            {
                raceButton.onClick.RemoveAllListeners();
                raceButton.onClick.AddListener(() => SelectMode(GameMode.Race));
            }

            if (combatantButton != null)
            {
                combatantButton.onClick.RemoveAllListeners();
                combatantButton.onClick.AddListener(() => SelectMode(GameMode.Combatant));
            }

            if (timeTrialButton != null)
            {
                timeTrialButton.onClick.RemoveAllListeners();
                timeTrialButton.onClick.AddListener(() => SelectMode(GameMode.TimeTrial));
            }

            if (tempoButton != null)
            {
                tempoButton.onClick.RemoveAllListeners();
                tempoButton.onClick.AddListener(() => SelectMode(GameMode.Tempo));
            }

            if (difficultySlide != null)
            {
                difficultySlide.onClick.RemoveAllListeners();
                difficultySlide.onClick.AddListener(ToggleDifficulty);
            }

            if (opponentPrev != null)
            {
                opponentPrev.onClick.RemoveAllListeners();
                opponentPrev.onClick.AddListener(() =>
                {
                    GameManager.Instance?.CycleRaceOpponent(-1);
                    RefreshOpponent();
                });
            }

            if (opponentNext != null)
            {
                opponentNext.onClick.RemoveAllListeners();
                opponentNext.onClick.AddListener(() =>
                {
                    GameManager.Instance?.CycleRaceOpponent(1);
                    RefreshOpponent();
                });
            }

            if (duration90Button != null)
            {
                duration90Button.onClick.RemoveAllListeners();
                duration90Button.onClick.AddListener(OnDuration90Clicked);
            }

            if (duration120Button != null)
            {
                duration120Button.onClick.RemoveAllListeners();
                duration120Button.onClick.AddListener(OnDuration120Clicked);
            }

            if (duration200Button != null)
            {
                duration200Button.onClick.RemoveAllListeners();
                duration200Button.onClick.AddListener(OnDuration200Clicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        public void Open()
        {
            if (root == null) return;
            open = true;
            root.SetActive(true);
            Refresh(instantSlide: true);
            FeedbackService.UiSelect();
        }

        public void Close()
        {
            if (!open)
            {
                if (root != null && root.activeSelf)
                    root.SetActive(false);
                return;
            }

            open = false;
            if (root != null)
                root.SetActive(false);
            FeedbackService.UiSelect();
            GameManager.Instance?.OnGameModeClosed();
        }

        void SelectMode(GameMode mode)
        {
            GameManager.Instance?.SetGameMode(mode);
            Refresh(instantSlide: true);
        }

        void OnDuration90Clicked() => SelectDuration(TimeTrialLength.Seconds90);
        void OnDuration120Clicked() => SelectDuration(TimeTrialLength.Seconds120);
        void OnDuration200Clicked() => SelectDuration(TimeTrialLength.Seconds200);

        void SelectDuration(TimeTrialLength length)
        {
            length = GameModeRules.ClampTimeTrialLength(GameModeRules.TimeTrialSeconds(length));
            // Always persist even if GameManager is missing mid-setup.
            SaveService.SelectedTimeTrialLength = length;
            if (GameManager.Instance != null)
                GameManager.Instance.SetTimeTrialLength(length);
            Refresh(instantSlide: true);
        }

        void ToggleDifficulty()
        {
            var next = hardSelected ? Difficulty.Easy : Difficulty.Hard;
            GameManager.Instance?.SetDifficulty(next);
            Refresh(instantSlide: false);
        }

        void Refresh(bool instantSlide)
        {
            var gm = GameManager.Instance;
            var mode = gm != null ? gm.SelectedMode : SaveService.SelectedGameMode;
            var difficulty = gm != null ? gm.SelectedDifficulty : SaveService.SelectedDifficulty;
            var length = gm != null ? gm.SelectedTimeTrialLength : SaveService.SelectedTimeTrialLength;
            hardSelected = difficulty == Difficulty.Hard;

            LayoutChrome();

            if (titleText != null)
                titleText.gameObject.SetActive(false);

            if (subtitleText != null)
            {
                if (GameModeRules.IsTimeTrial(mode))
                {
                    subtitleText.text =
                        $"{GameModeRules.Description(mode, difficulty)}\n" +
                        $"Clock: {GameModeRules.TimeTrialSecondsLabel(length)}";
                }
                else
                {
                    subtitleText.text = GameModeRules.Description(mode, difficulty);
                }

                var box = subtitleText.rectTransform.sizeDelta.x;
                if (box < 8f) box = 40 * RosterScreenLayout.Unit;
                RosterScreenLayout.ApplyWrapped(
                    subtitleText,
                    RosterScreenLayout.FitOneLine(box, "Classic climb. Solid pads and breakables mixed in.", SubtitleFontMin, SubtitleFontMax),
                    TextAnchor.MiddleCenter);
            }

            StyleModeButton(classicButton, "Classic", mode == GameMode.Classic);
            StyleModeButton(blackoutButton, "Blackout", mode == GameMode.Blackout);
            StyleModeButton(raceButton, "Race", mode == GameMode.Race);
            StyleModeButton(combatantButton, "Combatant", mode == GameMode.Combatant);
            StyleModeButton(timeTrialButton, "Time Trial", mode == GameMode.TimeTrial);
            StyleModeButton(tempoButton, "Tempo", mode == GameMode.Tempo);

            // Six modes — 2 columns × 3 rows (phone-friendly touch cells).
            LayoutModeGrid();

            ApplyToggleVisual(instantSlide);
            LayoutToggle();
            RefreshOpponent();
            RefreshDuration(length);

            if (opponentRow != null)
                opponentRow.SetActive(mode == GameMode.Race);

            if (durationRow != null)
                durationRow.SetActive(mode == GameMode.TimeTrial);

            LayoutBackButton();
        }

        // ── Mobile layout (20:9 / 1080×2400 design space, CanvasScaler) ───────
        // Stack: 2×3 modes → context (race/duration) → mode description
        // → back chevron (left) + Easy/Hard (right).

        const int SubtitleFontMin = 34;
        const int SubtitleFontMax = 42;
        const int ModeCellFont = 40;

        const float ModeCellW = 360f;
        const float ModeCellH = 120f;
        const float ModeColGap = 20f;
        const float ModeRowGap = 14f;
        const float ModeGridTopY = 120f;

        const float BottomPad = RosterScreenLayout.BackChevronBottom;
        const float SidePad = RosterScreenLayout.BackChevronPad;
        const float SliderHitH = TrackHeight + 28f;
        const float DescriptionH = 7f * RosterScreenLayout.Unit;
        static float BottomButtonH => Mathf.Max(RosterScreenLayout.BackChevronSize, SliderHitH);
        static float DescriptionY => BottomPad + BottomButtonH + RosterScreenLayout.Unit;
        static float ContextRowY => DescriptionY + DescriptionH + RosterScreenLayout.Unit * 2f;

        // Duration chips (90 / 120 / 200).
        const float DurationBtnWidth = 200f;
        const float DurationBtnHeight = 100f;
        const float DurationChipSpacing = 220f;

        void RefreshDuration(TimeTrialLength length)
        {
            LayoutDurationChips();

            StyleDurationButton(duration90Button, "90s", length == TimeTrialLength.Seconds90);
            StyleDurationButton(duration120Button, "120s", length == TimeTrialLength.Seconds120);
            StyleDurationButton(duration200Button, "200s", length == TimeTrialLength.Seconds200);
        }

        /// <summary>
        /// Places the three Time Trial duration squares (90 / 120 / 200) in a row.
        /// Reparents chips under durationRow so moving the row always moves the squares.
        /// </summary>
        void LayoutDurationChips()
        {
            if (durationRow == null) return;

            var row = durationRow.transform as RectTransform;
            if (row != null)
            {
                row.anchorMin = new Vector2(0.5f, 0f);
                row.anchorMax = new Vector2(0.5f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, ContextRowY);
                row.sizeDelta = new Vector2(720f, DurationBtnHeight + 20f);
                row.localScale = Vector3.one;
            }

            // Ensure chips are children of the row (old scenes sometimes left them elsewhere).
            EnsureChildOf(duration90Button, durationRow.transform);
            EnsureChildOf(duration120Button, durationRow.transform);
            EnsureChildOf(duration200Button, durationRow.transform);

            LayoutDurationButton(duration90Button, -DurationChipSpacing);
            LayoutDurationButton(duration120Button, 0f);
            LayoutDurationButton(duration200Button, DurationChipSpacing);
        }

        static void EnsureChildOf(Button button, Transform parent)
        {
            if (button == null || parent == null) return;
            if (button.transform.parent != parent)
                button.transform.SetParent(parent, false);
        }

        static void LayoutDurationButton(Button button, float x)
        {
            if (button == null) return;
            var rect = button.transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Local to DurationRow — row Y is what moves the whole set up/down.
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(DurationBtnWidth, DurationBtnHeight);
            rect.localScale = Vector3.one;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;
                lr.localScale = Vector3.one;
                label.raycastTarget = false;
            }
        }

        static void StyleDurationButton(Button button, string label, bool selected)
        {
            if (button == null) return;

            button.transition = Selectable.Transition.None;

            var img = button.targetGraphic as Image;
            if (img == null)
                img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = selected
                    ? new Color(0.15f, 0.35f, 0.65f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.12f);
                img.raycastTarget = true;
                button.targetGraphic = img;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.raycastTarget = false;
                UiTextStyle.ApplySized(text, UiTextStyle.CaptionFontSize, TextAnchor.MiddleCenter);
            }
        }

        // Race opponent: portrait + nickname between arrows (phone-scaled).
        const float OppIconSize = 180f;
        const float OppIconY = 70f;
        const float OppNameY = -70f;
        const float OppArrowX = 220f;

        void RefreshOpponent()
        {
            LayoutOpponentRow();

            var gm = GameManager.Instance;
            if (gm == null) return;

            var opp = gm.GetRaceOpponent();
            if (opponentLabel != null)
            {
                // Race nickname under the portrait, between the arrows.
                opponentLabel.text = opp != null ? RaceNicknames.ForCreature(opp) : "RIVAL";
                UiTextStyle.ApplySized(opponentLabel, UiTextStyle.CaptionFontSize, TextAnchor.MiddleCenter);
            }

            if (opponentIcon != null)
            {
                var sprite = opp != null ? opp.ResolveSprite() : null;
                opponentIcon.sprite = sprite;
                opponentIcon.color = opp != null ? opp.tint : Color.white;
                opponentIcon.enabled = sprite != null;
                opponentIcon.preserveAspect = true;
                opponentIcon.raycastTarget = false;
            }
        }

        /// <summary>
        /// Forces the race opponent block into: centered big icon, name below between &lt; &gt;.
        /// Safe to call every refresh so older scenes pick up the layout.
        /// </summary>
        void LayoutOpponentRow()
        {
            if (opponentRow == null) return;

            var row = opponentRow.transform as RectTransform;
            if (row != null)
            {
                row.anchorMin = new Vector2(0.5f, 0f);
                row.anchorMax = new Vector2(0.5f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                // Sits in the shared context band above the mode description.
                row.anchoredPosition = new Vector2(0f, ContextRowY - 40f);
                row.sizeDelta = new Vector2(720f, 280f);
            }

            if (opponentIcon != null)
            {
                var ir = opponentIcon.rectTransform;
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.pivot = new Vector2(0.5f, 0.5f);
                ir.anchoredPosition = new Vector2(0f, OppIconY);
                ir.sizeDelta = new Vector2(OppIconSize, OppIconSize);
                ir.localScale = Vector3.one;
                opponentIcon.preserveAspect = true;
            }

            if (opponentLabel != null)
            {
                var lr = opponentLabel.rectTransform;
                lr.anchorMin = new Vector2(0.5f, 0.5f);
                lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(0f, OppNameY);
                lr.sizeDelta = new Vector2(360f, 64f);
                lr.localScale = Vector3.one;
                opponentLabel.alignment = TextAnchor.MiddleCenter;
                opponentLabel.raycastTarget = false;
            }

            LayoutOpponentArrow(opponentPrev, -OppArrowX, OppNameY);
            LayoutOpponentArrow(opponentNext, OppArrowX, OppNameY);
        }

        static void LayoutOpponentArrow(Button button, float x, float y)
        {
            if (button == null) return;
            var rect = button.transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(80f, 80f);
            rect.localScale = Vector3.one;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var tr = label.rectTransform;
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = Vector2.zero;
                tr.offsetMax = Vector2.zero;
                tr.localScale = Vector3.one;
                label.raycastTarget = false;
            }
        }

        float KnobMinX => -(TrackWidth * 0.5f) + KnobSize * 0.5f + KnobPad;
        float KnobMaxX => (TrackWidth * 0.5f) - KnobSize * 0.5f - KnobPad;

        void ApplyToggleVisual(bool instant)
        {
            var targetT = hardSelected ? 1f : 0f;
            if (instant)
                knobT = targetT;

            SetTrackColor(Color.Lerp(EasyTrack, HardTrack, knobT));

            if (slideKnob != null)
                slideKnob.color = KnobFace;

            if (slideLabel != null)
            {
                // Text switches with state (Easy ↔ Hard), on the open side of the oval.
                slideLabel.text = hardSelected ? "Hard" : "Easy";
                slideLabel.color = LabelOn;
                slideLabel.fontStyle = FontStyle.Bold;
                slideLabel.alignment = TextAnchor.MiddleCenter;
                slideLabel.fontSize = LabelFont;
                UiTextStyle.Apply(slideLabel);

                var lr = slideLabel.rectTransform;
                lr.anchorMin = new Vector2(0.5f, 0.5f);
                lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(TrackWidth * 0.46f, TrackHeight);
                lr.anchoredPosition = new Vector2(hardSelected ? -TrackWidth * 0.17f : TrackWidth * 0.17f, 0f);
            }

            if (instant)
                ApplyKnobPosition(knobT);
        }

        void SetTrackColor(Color c)
        {
            if (slideTrack != null) slideTrack.color = c;
            if (slideTrackLeft != null) slideTrackLeft.color = c;
            if (slideTrackRight != null) slideTrackRight.color = c;
        }

        void ApplyKnobPosition(float t)
        {
            var x = Mathf.Lerp(KnobMinX, KnobMaxX, Mathf.Clamp01(t));
            if (slideKnobRect != null)
                slideKnobRect.anchoredPosition = new Vector2(x, 0f);
        }

        void LayoutToggle()
        {
            if (difficultySlide == null) return;

            var rect = difficultySlide.transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-SidePad, BottomPad);
            rect.sizeDelta = new Vector2(TrackWidth + 48f, SliderHitH);

            var track = difficultySlide.transform.Find("Track") as RectTransform;
            if (track != null)
            {
                track.sizeDelta = new Vector2(TrackWidth, TrackHeight);
                ResizeCap(track.Find("LeftCap") as RectTransform, true);
                ResizeCap(track.Find("RightCap") as RectTransform, false);
                var mid = track.Find("Mid") as RectTransform;
                if (mid != null)
                {
                    mid.offsetMin = new Vector2(TrackHeight * 0.5f - 1f, 0f);
                    mid.offsetMax = new Vector2(-(TrackHeight * 0.5f - 1f), 0f);
                }
            }

            if (slideKnobRect != null)
            {
                slideKnobRect.anchorMin = new Vector2(0.5f, 0.5f);
                slideKnobRect.anchorMax = new Vector2(0.5f, 0.5f);
                slideKnobRect.pivot = new Vector2(0.5f, 0.5f);
                slideKnobRect.sizeDelta = new Vector2(KnobSize, KnobSize);
            }

            ApplyKnobPosition(knobT);
        }

        static void ResizeCap(RectTransform cap, bool left)
        {
            if (cap == null) return;
            cap.anchorMin = new Vector2(left ? 0f : 1f, 0.5f);
            cap.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            cap.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            cap.sizeDelta = new Vector2(TrackHeight, TrackHeight);
            cap.anchoredPosition = Vector2.zero;
        }

        void LayoutChrome()
        {
            var contentW = RosterScreenLayout.ContentWidth(root != null ? root.transform as RectTransform : null);

            if (titleText != null)
                titleText.gameObject.SetActive(false);

            PlaceDescription(subtitleText != null ? subtitleText.rectTransform : null, contentW);
        }

        void LayoutBackButton()
        {
            if (closeButton == null) return;
            RosterScreenLayout.PlaceBackChevron(closeButton.transform as RectTransform);
            RosterScreenLayout.StyleBackChevron(closeButton);
        }

        static void PlaceDescription(RectTransform rect, float width)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, DescriptionY);
            rect.sizeDelta = new Vector2(width, DescriptionH);
        }

        void LayoutModeGrid()
        {
            //  [ Classic  ] [ Blackout  ]
            //  [ Race     ] [ Combatant ]
            //  [ Time Trial ] [ Tempo   ]
            var colX = (ModeCellW + ModeColGap) * 0.5f;
            var rowStep = ModeCellH + ModeRowGap;
            var topY = ModeGridTopY;

            LayoutModeButton(classicButton, -colX, topY);
            LayoutModeButton(blackoutButton, colX, topY);
            LayoutModeButton(raceButton, -colX, topY - rowStep);
            LayoutModeButton(combatantButton, colX, topY - rowStep);
            LayoutModeButton(timeTrialButton, -colX, topY - rowStep * 2f);
            LayoutModeButton(tempoButton, colX, topY - rowStep * 2f);
        }

        static void LayoutModeButton(Button button, float x, float y)
        {
            if (button == null) return;
            var rect = button.transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(ModeCellW, ModeCellH);
            rect.localScale = Vector3.one;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(8f, 4f);
                lr.offsetMax = new Vector2(-8f, -4f);
                lr.localScale = Vector3.one;
                label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.resizeTextForBestFit = false;
            }
        }

        static void StyleModeButton(Button button, string label, bool selected)
        {
            if (button == null) return;

            button.transition = Selectable.Transition.None;

            var img = button.targetGraphic as Image;
            if (img == null)
                img = button.GetComponent<Image>();
            if (img != null)
            {
                // Unselected still has a subtle plate so the hit target is visible on phone.
                img.color = selected
                    ? new Color(0.15f, 0.35f, 0.65f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.10f);
                img.raycastTarget = true;
                button.targetGraphic = img;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                // Slightly smaller than full menu size so "Time Trial" fits a grid cell.
                UiTextStyle.ApplySized(text, ModeCellFont, TextAnchor.MiddleCenter);
            }
        }

        void Update()
        {
            if (!open) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            var targetT = hardSelected ? 1f : 0f;
            if (!Mathf.Approximately(knobT, targetT))
            {
                knobT = Mathf.MoveTowards(knobT, targetT, 10f * Time.unscaledDeltaTime);
                SetTrackColor(Color.Lerp(EasyTrack, HardTrack, knobT));
                ApplyKnobPosition(knobT);
            }
        }

        /// <summary>
        /// True oval toggle: left circle + mid bar + right circle, white dot knob, Easy/Hard text.
        /// </summary>
        public static void BuildDifficultySlide(
            Transform parent,
            out Button slideButton,
            out Image trackMid,
            out Image trackLeft,
            out Image trackRight,
            out Image knob,
            out Text label)
        {
            EnsureSprites();

            // Transparent hit target (must not be a colored rectangle).
            var go = new GameObject("DifficultyToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-SidePad, BottomPad);
            rect.sizeDelta = new Vector2(TrackWidth + 48f, SliderHitH);

            var hit = go.GetComponent<Image>();
            hit.sprite = s_pixelSprite;
            hit.color = new Color(1f, 1f, 1f, 0.001f); // nearly invisible but raycastable
            hit.raycastTarget = true;

            slideButton = go.GetComponent<Button>();
            slideButton.transition = Selectable.Transition.None;
            slideButton.targetGraphic = hit;

            // --- Oval track: circle | bar | circle ---
            var trackRoot = new GameObject("Track", typeof(RectTransform));
            trackRoot.transform.SetParent(go.transform, false);
            var tr = trackRoot.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(TrackWidth, TrackHeight);
            tr.anchoredPosition = Vector2.zero;

            trackLeft = CreateFilledImage(trackRoot.transform, "LeftCap", s_circleSprite);
            var leftR = trackLeft.rectTransform;
            leftR.anchorMin = new Vector2(0f, 0.5f);
            leftR.anchorMax = new Vector2(0f, 0.5f);
            leftR.pivot = new Vector2(0f, 0.5f);
            leftR.sizeDelta = new Vector2(TrackHeight, TrackHeight);
            leftR.anchoredPosition = Vector2.zero;
            trackLeft.color = EasyTrack;

            trackRight = CreateFilledImage(trackRoot.transform, "RightCap", s_circleSprite);
            var rightR = trackRight.rectTransform;
            rightR.anchorMin = new Vector2(1f, 0.5f);
            rightR.anchorMax = new Vector2(1f, 0.5f);
            rightR.pivot = new Vector2(1f, 0.5f);
            rightR.sizeDelta = new Vector2(TrackHeight, TrackHeight);
            rightR.anchoredPosition = Vector2.zero;
            trackRight.color = EasyTrack;

            trackMid = CreateFilledImage(trackRoot.transform, "Mid", s_pixelSprite);
            var midR = trackMid.rectTransform;
            midR.anchorMin = new Vector2(0f, 0f);
            midR.anchorMax = new Vector2(1f, 1f);
            midR.pivot = new Vector2(0.5f, 0.5f);
            // Overlap into the caps so there's no gap; ends stay circular.
            midR.offsetMin = new Vector2(TrackHeight * 0.5f - 1f, 0f);
            midR.offsetMax = new Vector2(-(TrackHeight * 0.5f - 1f), 0f);
            trackMid.color = EasyTrack;

            // Label on open side of the oval.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(TrackWidth * 0.46f, TrackHeight);
            lr.anchoredPosition = new Vector2(TrackWidth * 0.17f, 0f);

            label = labelGo.GetComponent<Text>();
            label.text = "Easy";
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = LabelFont;
            label.color = LabelOn;
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiTextStyle.Apply(label);

            // White circular knob (the "dot").
            knob = CreateFilledImage(go.transform, "Knob", s_circleSprite);
            var kr = knob.rectTransform;
            kr.anchorMin = new Vector2(0.5f, 0.5f);
            kr.anchorMax = new Vector2(0.5f, 0.5f);
            kr.pivot = new Vector2(0.5f, 0.5f);
            kr.sizeDelta = new Vector2(KnobSize, KnobSize);
            kr.anchoredPosition = new Vector2(-(TrackWidth * 0.5f) + KnobSize * 0.5f + KnobPad, 0f);
            knob.color = KnobFace;
            knob.raycastTarget = false;

            // Draw order: track under label under knob.
            trackRoot.transform.SetSiblingIndex(0);
            labelGo.transform.SetSiblingIndex(1);
            knob.transform.SetAsLastSibling();
        }

        static Image CreateFilledImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            return img;
        }

        static void EnsureSprites()
        {
            if (s_circleSprite == null)
                s_circleSprite = CreateCircleSprite(128);
            if (s_pixelSprite == null)
                s_pixelSprite = CreatePixelSprite();
        }

        static Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        static Sprite CreateCircleSprite(int size)
        {
            size = Mathf.Max(32, size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var r = (size - 1) * 0.5f;
            const float aa = 1.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - r;
                    var dy = y - r;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = 1f - Mathf.Clamp01((dist - (r - aa)) / aa);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
