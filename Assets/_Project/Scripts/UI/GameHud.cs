using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntColony
{
    /// <summary>
    /// The on-screen interface: a pixel-art HUD drawn with Unity's immediate-mode GUI and <see cref="PixelFont"/>.
    /// It is built to be read at a glance, not all at once:
    /// - top left: resources with icons and bars; top middle: the next wave,
    /// - top right: the advisor's most important tip,
    /// - bottom: icon buttons with key, name and cost; hover one for what it does,
    /// - R opens the colony report (food flow, workers, brood, all tips); M the missions (jobs for workers and builders);
    ///   H the controls.
    /// Everything is laid out for a screen about 720 units tall. The scale is chosen so one frame pixel is a whole number
    /// of screen pixels, and panels, buttons, bars, icons and text are snapped to screen pixels, so the pixel art stays crisp.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        const float ReferenceHeight = 720f;
        const float Margin = 10f;
        const float RowHeight = 22f;
        const float SidePanelWidth = 300f;
        /// <summary>The bevelled frames are drawn on an 8x8 grid; the outer 3 cells are the edge that doesn't stretch.</summary>
        const int FrameCells = 8;
        const int FrameEdgeCells = 3;
        const int FramePixel = 3;

        static readonly Color Cream = AutumnPalette.Cream;
        static readonly Color Muted = new Color(0.78f, 0.7f, 0.58f);
        // Dark ink for text on the parchment card.
        static readonly Color Ink = AutumnPalette.Hex("3a2414");
        static readonly Color InkMuted = AutumnPalette.Hex("6e4b2c");
        static readonly Color InkDanger = AutumnPalette.Hex("9a2716");
        static readonly Color InkWarning = AutumnPalette.Hex("7a4a00");
        static readonly Color Gold = AutumnPalette.Gold;
        static readonly Color Good = new Color(0.6f, 0.95f, 0.45f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.35f);
        static readonly Color LeafOrange = AutumnPalette.Orange;
        static readonly Color SoldierTint = new Color(0.55f, 0.42f, 0.42f);
        static readonly Color BuilderTint = new Color(0.8f, 0.95f, 0.65f);
        static readonly Color SpitterTint = new Color(0.62f, 0.85f, 0.45f);

        /// <summary>One button of the action bar.</summary>
        struct ActionSpec
        {
            public string Key;
            public string Label;
            public Sprite Icon;
            public Color IconTint;
            public string Cost;
            public string TooltipTitle;
            public string Tooltip;
            public bool Available;
            public bool Active;
            public Action OnClick;
        }

        ColonyGame game;
        PixelFont font;
        readonly List<Texture2D> createdTextures = new List<Texture2D>();
        /// <summary>Screen areas covered by the HUD (in reference units), so clicks there don't reach the world.</summary>
        readonly List<Rect> uiRects = new List<Rect>();
        float guiScale = 1f;
        /// <summary>Height of the screen in GUI units (about <see cref="ReferenceHeight"/>, a bit more at some resolutions).</summary>
        float screenHeight = ReferenceHeight;
        bool showMissions;
        bool missionsPositionSet;
        Vector2 missionsPosition;
        Vector2 missionsDragOffset;
        int missionsDragControl;
        bool IsDraggingMissions => missionsDragControl != 0 && GUIUtility.hotControl == missionsDragControl;

        void StopMissionsDrag()
        {
            StopWindowDrag(ref missionsDragControl);
        }
        bool showReport;
        bool showWaveReport;
        bool waveReportPositionSet;
        Vector2 waveReportPosition;
        Vector2 waveReportDragOffset;
        int waveReportDragControl;
        int selectedReportWave;
        bool IsDraggingWaveReport => waveReportDragControl != 0 && GUIUtility.hotControl == waveReportDragControl;
        bool reportPositionSet;
        Vector2 reportPosition;
        Vector2 reportDragOffset;
        int reportDragControl;
        bool IsDraggingReport => reportDragControl != 0 && GUIUtility.hotControl == reportDragControl;
        bool showHelp;
        bool showSound;
        bool showMusic;
        int musicFirstTrack;
        AudioClip portraitTrack;
        int portraitSelection = -2;
        int portraitIndex = -1;
        int previousPortrait = -1;
        float portraitChangedAt;
        bool musicPositionSet;
        Vector2 musicPosition;
        Vector2 musicDragOffset;
        int musicDragControl;
        bool IsDraggingMusic => musicDragControl != 0 && GUIUtility.hotControl == musicDragControl;

        static void StopWindowDrag(ref int control)
        {
            if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            control = 0;
        }
        enum Confirmation { None, NewGame, Tutorial, MainMenu }
        Confirmation confirmation;
        string confirmationError = "";
        bool drawingConfirmation;
        public bool HasConfirmation => confirmation != Confirmation.None;

        public void CancelConfirmation()
        {
            confirmation = Confirmation.None;
            confirmationError = "";
            coverRects.Clear();
            nextCoverRects.Clear();
        }

        public void RequestNewGame(bool tutorial = false)
        {
            ClosePanels();
            if (game.HasSave)
                confirmation = tutorial ? Confirmation.Tutorial : Confirmation.NewGame;
            else game.StartNewGame(tutorial);
        }

        void RequestMainMenu()
        {
            ClosePanels();
            confirmation = Confirmation.MainMenu;
        }

        /// <summary>Where each action button was drawn this frame, by its key (for the tutorial's highlight).</summary>
        readonly Dictionary<string, Rect> actionRects = new Dictionary<string, Rect>();

        public bool IsMissionsOpen => showMissions;

        /// <summary>The nest room whose details card is open (clicked in the nest view), or null.</summary>
        Room selectedRoom;

        public void SelectRoom(Room room)
        {
            if (room != null && game.Audio != null) game.Audio.Play(Sound.UiClick);
            selectedRoom = room;
        }

        // Leaves (or another particle prefab) falling from a button's sides when the mouse moves onto it.
        UiParticles hoverParticles;
        /// <summary>The button under the mouse in the last repaint (zero size if none).</summary>
        Rect hoveredButton;
        bool anyButtonHovered;
        float hoverParticleCooldown;

        // Panels drawn over other buttons (help, report, missions). Buttons under them ignore the mouse.
        // The rects are collected while repainting and used from the next frame on, since those panels are drawn last.
        readonly List<Rect> coverRects = new List<Rect>();
        readonly List<Rect> nextCoverRects = new List<Rect>();
        bool drawingCover;

        // Font pixel sizes, snapped to whole screen pixels every frame.
        float small;
        float medium;
        float large;
        float huge;
        float iconPixel;

        GUIStyle panelStyle;
        GUIStyle cardStyle;
        GUIStyle buttonStyle;
        GUIStyle disabledButtonStyle;
        GUIStyle activeButtonStyle;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            hoverParticles ??= new UiParticles(transform);
            Health.Damaged -= ShowDamage;
            Health.Damaged += ShowDamage;
        }

        // ---------- Floating numbers ----------

        public static readonly Color FoodColor = AutumnPalette.Gold;
        public static readonly Color LeavesColor = AutumnPalette.Orange;
        public static readonly Color StoneColor = new Color(0.78f, 0.78f, 0.74f);
        static readonly Color EnemyDamageColor = new Color(1f, 0.95f, 0.85f);

        /// <summary>A number or word that rises from a spot in the world and fades.</summary>
        struct Floater
        {
            public Vector2 World;
            public string Text;
            public Color Color;
            public float Age;
            public bool OnSurface;
        }

        const float FloaterLife = 1.1f;
        const int MaxFloaters = 80;
        readonly List<Floater> floaters = new List<Floater>();

        /// <summary>Shows <paramref name="text"/> rising from <paramref name="world"/> (e.g. "+3" for stored food).</summary>
        public void FloatText(Vector2 world, string text, Color color)
        {
            if (floaters.Count >= MaxFloaters) floaters.RemoveAt(0);
            // A little sideways jitter, so numbers from the same spot don't sit on top of each other.
            world.x += UnityEngine.Random.Range(-0.15f, 0.15f);
            floaters.Add(new Floater { World = world, Text = text, Color = color, OnSurface = game.Surface.IsOnSurface(world) });
        }

        void ShowDamage(Health health, float amount)
        {
            if (health == null || game == null || game.Surface == null) return;
            bool enemy = health.GetComponent<Enemy>() != null;
            Vector2 at = (Vector2)health.transform.position + Vector2.up * 0.5f;
            FloatText(at, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), enemy ? EnemyDamageColor : Bad);
        }

        /// <summary>Rises in whole pixel steps (stepped, like the rest of the art) and fades out at the end.</summary>
        void DrawFloaters()
        {
            if (floaters.Count == 0) return;
            Camera view = game.CameraRig.Camera;
            bool surface = game.CameraRig.View == CameraView.Surface;
            foreach (Floater floater in floaters)
            {
                if (floater.OnSurface != surface) continue;
                Vector3 screen = view.WorldToScreenPoint(floater.World);
                if (screen.z < 0f) continue;
                float rise = Mathf.Floor(floater.Age * 10f) * 2f;
                var center = new Vector2(screen.x / guiScale, (Screen.height - screen.y) / guiScale - rise);
                Color color = floater.Color;
                color.a = Mathf.Clamp01((FloaterLife - floater.Age) / 0.35f);
                Text(new Rect(center.x - 60f, center.y - 8f, 120f, 16f), floater.Text, color, small, TextAnchor.MiddleCenter);
            }
        }

        void UpdateFloaters(float deltaTime)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                Floater floater = floaters[i];
                floater.Age += deltaTime;
                if (floater.Age >= FloaterLife) floaters.RemoveAt(i);
                else floaters[i] = floater;
            }
        }

        void Update()
        {
            hoverParticles?.Update(Time.unscaledDeltaTime);
            UpdateFloaters(Time.deltaTime); // pause with the game
            hoverParticleCooldown -= Time.unscaledDeltaTime;
        }

        public void ToggleReport()
        {
            bool open = !showReport;
            ClosePanels();
            showReport = open;
        }

        public void ToggleHelp()
        {
            bool open = !showHelp;
            ClosePanels();
            showHelp = open;
        }

        /// <summary>Closes the report, missions, help or sound panel. False if none was open.</summary>
        public bool ClosePanels()
        {
            bool anyOpen = showReport || showHelp || showMissions || showSound || showMusic || showWaveReport || selectedRoom != null;
            showReport = showHelp = showMissions = showSound = showMusic = showWaveReport = false;
            selectedRoom = null;
            StopMissionsDrag();
            StopWindowDrag(ref musicDragControl);
            StopWindowDrag(ref reportDragControl);
            StopWindowDrag(ref waveReportDragControl);
            // Closed overlays must not keep blocking hover/clicks until a later repaint.
            coverRects.Clear();
            nextCoverRects.Clear();
            return anyOpen;
        }

        public void ToggleMissions()
        {
            bool open = !showMissions;
            ClosePanels();
            showMissions = open;
        }

        public void ToggleSound()
        {
            bool open = !showSound;
            ClosePanels();
            showSound = open;
        }

        /// <summary>True if a screen position (pixels, y up, as the Input System gives it) is over a HUD panel or button.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (IsDraggingMissions || IsDraggingMusic || IsDraggingReport || IsDraggingWaveReport) return true;
            var guiPoint = new Vector2(screenPosition.x / guiScale, (Screen.height - screenPosition.y) / guiScale);
            foreach (Rect rect in uiRects)
            {
                if (rect.Contains(guiPoint)) return true;
            }
            return false;
        }

        void OnGUI()
        {
            if (game == null || game.Colony == null || game.Colony.Stats == null) return;
            CreateStylesOnce();

            // Whole screen pixels per frame pixel (rounded down, so the layout always fits).
            float scale = Mathf.Max(1f, Mathf.Floor(Screen.height / ReferenceHeight * FramePixel)) / FramePixel;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale;
            screenHeight = Screen.height / scale;
            guiScale = scale;
            font.GuiScale = scale;
            uiRects.Clear();
            small = PixelFont.Snap(1.5f, scale);
            medium = PixelFont.Snap(2f, scale);
            large = PixelFont.Snap(3f, scale);
            huge = PixelFont.Snap(5f, scale);
            iconPixel = PixelFont.Snap(1.5f, scale);

            bool repaint = Event.current.type == EventType.Repaint;
            if (game.Intro != null && game.Intro.IsPlaying)
            {
                // The intro shows no HUD: only letterbox bars, its captions and fades, and how to skip it.
                if (repaint) DrawIntro(screenWidth, game.Intro);
                GUI.matrix = previousMatrix;
                return;
            }
            if (repaint)
            {
                anyButtonHovered = false;
                nextCoverRects.Clear();
            }
            bool wasEnabled = GUI.enabled;
            if (HasConfirmation) GUI.enabled = false;
            DrawScreens(screenWidth, scale);
            GUI.enabled = wasEnabled;
            if (HasConfirmation)
            {
                drawingCover = drawingConfirmation = true;
                DrawConfirmation(screenWidth);
                drawingConfirmation = false;
            }
            drawingCover = false;
            if (repaint)
            {
                if (!anyButtonHovered) hoveredButton = Rect.zero;
                coverRects.Clear();
                coverRects.AddRange(nextCoverRects);
                // Over everything else, once all panels have said where they are.
                bool playing = !game.InMainMenu && !game.IsPaused && game.State == GameState.Playing
                               && (game.Tutorial == null || !game.Tutorial.ShowingPopup);
                if (playing) DrawAntCard();
                hoverParticles?.Draw();
            }
            GUI.matrix = previousMatrix;
        }

        /// <summary>The intro's frame: black bars top and bottom, the fade, a caption and a small "skip" hint.</summary>
        void DrawIntro(float screenWidth, IntroCutscene intro)
        {
            float bar = Mathf.Round(screenHeight * 0.1f);
            if (intro.Blackness > 0f)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, intro.Blackness);
                GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
                GUI.color = previous;
            }
            FillRect(new Rect(0f, 0f, screenWidth, bar), Color.black);
            FillRect(new Rect(0f, screenHeight - bar, screenWidth, bar), Color.black);

            if (!string.IsNullOrEmpty(intro.Caption) && intro.CaptionAlpha > 0f)
            {
                Color colour = Cream;
                colour.a = intro.CaptionAlpha;
                Text(new Rect(0f, screenHeight - bar, screenWidth, bar), intro.Caption, colour, medium, TextAnchor.MiddleCenter);
            }
            Color hint = Muted;
            hint.a = intro.CanSkip ? 0.8f : 0f;
            Text(new Rect(0f, bar * 0.5f - 7f, screenWidth - Margin * 2f, 14f), "Esc / Space / click: skip", hint, small, TextAnchor.MiddleRight);
        }

        void DrawScreens(float screenWidth, float scale)
        {
            if (game.InMainMenu)
            {
                DrawMainMenu(screenWidth);
                drawingCover = true;
                if (showHelp) DrawHelp(screenWidth);
                if (showSound) DrawSound(screenWidth);
                drawingCover = false;
                return;
            }

            DrawRoomLabels(scale);
            DrawRoomInfo(scale);
            if (Event.current.type == EventType.Repaint) DrawFloaters();
            DrawSelectionBox(scale);
            DrawSceneFade(screenWidth);

            DrawResourcePanel();
            DrawWaveBanner(screenWidth);
            DrawBossBar(screenWidth);
            float advisorBottom = DrawAdvisorCard(screenWidth, Margin);
            DrawMusicControls(screenWidth, advisorBottom > Margin ? advisorBottom + 8f : Margin);
            DrawEnemyWarnings(screenWidth);
            DrawMessage(screenWidth);

            if (game.State != GameState.Playing)
            {
                DrawEndScreen(screenWidth);
                drawingCover = true;
                if (showWaveReport) DrawWaveReport(screenWidth);
                drawingCover = false;
                return;
            }
            if (game.IsPaused)
            {
                DrawPauseScreen(screenWidth);
                drawingCover = true;
                if (showSound) DrawSound(screenWidth);
                if (showMusic) DrawMusicWindow(screenWidth);
                if (showWaveReport) DrawWaveReport(screenWidth);
                drawingCover = false;
                return;
            }

            showSound = false; // the sound panel lives in the menus
            actionRects.Clear();
            DrawActionBar(screenWidth);

            Tutorial tutorial = game.Tutorial;
            if (tutorial != null && tutorial.IsActive && !tutorial.ShowingPopup) DrawTutorialGoal(screenWidth, tutorial);
            if (tutorial != null && tutorial.ShowingPopup)
            {
                drawingCover = true;
                DrawTutorialPopup(screenWidth, tutorial);
                drawingCover = false;
                return;
            }

            drawingCover = true;
            if (showReport) DrawReport(screenWidth);
            if (showHelp) DrawHelp(screenWidth);
            if (showMissions) DrawMissions(screenWidth);
            if (showMusic) DrawMusicWindow(screenWidth);
            if (showWaveReport) DrawWaveReport(screenWidth);
            drawingCover = false;
        }

        /// <summary>True if the mouse is over <paramref name="rect"/> and not over a panel drawn on top of it.</summary>
        bool MouseOver(Rect rect)
        {
            Vector2 mouse = PointerPosition();
            return GUI.enabled && rect.Contains(mouse) && !IsCovered(mouse);
        }

        Vector2 PointerPosition()
        {
            // IMGUI repaint events can retain an old pointer after focusing/resizing the Game view.
            // Use the same current Input System position as world input, converted to this GUI's scale.
            if (Mouse.current == null) return Event.current.mousePosition;
            Vector2 position = Mouse.current.position.ReadValue();
            return new Vector2(position.x / guiScale, (Screen.height - position.y) / guiScale);
        }

        bool IsCovered(Vector2 point)
        {
            if (HasConfirmation && !drawingConfirmation) return true;
            if (drawingCover) return false;
            if (game.Tutorial != null && game.Tutorial.ShowingPopup) return true;
            if (!showReport && !showHelp && !showMissions && !showSound && !showMusic && !showWaveReport
                && (game.Tutorial == null || !game.Tutorial.ShowingPopup)) return false;
            foreach (Rect cover in coverRects)
            {
                if (cover.Contains(point)) return true;
            }
            return false;
        }

        /// <summary>An invisible-or-styled button that ignores clicks landing on a panel drawn over it.</summary>
        bool HudButton(Rect rect, GUIStyle style)
        {
            bool covered = IsCovered(PointerPosition());
            bool wasEnabled = GUI.enabled;
            if (covered) GUI.enabled = false;
            bool clicked = GUI.Button(rect, GUIContent.none, style);
            GUI.enabled = wasEnabled;
            // Little square buttons (+, -, X, the music arrows) click silently; they're pressed often.
            bool quiet = rect.width <= SilentButtonSize;
            if (clicked && !covered && !quiet && game.Audio != null) game.Audio.Play(Sound.UiClick);
            return clicked && !covered;
        }

        void OnDestroy()
        {
            Health.Damaged -= ShowDamage;
            hoverParticles?.Clear();
            foreach (Texture2D texture in createdTextures) Destroy(texture);
            font?.Destroy();
        }

        // ---------- Top left: resources ----------

        // Colours of the resource panel (a flat wood box with a light rim and inset dividers).
        static readonly Color WoodFill = new Color32(78, 51, 32, 255);
        static readonly Color WoodOutline = new Color32(38, 20, 9, 255);
        static readonly Color WoodRimLight = new Color32(150, 117, 90, 255);
        static readonly Color WoodRimDark = new Color32(105, 71, 43, 255);
        static readonly Color WoodShadow = new Color32(48, 24, 12, 255);
        static readonly Color GrooveDark = new Color32(61, 34, 15, 255);
        static readonly Color GrooveLight = new Color32(111, 84, 66, 255);
        static readonly Color BarTrack = new Color32(40, 26, 18, 255);
        static readonly Color BarGold = new Color32(240, 196, 84, 255);
        static readonly Color BarGoldDark = new Color32(217, 148, 40, 255);
        static readonly Color BarMissing = new Color32(196, 52, 40, 255);
        static readonly Color QueenLabel = new Color32(236, 184, 80, 255);

        /// <summary>
        /// Top left: food (and its trend), leaves and stone; then storage, the ants by caste and the brood; then the
        /// queen's health. Icons come from the UI Skin (Resource panel icons) and fall back to the game's sprites.
        /// </summary>
        /// <summary>The panel image's scale on the resource panel (smaller than on big panels, so the frame is thin).</summary>
        const float ResourceFrameScale = 0.15f;
        /// <summary>How far the frame reaches past the resource panel's content.</summary>
        const float ResourceFrameBleed = 6f;

        void DrawResourcePanel()
        {
            Colony colony = game.Colony;
            GameArt art = game.Art;
            UiSkin skin = game.UiSkin;
            const float row = 24f;
            const float divider = 14f;
            const float width = 280f;
            float height = 10f + row * 3f + divider + 20f + 18f + row * 2f + divider + row + 8f;

            // The skin's panel image (Top_Left_HUD) frames it, drawn small so its rim and moss stay thin; else a flat wood box.
            var panel = Snap(new Rect(Margin, Margin, width, height));
            if (skin != null && skin.panel != null)
            {
                Rect frame = Snap(Inset(panel, -ResourceFrameBleed));
                NineSlice(frame, skin.panel, skin.panelBorder, ResourceFrameScale);
                uiRects.Add(frame);
            }
            else
            {
                WoodBox(panel);
            }
            uiRects.Add(panel);
            Rect content = panel;

            float x = content.x + 12f;
            float right = content.xMax - 12f;
            float textX = x + 26f;
            float y = content.y + 10f;

            // Food, and whether it is going up or down.
            Icon(new Vector2(x + 9f, y + row * 0.5f), SkinIcon(skin?.iconFood, art.Berry), maxSize: 20f);
            Text(new Rect(textX, y, 160f, row), $"Food {colony.Food}", Cream, medium, TextAnchor.MiddleLeft);
            float net = colony.Stats.NetPerMinute;
            string rate = $"{net:+0;-0;0}/min";
            Text(new Rect(x, y, right - x, row), rate, net >= 0f ? Good : Bad, small, TextAnchor.MiddleRight);
            if (skin != null && skin.iconRate != null)
                Icon(new Vector2(right - font.Measure(rate, small) - 11f, y + row * 0.5f), skin.iconRate, maxSize: 14f);
            y += row;

            Icon(new Vector2(x + 9f, y + row * 0.5f), SkinIcon(skin?.iconLeaves, art.Leaf), maxSize: 20f);
            Text(new Rect(textX, y, 120f, row), "Leaves", Cream, medium, TextAnchor.MiddleLeft);
            Text(new Rect(textX + font.Measure("Leaves ", medium), y, 60f, row), colony.Leaves.ToString(), LeafOrange, medium, TextAnchor.MiddleLeft);
            Text(new Rect(x, y, right - x, row), "-> garden", Muted, small, TextAnchor.MiddleRight);
            y += row;

            Icon(new Vector2(x + 9f, y + row * 0.5f), SkinIcon(skin?.iconStone, art.Pebble), maxSize: 20f);
            Text(new Rect(textX, y, 160f, row), $"Stone {colony.Stone}", Cream, medium, TextAnchor.MiddleLeft);
            Text(new Rect(x, y, right - x, row), "dig for more", Muted, small, TextAnchor.MiddleRight);
            y += row;

            y = Divider(content, y + 4f) + 4f;

            // Storage: food and leaves share it.
            int capacity = Mathf.Max(1, colony.StorageCapacity);
            int stored = colony.Food + colony.Leaves;
            float labelX = x;
            if (skin != null && skin.iconStorage != null)
            {
                Icon(new Vector2(x + 8f, y + 9f), skin.iconStorage, maxSize: 18f);
                labelX = x + 22f;
            }
            Color storageColor = colony.FreeSpace < capacity * 0.1f ? Bad : Cream;
            Text(new Rect(labelX, y, right - labelX, 18f), $"Capacity: {stored}/{capacity}", storageColor, medium, TextAnchor.MiddleLeft);
            y += 20f;
            GoldBar(new Rect(x, y, right - x, 10f), stored / (float)capacity, missingColor: null);
            y += 18f;

            // Ants by caste.
            float column = (right - x) / 4f;
            CasteIconCount(new Vector2(x, y), skin?.iconWorker, AntRole.Worker, "W");
            CasteIconCount(new Vector2(x + column, y), skin?.iconSoldier, AntRole.Soldier, "S");
            CasteIconCount(new Vector2(x + column * 2f, y), skin?.iconBuilder, AntRole.Builder, "B");
            CasteIconCount(new Vector2(x + column * 3f, y), skin?.iconSpitter, AntRole.Spitter, "A");
            y += row;

            // Brood.
            int brood = 0;
            int hungry = 0;
            bool anyLarva = false;
            foreach (Brood b in colony.Brood)
            {
                brood++;
                if (b.IsWaitingForFood) hungry++;
                if (b.Stage == BroodStage.Larva) anyLarva = true;
            }
            Sprite eggIcon = anyLarva && skin != null && skin.iconEggHatching != null ? skin.iconEggHatching : SkinIcon(skin?.iconEgg, art.Egg);
            Icon(new Vector2(x + 9f, y + row * 0.5f), eggIcon, maxSize: 20f);
            string broodText = colony.RaiseQueue.Count > 0 ? $"{brood} growing  +{colony.RaiseQueue.Count} queued" : $"{brood} growing";
            Text(new Rect(textX, y, right - textX, row), broodText, brood > 0 ? Good : Muted, medium, TextAnchor.MiddleLeft);
            if (hungry > 0) Text(new Rect(x, y, right - x, row), "hungry!", Bad, small, TextAnchor.MiddleRight);
            y += row;

            y = Divider(content, y + 4f) + 4f;

            // Queen: gold for her health, red for what she has lost.
            Text(new Rect(x, y, 80f, row), "Queen", QueenLabel, medium, TextAnchor.MiddleLeft);
            float barX = x + font.Measure("Queen", medium) + 14f;
            GoldBar(new Rect(barX, y + 7f, right - barX, 10f), colony.QueenHealth / colony.QueenMaxHealth, BarMissing);
        }

        static Sprite SkinIcon(Sprite custom, Sprite fallback) => custom != null ? custom : fallback;

        /// <summary>
        /// The icon for an ant caste: its own art from its prefab (side view, or top view when asked), drawn as it is;
        /// without one, the game's generic ant tinted in the caste's colour.
        /// </summary>
        (Sprite sprite, Color tint) CasteArt(AntRole role, bool topDown = false)
        {
            Ant prefab = game.Prefabs.AntFor(role);
            Sprite own = prefab != null ? (topDown ? prefab.TopIcon : prefab.SideIcon) : null;
            if (own != null) return (own, Color.white);

            Sprite generic = topDown ? game.Art.AntTop.Walk[0] : game.Art.AntSide.Walk[0];
            switch (role)
            {
                case AntRole.Soldier: return (generic, SoldierTint);
                case AntRole.Builder: return (generic, BuilderTint);
                case AntRole.Spitter: return (generic, SpitterTint);
                default: return (generic, new Color(0.85f, 0.3f, 0.2f));
            }
        }

        /// <summary>A caste's count with its icon: the UI skin's icon if one is set, otherwise the caste's own art.</summary>
        void CasteIconCount(Vector2 position, Sprite skinIcon, AntRole role, string letter)
        {
            (Sprite sprite, Color tint) = skinIcon != null ? (skinIcon, Color.white) : CasteArt(role);
            CasteCount(position, sprite, tint, game.Colony.CountAnts(role), letter);
        }

        void CasteCount(Vector2 position, Sprite icon, Color tint, int count, string letter)
        {
            Icon(new Vector2(position.x + 9f, position.y + 12f), icon, tint, maxSize: 18f);
            Text(new Rect(position.x + 22f, position.y, 44f, 24f), $"{count} {letter}", Cream, medium, TextAnchor.MiddleLeft);
        }

        /// <summary>The flat wood box: dark outline, a light rim (lighter on top), a dark inner line, and cut corners.</summary>
        void WoodBox(Rect rect)
        {
            const float p = 2f; // one "pixel" of the frame, in UI units
            DropShadow(rect);
            FillCut(rect, WoodOutline, p);
            Rect rim = Inset(rect, p);
            FillCut(rim, WoodRimDark, p);
            FillRect(new Rect(rim.x + p, rim.y, rim.width - p * 2f, p), WoodRimLight); // top highlight
            Rect shadow = Inset(rim, p);
            FillRect(shadow, WoodShadow);
            FillRect(Inset(shadow, p), WoodFill);
        }

        /// <summary>An inset groove across the panel (a dark line over a light one). Returns the y below it.</summary>
        float Divider(Rect panel, float y)
        {
            const float p = 2f;
            float left = panel.x + 6f;
            float width = panel.width - 12f;
            FillRect(new Rect(left, y, width, p), GrooveDark);
            FillRect(new Rect(left, y + p, width, p), GrooveLight);
            return y + p * 2f;
        }

        /// <summary>A bar with a dark track and a gold fill (lighter on top); <paramref name="missingColor"/> paints the empty part.</summary>
        void GoldBar(Rect rect, float fraction, Color? missingColor)
        {
            rect = Snap(rect);
            const float p = 2f;
            FillRect(rect, WoodOutline);
            Rect inner = Inset(rect, p * 0.5f);
            FillRect(inner, missingColor ?? BarTrack);
            float filled = Mathf.Round(inner.width * Mathf.Clamp01(fraction) * guiScale) / guiScale;
            if (filled <= 0f) return;
            FillRect(new Rect(inner.x, inner.y, filled, inner.height), BarGoldDark);
            FillRect(new Rect(inner.x, inner.y, filled, Mathf.Ceil(inner.height * 0.5f)), BarGold);
        }

        /// <summary>A filled rect with its four corner pixels left out (a rounded pixel corner).</summary>
        void FillCut(Rect rect, Color color, float corner)
        {
            FillRect(new Rect(rect.x + corner, rect.y, rect.width - corner * 2f, rect.height), color);
            FillRect(new Rect(rect.x, rect.y + corner, rect.width, rect.height - corner * 2f), color);
        }

        void FillRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(Snap(rect), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static Rect Inset(Rect rect, float by) => new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);

        // ---------- Top middle: waves ----------

        void DrawWaveBanner(float screenWidth)
        {
            WaveSpawner waves = game.Waves;
            bool underAttack = waves.IsNestUnderAttack;
            bool tutorialWait = game.Tutorial != null && game.Tutorial.HoldsWaves && !underAttack && waves.HasMoreWaves;
            string text = underAttack ? "Ant hill under attack!"
                : tutorialWait ? $"Wave {waves.CurrentWave + 1}: after the tutorial"
                : waves.HasMoreWaves ? $"Wave {waves.CurrentWave + 1}/{waves.TotalWaves} in {Mathf.CeilToInt(waves.TimeUntilNextWave)}s"
                : $"Final wave! {waves.Enemies.Count} left";

            float width = 230f;
            var panel = new Rect((screenWidth - width) * 0.5f, Margin, width, 34f);
            Panel(panel, card: true);
            Icon(new Vector2(panel.x + 18f, panel.center.y), game.Art.Beetle.Walk[0]);

            bool blink = underAttack && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f;
            Text(new Rect(panel.x + 32f, panel.y, panel.width - 40f, panel.height), text, blink ? Cream : underAttack ? Bad : Cream, small, TextAnchor.MiddleCenter);
            if (MouseOver(panel)) Outline(panel, Gold, 1f);
            if (HudButton(panel, GUIStyle.none))
            {
                bool open = !showWaveReport;
                ClosePanels();
                showWaveReport = open;
                if (open) selectedReportWave = waves.CurrentWave;
            }
        }

        /// <summary>Illustrated battle cards. Only waves that have already started can be browsed.</summary>
        void DrawWaveReport(float screenWidth)
        {
            WaveSpawner waves = game.Waves;
            float width = Mathf.Min(680f, screenWidth - Margin * 2f);
            float height = Mathf.Min(500f, screenHeight - Margin * 2f);
            if (!waveReportPositionSet)
            {
                waveReportPosition = new Vector2((screenWidth - width) * 0.5f, (screenHeight - height) * 0.5f);
                waveReportPositionSet = true;
            }
            var panel = new Rect(waveReportPosition.x, waveReportPosition.y, width, height);
            DragWindow(ref panel, screenWidth, "WaveReportDrag", ref waveReportDragControl, ref waveReportDragOffset);
            waveReportPosition = panel.position;
            Panel(panel);
            Text(new Rect(panel.x + 18f, panel.y + 14f, width - 240f, 28f), "Wave report", Gold, large);
            Text(new Rect(panel.xMax - 212f, panel.y + 20f, 160f, 16f), "Drag title to move", Muted, small, TextAnchor.MiddleRight);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 12f, 28f, 28f))) { ClosePanels(); return; }

            int lastStarted = waves.CurrentWave;
            if (lastStarted <= 0)
            {
                Icon(new Vector2(panel.center.x, panel.y + 164f), game.Art.AntHill, null, 100f);
                Text(new Rect(panel.x + 24f, panel.y + 240f, width - 48f, 30f), "No battles yet", Gold, large, TextAnchor.MiddleCenter);
                Text(new Rect(panel.x + 24f, panel.y + 286f, width - 48f, 56f),
                    "Battle reports appear here when an attack starts.", Cream, medium, TextAnchor.UpperCenter);
                return;
            }
            selectedReportWave = Mathf.Clamp(selectedReportWave, 1, lastStarted);
            float navY = panel.y + 58f;
            if (SmallButton(new Rect(panel.x + 18f, navY, 42f, 38f), "<", selectedReportWave > 1)) selectedReportWave--;
            if (SmallButton(new Rect(panel.xMax - 60f, navY, 42f, 38f), ">", selectedReportWave < lastStarted)) selectedReportWave++;
            WaveReportRecord record = waves.FindReport(selectedReportWave);
            bool unknown = record == null;
            bool partial = record != null && record.partial;
            string status = unknown ? "History unavailable" : record.completed ? "Cleared" : "In progress";
            Text(new Rect(panel.x + 68f, navY, width - 136f, 30f), $"Wave {selectedReportWave}", Gold, large, TextAnchor.MiddleCenter);
            Text(new Rect(panel.x + 68f, navY + 32f, width - 136f, 18f), status,
                record != null && record.completed ? Good : Muted, small, TextAnchor.MiddleCenter);

            var battle = new Rect(panel.x + 18f, panel.y + 122f, width - 36f, 80f);
            WoodBox(battle);
            Icon(new Vector2(battle.x + 42f, battle.center.y), game.Art.Beetle.Walk[0], null, 48f);
            float contentX = battle.x + 82f;
            float contentWidth = battle.width - 98f;
            float columnWidth = contentWidth * 0.5f;
            Text(new Rect(contentX, battle.y + 10f, columnWidth, 18f), "Enemies defeated", Cream, small);
            Text(new Rect(contentX, battle.y + 34f, columnWidth, 30f),
                unknown ? "--" : record.kills.ToString() + (partial ? "+" : ""), Gold, large);
            Text(new Rect(contentX + columnWidth, battle.y + 10f, columnWidth, 18f), "Enemies remaining", Cream, small);
            Text(new Rect(contentX + columnWidth, battle.y + 34f, columnWidth, 30f),
                waves.RemainingInWave(selectedReportWave).ToString(), Cream, large);

            float gap = 12f;
            float cardWidth = (width - 36f - gap) * 0.5f;
            float cardHeight = Mathf.Max(70f, (height - 292f) * 0.5f);
            float y = panel.y + 218f;
            DrawWaveLossCard(new Rect(panel.x + 18f, y, cardWidth, cardHeight), "Soldier", record?.soldiersLost ?? 0, unknown, partial, SoldierTint);
            DrawWaveLossCard(new Rect(panel.x + 18f + cardWidth + gap, y, cardWidth, cardHeight), "Spitter", record?.spittersLost ?? 0, unknown, partial, SpitterTint);
            y += cardHeight + gap;
            DrawWaveLossCard(new Rect(panel.x + 18f, y, cardWidth, cardHeight), "Worker", record?.workersLost ?? 0, unknown, partial, Color.white);
            DrawWaveLossCard(new Rect(panel.x + 18f + cardWidth + gap, y, cardWidth, cardHeight), "Builder", record?.buildersLost ?? 0, unknown, partial, BuilderTint);

            WaveReportRecord upkeep = waves.FindReport(0);
            string note = unknown ? "This save does not contain the results of this battle."
                : partial ? "'+' means counted since loading; earlier results are unavailable."
                : "Combat losses belong to the attacking enemy's wave. Remaining includes arrivals.";
            Text(new Rect(panel.x + 18f, panel.yMax - 48f, width - 36f, 20f), note, Muted, small);
            Text(new Rect(panel.x + 18f, panel.yMax - 26f, width - 36f, 18f),
                $"Non-combat losses this game: {upkeep?.TotalLosses ?? 0}   (starvation: {upkeep?.starved ?? 0})", Muted, small);
        }

        void DrawWaveLossCard(Rect card, string role, int losses, bool unknown, bool partial, Color tint)
        {
            WoodBox(card);
            Texture2D portrait = null;
            UiSkin skin = game.UiSkin;
            if (skin != null && skin.musicPortraits != null)
                foreach (UiSkin.MusicPortrait entry in skin.musicPortraits)
                    if (entry != null && string.Equals(entry.caption, role, StringComparison.OrdinalIgnoreCase))
                    { portrait = entry.image; break; }
            var image = new Rect(card.x + 8f, card.y + 8f, 68f, card.height - 16f);
            if (portrait != null) GUI.DrawTexture(image, portrait, ScaleMode.ScaleToFit);
            else Icon(image.center, game.Art.AntTop.Walk[0], tint, Mathf.Min(image.width, image.height));
            float left = card.x + 88f;
            float textWidth = card.width - 100f;
            Text(new Rect(left, card.y + 12f, textWidth, 20f), role + " losses", Cream, small);
            string value = unknown ? "--" : losses.ToString() + (partial ? "+" : "");
            Text(new Rect(left, card.y + 42f, textWidth, 30f), value, losses > 0 ? Bad : Good, large);
        }

        /// <summary>While the final boss lives: its name and a big health bar under the wave banner.</summary>
        void DrawBossBar(float screenWidth)
        {
            Enemy boss = game.Waves.Boss;
            if (boss == null || boss.Health == null) return;

            const float width = 380f;
            var panel = Snap(new Rect((screenWidth - width) * 0.5f, Margin + 34f + 46f, width, 44f));
            WoodBox(panel);
            uiRects.Add(panel);
            Text(new Rect(panel.x, panel.y + 6f, panel.width, 14f), boss.Stats.name, Bad, small, TextAnchor.UpperCenter);
            GoldBar(new Rect(panel.x + 12f, panel.y + 24f, panel.width - 24f, 12f), boss.Health.Current / Mathf.Max(1f, boss.Health.Max), BarMissing);
        }

        // ---------- Top right: advice ----------

        /// <summary>Only the advisor's most important tip, with a hint that the report has the rest.</summary>
        float DrawAdvisorCard(float screenWidth, float top)
        {
            // Only real problems get a card; calm advice waits in the report.
            IReadOnlyList<Advisor.Tip> tips = game.Colony.Advisor.Tips;
            if (tips.Count == 0 || tips[0].Urgency == Advisor.Urgency.Info) return top;

            Advisor.Tip tip = tips[0];
            string footer = tips.Count > 1 ? $"R: report  (+{tips.Count - 1} more tips)" : "R: colony report";
            UiSkin skin = game.UiSkin;
            if (skin != null && skin.card != null)
            {
                return DrawParchmentAdvice(screenWidth, top, tip, footer, skin);
            }

            float innerWidth = SidePanelWidth - 44f;
            float textHeight = font.MeasureHeight(tip.Text, small, innerWidth);
            var panel = new Rect(screenWidth - Margin - SidePanelWidth, top, SidePanelWidth, 12f + textHeight + 18f + 6f);
            Panel(panel, card: true);

            Color color = TipColor(tip.Urgency);
            string mark = tip.Urgency == Advisor.Urgency.Info ? "i" : "!";
            Text(new Rect(panel.x + 10f, panel.y + 8f, 20f, 20f), mark, color, large, TextAnchor.UpperCenter);
            Text(new Rect(panel.x + 34f, panel.y + 9f, innerWidth, textHeight), tip.Text, color, small);
            Text(new Rect(panel.x + 34f, panel.y + 12f + textHeight, innerWidth, 14f), footer, Muted, small);
            return panel.yMax;
        }

        /// <summary>The advisor's tip on the parchment card, in dark ink, with the warning sign beside it.</summary>
        float DrawParchmentAdvice(float screenWidth, float top, Advisor.Tip tip, string footer, UiSkin skin)
        {
            const float padding = 16f;
            const float iconColumn = 26f;
            float innerWidth = SidePanelWidth - padding * 2f - iconColumn;
            float textHeight = font.MeasureHeight(tip.Text, small, innerWidth);
            float height = padding + textHeight + 8f + 14f + padding;
            var panel = Snap(new Rect(screenWidth - Margin - SidePanelWidth, top, SidePanelWidth, Mathf.Max(height, 64f)));
            NineSlice(panel, skin.card, skin.cardBorder, skin.frameScale);
            uiRects.Add(panel);

            Color color = tip.Urgency == Advisor.Urgency.Danger ? InkDanger : tip.Urgency == Advisor.Urgency.Warning ? InkWarning : Ink;
            float textX = panel.x + padding + iconColumn;
            if (skin.warningIcon != null && tip.Urgency != Advisor.Urgency.Info)
            {
                // The sign keeps its shape: as tall as the tip's first lines, at most 30 units.
                float iconHeight = Mathf.Min(30f, Mathf.Max(20f, textHeight + 4f));
                float iconWidth = iconHeight * skin.warningIcon.width / skin.warningIcon.height;
                var icon = Snap(new Rect(panel.x + padding + (iconColumn - iconWidth) * 0.5f - 4f, panel.y + padding - 2f, iconWidth, iconHeight));
                GUI.DrawTexture(icon, skin.warningIcon, ScaleMode.ScaleToFit);
            }
            else
            {
                InkText(new Rect(panel.x + padding - 4f, panel.y + padding - 2f, iconColumn, 20f), "i", color, large, TextAnchor.UpperCenter);
            }

            InkText(new Rect(textX, panel.y + padding, innerWidth, textHeight), tip.Text, color, small);
            InkText(new Rect(textX, panel.y + padding + textHeight + 8f, innerWidth, 14f), footer, InkMuted, small);
            return panel.yMax;
        }

        static Color TipColor(Advisor.Urgency urgency)
        {
            switch (urgency)
            {
                case Advisor.Urgency.Danger: return Bad;
                case Advisor.Urgency.Warning: return Gold;
                default: return Cream;
            }
        }

        // ---------- Bottom: action bar ----------

        void DrawActionBar(float screenWidth)
        {
            List<ActionSpec> actions = AllActions();

            bool slots = game.UiSkin != null && game.UiSkin.HasSlots;
            const float gap = 6f;
            // The slot images are nearly square, so their buttons are a little taller.
            float buttonHeight = slots ? 76f : 62f;
            float buttonWidth = Mathf.Min(slots ? 92f : 96f, (screenWidth - Margin * 2f - gap * (actions.Count - 1)) / actions.Count);
            float totalWidth = buttonWidth * actions.Count + gap * (actions.Count - 1);
            float x = (screenWidth - totalWidth) * 0.5f;
            float y = screenHeight - Margin - buttonHeight;

            UiSkin skin = game.UiSkin;
            if (skin != null && skin.actionBar != null)
            {
                // Size the bar so the buttons sit in its groove, with the bar's bottom at the screen's edge.
                Vector4 groove = skin.actionBarGroove;
                float grooveHeight = Mathf.Max(1f, skin.actionBar.height - groove.z - groove.w);
                float barScale = (buttonHeight + 14f) / grooveHeight;
                float barHeight = skin.actionBar.height * barScale;
                float barTop = screenHeight - barHeight + 6f;
                y = barTop + groove.z * barScale + 7f;

                float padLeft = groove.x * barScale + 10f;
                float padRight = groove.y * barScale + 10f;
                var bar = new Rect(x - padLeft, barTop, totalWidth + padLeft + padRight, barHeight);
                NineSlice(bar, skin.actionBar, skin.actionBarBorder, barScale);
                uiRects.Add(bar);
            }
            uiRects.Add(new Rect(x, y, totalWidth, buttonHeight));

            DrawToolHint(screenWidth, y - 26f);
            const string corner = "H: help   R: report   M: missions";
            Text(new Rect(Margin, screenHeight - Margin - 14f, 360f, 14f), corner, Muted, small);

            ActionSpec? hovered = null;
            Rect hoveredRect = default;
            foreach (ActionSpec action in actions)
            {
                var rect = new Rect(x, y, buttonWidth, buttonHeight);
                actionRects[action.Key] = rect;
                if (ActionButton(rect, action)) action.OnClick();
                if (MouseOver(rect))
                {
                    hovered = action;
                    hoveredRect = rect;
                }
                x += buttonWidth + gap;
            }

            if (hovered.HasValue) DrawTooltip(hovered.Value, hoveredRect, screenWidth);
        }

        List<ActionSpec> AllActions()
        {
            GameSettings settings = game.Settings;
            GameArt art = game.Art;
            int food = game.Colony.Food;
            bool onSurface = game.CameraRig.View == CameraView.Surface;
            var actions = new List<ActionSpec>();
            var worker = CasteArt(AntRole.Worker);
            var soldier = CasteArt(AntRole.Soldier);
            var builder = CasteArt(AntRole.Builder);
            var spitter = CasteArt(AntRole.Spitter);

            actions.Add(RaiseAction("1", "Worker", AntRole.Worker, worker.sprite, worker.tint,
                "Gathers food and leaves on the surface and feeds larvae. Your economy."));
            actions.Add(RaiseAction("2", "Soldier", AntRole.Soldier, soldier.sprite, soldier.tint,
                "Guards the ant hill and fights. Command soldiers on the surface. Eats food but earns none."));
            actions.Add(RaiseAction("3", "Builder", AntRole.Builder, builder.sprite, builder.tint,
                "Digs tunnels, builds rooms, walls and towers, and repairs damage."));
            actions.Add(RaiseAction("Z", "Spitter", AntRole.Spitter, spitter.sprite, spitter.tint,
                "A soldier that spits acid from a distance. Frail: keep it behind biters and walls. Selected and ordered like soldiers."));

            if (onSurface)
            {
                BuildMode buildMode = game.BuildMode;
                actions.Add(new ActionSpec
                {
                    Key = "4", Label = "Wall", Icon = art.Wall, IconTint = Color.white, Cost = $"{settings.wallCost}S",
                    TooltipTitle = "Wall", Tooltip = $"Drag a line on the surface. {settings.wallCost} stone per block (dig for stone). " +
                                                     "Enemies must chew through walls, and your ants walk around them: leave a gap so they can get out!",
                    Available = game.Colony.Stone >= settings.wallCost, Active = buildMode.IsActive && buildMode.Kind == StructureKind.Wall,
                    OnClick = () => buildMode.Toggle(StructureKind.Wall),
                });
                actions.Add(new ActionSpec
                {
                    Key = "5", Label = "Tower", Icon = art.Tower, IconTint = Color.white, Cost = $"{settings.towerCost}+{settings.towerStoneCost}S",
                    TooltipTitle = "Acid tower", Tooltip = $"Click to place. {settings.towerCost} food and {settings.towerStoneCost} stone. " +
                                                           "Spits acid at enemies in range (hover a tower to see its range). Ants walk around towers.",
                    Available = food >= settings.towerCost && game.Colony.Stone >= settings.towerStoneCost, Active = buildMode.IsActive && buildMode.Kind == StructureKind.Tower,
                    OnClick = () => buildMode.Toggle(StructureKind.Tower),
                });
                int marked = game.Gather.MarkedCount;
                actions.Add(new ActionSpec
                {
                    Key = "6", Label = "Gather", Icon = art.Berry, IconTint = Color.white, Cost = marked > 0 ? $"{marked}" : "",
                    TooltipTitle = "Gather food",
                    Tooltip = "Select food piles like soldiers: click one or drag a box (Shift adds). Food gatherers and leaf cutters (M) " +
                              "only go to marked piles; with none marked they stay in the nest.",
                    Available = true, Active = game.Gather.IsActive, OnClick = game.Gather.Toggle,
                });
            }
            else
            {
                NestTools tools = game.NestTools;
                actions.Add(new ActionSpec
                {
                    Key = "4", Label = "Dig", Icon = art.DirtTile, IconTint = Color.white, Cost = "",
                    TooltipTitle = "Dig", Tooltip = "Drag a box over soil. Builders dig tiles that touch a tunnel. Digging can give stone (pebbly veins give the most) " +
                              "and uncover seed caches and grubs (food), more of them deeper down. Solid grey rock can't be dug.",
                    Available = true, Active = tools.Tool == NestTool.Dig, OnClick = () => tools.Toggle(NestTool.Dig),
                });
                actions.Add(RoomAction("5", NestTool.Storage, art.StoragePile,
                    $"Holds {settings.storageRoomCapacity} more food and leaves. Workers drop loads at the nearest one, so build near the entrance."));
                actions.Add(RoomAction("6", NestTool.Cradle, art.Cradle,
                    "Holds one egg or larva. More cradles = more ants growing at once."));
                actions.Add(RoomAction("7", NestTool.Garden, art.FungusGarden,
                    $"Turns 1 leaf into {settings.gardenFoodPerLeaf} food every {settings.gardenCycleTime:0}s. Most food outside is leaves!"));
                actions.Add(new ActionSpec
                {
                    Key = "X", Label = "Remove", Icon = null, Cost = "",
                    TooltipTitle = "Remove", Tooltip = "Drag a box: removes rooms (built ones give half their cost back), fills empty tunnels back in and cancels dig marks. Never cuts a room off from the entrance.",
                    Available = true, Active = tools.Tool == NestTool.Remove, OnClick = () => tools.Toggle(NestTool.Remove),
                });
            }

            actions.Add(new ActionSpec
            {
                Key = "Tab", Label = onSurface ? "Nest" : "Surface", Icon = onSurface ? art.AntHill : art.Trees[0], IconTint = Color.white, Cost = "",
                TooltipTitle = onSurface ? "Go to the nest" : "Go to the surface",
                Tooltip = onSurface ? "Underground: dig, build rooms, raise larvae, read the colony report."
                                    : "Outside: food, enemies, soldiers and defenses.",
                Available = true, OnClick = () => game.CameraRig.ToggleView(),
            });

            actions.Add(new ActionSpec
            {
                Key = "M", Label = "Missions", Icon = worker.sprite, IconTint = worker.tint, Cost = "",
                TooltipTitle = "Missions", Tooltip = "Give workers and builders a single job: larva feeders, food gatherers, leaf cutters, diggers, wall builders.",
                Available = true, Active = showMissions, OnClick = ToggleMissions,
            });

            if (game.Waves.HasMoreWaves)
            {
                actions.Add(new ActionSpec
                {
                    Key = "N", Label = "Wave", Icon = art.Beetle.Walk[0], IconTint = Color.white, Cost = "",
                    TooltipTitle = "Call the next wave", Tooltip = "Start the next wave now instead of waiting.",
                    Available = true, OnClick = () => game.Waves.StartNextWave(),
                });
            }
            return actions;
        }

        ActionSpec RaiseAction(string key, string label, AntRole role, Sprite icon, Color tint, string description)
        {
            GameSettings settings = game.Settings;
            int queued = game.Colony.CountQueued(role);
            CasteCosts costs = settings.CostsFor(role);
            return new ActionSpec
            {
                Key = key, Label = queued > 0 ? $"{label} +{queued}" : label, Icon = icon, IconTint = tint,
                Cost = $"{settings.RaiseCost(role)}",
                TooltipTitle = $"Raise a {label.ToLower()}",
                Tooltip = $"{description}\nEgg {settings.eggCost} food, then workers feed the larva {costs.larvaFood} food. Needs a free cradle.",
                Available = game.Colony.Food >= settings.eggCost,
                OnClick = () => game.Colony.QueueRaise(role),
            };
        }

        ActionSpec RoomAction(string key, NestTool tool, Sprite icon, string description)
        {
            RoomType type = NestTools.RoomFor(tool);
            RoomInfo info = RoomInfo.Of(type);
            int cost = RoomInfo.CostOf(type, game.Settings);
            return new ActionSpec
            {
                Key = key, Label = tool == NestTool.Garden ? "Garden" : info.Name, Icon = icon, IconTint = Color.white, Cost = $"{cost}",
                TooltipTitle = $"{info.Name}  ({info.Size.x}x{info.Size.y} tiles)", Tooltip = $"{description}\nPlace it on dug-out tunnel.",
                Available = game.Colony.Food >= cost, Active = game.NestTools.Tool == tool,
                OnClick = () => game.NestTools.Toggle(tool),
            };
        }

        /// <summary>A chunky pixel button: key badge, icon, name and cost. Sinks while held down.</summary>
        bool ActionButton(Rect rect, ActionSpec action)
        {
            rect = Snap(rect);
            if (game.UiSkin != null && game.UiSkin.HasSlots) return SlotActionButton(rect, action);

            GUIStyle style = action.Active ? activeButtonStyle : action.Available ? buttonStyle : disabledButtonStyle;
            bool clicked = HudButton(rect, style);

            bool hovered = MouseOver(rect);
            if (hovered) NoteHover(rect);
            bool pressed = hovered && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;
            float sink = pressed ? FramePixel : -FramePixel; // the raised frame has a thick bottom shadow
            float alpha = action.Available || action.Active ? 1f : 0.55f;

            Color textColor = hovered ? Color.white : Cream;
            textColor.a = alpha;

            if (action.Icon != null)
            {
                Color tint = action.IconTint;
                tint.a = alpha;
                Icon(new Vector2(rect.center.x, rect.y + 22f + sink), action.Icon, tint, maxSize: 26f);
            }
            else
            {
                Text(new Rect(rect.x, rect.y + 10f + sink, rect.width, 24f), "X", new Color(Bad.r, Bad.g, Bad.b, alpha), large, TextAnchor.UpperCenter);
            }

            Text(new Rect(rect.x + 7f, rect.y + 7f + sink, 30f, 12f), action.Key, new Color(Gold.r, Gold.g, Gold.b, alpha), small);
            Text(new Rect(rect.x, rect.y + 38f + sink, rect.width, 12f), action.Label, textColor, small, TextAnchor.UpperCenter);
            if (!string.IsNullOrEmpty(action.Cost))
                Text(new Rect(rect.x, rect.y + 7f + sink, rect.width - 8f, 12f), action.Cost, new Color(Gold.r, Gold.g, Gold.b, alpha), small, TextAnchor.UpperRight);
            return clicked;
        }

        /// <summary>
        /// An action button on the slot images: key top-left, cost top-right, icon in the middle, name at the bottom.
        /// A button that can't be used shows the locked slot with its icon faintly on top; clicking it still explains why.
        /// </summary>
        bool SlotActionButton(Rect rect, ActionSpec action)
        {
            bool usable = action.Available || action.Active;
            bool clicked = SlotButton(rect, usable, action.Active, lockedLook: true, out bool hovered, out bool pressed);
            if (MouseOver(rect)) NoteHover(rect);
            float sink = pressed ? 2f : 0f;
            float alpha = usable ? 1f : 0.6f;
            const float inset = 11f;

            Color textColor = hovered ? Color.white : Cream;
            textColor.a = alpha;
            Color gold = new Color(Gold.r, Gold.g, Gold.b, alpha);

            if (action.Icon != null)
            {
                Color tint = action.IconTint;
                tint.a = usable ? 1f : 0.45f;
                Icon(new Vector2(rect.center.x, rect.y + rect.height * 0.46f + sink), action.Icon, tint, maxSize: 28f);
            }
            else
            {
                Text(new Rect(rect.x, rect.y + rect.height * 0.3f + sink, rect.width, 24f), "X", new Color(Bad.r, Bad.g, Bad.b, alpha), large, TextAnchor.UpperCenter);
            }

            Text(new Rect(rect.x + inset, rect.y + inset - 1f + sink, 30f, 12f), action.Key, gold, small);
            if (!string.IsNullOrEmpty(action.Cost))
                Text(new Rect(rect.x, rect.y + inset - 1f + sink, rect.width - inset, 12f), action.Cost, usable ? gold : new Color(Bad.r, Bad.g, Bad.b, 0.9f), small, TextAnchor.UpperRight);
            Text(new Rect(rect.x, rect.yMax - inset - 12f + sink, rect.width, 12f), action.Label, textColor, small, TextAnchor.UpperCenter);
            return clicked;
        }

        /// <summary>
        /// Draws a button from the slot images (normal, hover or held, pressed, locked) and returns whether it was clicked.
        /// Wide buttons are 9-sliced; a disabled wide button is a dimmed normal slot, since the locked picture can't stretch.
        /// With <paramref name="lockedLook"/>, a disabled button still reports clicks (so it can say why it is locked).
        /// </summary>
        bool SlotButton(Rect rect, bool enabled, bool active, bool lockedLook, out bool hovered, out bool pressed)
        {
            UiSkin skin = game.UiSkin;
            bool mouseOver = MouseOver(rect);
            hovered = enabled && mouseOver;
            pressed = hovered && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;

            bool wide = rect.width > rect.height * 1.4f;
            Texture2D texture = skin.slotNormal;
            if (!enabled && lockedLook && !wide && skin.slotLocked != null) texture = skin.slotLocked;
            else if (pressed && skin.slotPressed != null) texture = skin.slotPressed;
            else if ((hovered || active) && skin.slotHover != null) texture = skin.slotHover;

            Color previous = GUI.color;
            if (!enabled && texture == skin.slotNormal) GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            if (texture == skin.slotLocked) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            else NineSlice(rect, texture, skin.slotBorder, Mathf.Min(rect.width, rect.height) / texture.height);
            GUI.color = previous;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && (enabled || lockedLook);
            bool clicked = HudButton(rect, GUIStyle.none);
            GUI.enabled = previousEnabled;
            return clicked && (enabled || lockedLook);
        }

        /// <summary>
        /// Draws a wide button from the menu button images: the twiggy ends keep their shape and only the plank
        /// between them stretches. A disabled one is a dimmed normal button.
        /// </summary>
        bool PlankButton(Rect rect, bool enabled, out bool hovered, out bool pressed)
        {
            UiSkin skin = game.UiSkin;
            bool mouseOver = MouseOver(rect);
            hovered = enabled && mouseOver;
            pressed = hovered && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;

            Texture2D texture = skin.menuButton;
            if (pressed && skin.menuButtonPressed != null) texture = skin.menuButtonPressed;
            else if (hovered && skin.menuButtonHover != null) texture = skin.menuButtonHover;

            // The border is measured on the normal image; the hover and pressed images are the same picture at another size.
            float ratio = texture.width / (float)skin.menuButton.width;
            var border = new Vector4(skin.menuButtonBorder.x * ratio, skin.menuButtonBorder.y * ratio, 0f, 0f);

            Color previous = GUI.color;
            if (!enabled) GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            NineSlice(rect, texture, border, rect.height / texture.height);
            GUI.color = previous;

            if (hovered) NoteHover(rect);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enabled;
            bool clicked = HudButton(rect, GUIStyle.none);
            GUI.enabled = previousEnabled;
            return clicked && enabled;
        }

        /// <summary>
        /// Called for the button under the mouse while repainting. When the mouse has just moved onto it, the hover
        /// particles start at both of its sides, the right one mirrored.
        /// </summary>
        void NoteHover(Rect rect, bool particles = true)
        {
            if (Event.current.type != EventType.Repaint) return;
            anyButtonHovered = true;
            if (rect == hoveredButton) return;
            hoveredButton = rect;
            if (game.Audio != null) game.Audio.Play(Sound.UiHover);

            UiSkin skin = game.UiSkin;
            if (!particles || skin == null || skin.buttonHoverParticles == null || hoverParticleCooldown > 0f || hoverParticles == null) return;
            hoverParticleCooldown = skin.buttonHoverParticleCooldown;
            float y = rect.y + rect.height * 0.3f;
            hoverParticles.Play(skin.buttonHoverParticles, new Vector2(rect.x + 4f, y), skin.buttonHoverParticleScale);
            hoverParticles.Play(skin.buttonHoverParticles, new Vector2(rect.xMax - 4f, y), skin.buttonHoverParticleScale, mirrored: true);
        }

        void DrawTooltip(ActionSpec action, Rect button, float screenWidth)
        {
            const float width = 260f;
            float innerWidth = width - 24f;
            string body = string.IsNullOrEmpty(action.Cost) ? action.Tooltip : $"{action.Tooltip}\nCost: {action.Cost} food";
            float titleHeight = font.MeasureHeight(action.TooltipTitle, medium, innerWidth);
            float bodyHeight = font.MeasureHeight(body, small, innerWidth);
            float height = 18f + titleHeight + 4f + bodyHeight;

            float x = Mathf.Clamp(button.center.x - width * 0.5f, Margin, screenWidth - Margin - width);
            var panel = new Rect(x, button.y - height - 34f, width, height);
            Panel(panel);
            float y = panel.y + 9f;
            y += Text(new Rect(panel.x + 12f, y, innerWidth, titleHeight), action.TooltipTitle, Gold, medium) + 4f;
            Text(new Rect(panel.x + 12f, y, innerWidth, bodyHeight), body, Cream, small);
        }

        /// <summary>One line above the buttons explaining the tool in hand, or the soldier selection.</summary>
        void DrawToolHint(float screenWidth, float y)
        {
            string hint = null;
            bool onSurface = game.CameraRig.View == CameraView.Surface;
            BuildMode buildMode = game.BuildMode;
            NestTools tools = game.NestTools;
            int selectedCount = game.Selection.Selected.Count;

            if (onSurface && game.Gather.IsActive)
                hint = "Click or drag over food piles   -   Shift: add   -   right-click: stop";
            else if (onSurface && buildMode.IsActive)
                hint = buildMode.Kind == StructureKind.Wall ? "Drag to lay walls   -   right-click: stop" : "Click to place a tower   -   right-click: stop";
            else if (onSurface && selectedCount > 0)
                hint = $"{selectedCount} selected   -   right-click enemy: attack   -   right-click ground: move   -   Q: halve";
            else if (!onSurface && tools.IsActive && tools.PreviewSummary.Length > 0)
                hint = $"{tools.PreviewSummary}   -   release to apply";
            else if (!onSurface && tools.IsActive)
                hint = tools.Tool == NestTool.Dig ? "Drag a box over soil   -   right-click: stop"
                     : tools.Tool == NestTool.Remove ? "Drag a box: remove rooms, fill tunnels   -   right-click: stop"
                     : "Click on dug-out tunnel to place   -   right-click: stop";

            if (hint == null) return;
            float width = font.Measure(hint, medium) + 30f;
            var panel = new Rect((screenWidth - width) * 0.5f, y - 4f, width, 26f);
            Panel(panel, card: true);
            Text(panel, hint, AutumnPalette.Selection, medium, TextAnchor.MiddleCenter);
        }

        // ---------- Colony report (R) ----------

        void DrawReport(float screenWidth)
        {
            Colony colony = game.Colony;
            ColonyStats stats = colony.Stats;
            Nest nest = game.Nest;

            const float width = 600f;
            const float height = 470f;
            if (!reportPositionSet)
            {
                reportPosition = new Vector2((screenWidth - width) * 0.5f,
                    game.Tutorial != null && game.Tutorial.IsActive ? 228f : 92f);
                reportPositionSet = true;
            }
            var panel = new Rect(reportPosition.x, reportPosition.y, width, height);
            DragWindow(ref panel, screenWidth, "ReportDrag", ref reportDragControl, ref reportDragOffset);
            reportPosition = panel.position;
            Panel(panel);

            Text(new Rect(panel.x + 16f, panel.y + 12f, width - 240f, 24f), "Colony report", Gold, large);
            Text(new Rect(panel.xMax - 212f, panel.y + 18f, 160f, 16f), "Drag title to move", Muted, small, TextAnchor.MiddleRight);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 10f, 28f, 28f)))
            {
                showReport = false;
                StopWindowDrag(ref reportDragControl);
            }


            float columnWidth = (width - 48f) * 0.5f;
            float left = panel.x + 16f;
            float right = left + columnWidth + 16f;
            float top = panel.y + 50f;

            // Left column: where food comes from and goes.
            float y = top;
            Text(new Rect(left, y, columnWidth, 14f), "Food per minute", Muted, small);
            y += 18f;
            float gathered = stats.ForagedFood.PerMinute;
            float gardens = stats.GardenFood.PerMinute;
            float eaten = stats.Upkeep.PerMinute;
            float biggest = Mathf.Max(1f, gathered, gardens, eaten);
            y = FlowRow(left, y, columnWidth, "Gathered", gathered, biggest, Good);
            y = FlowRow(left, y, columnWidth, "Gardens", gardens, biggest, Good);
            y = FlowRow(left, y, columnWidth, "Eaten", -eaten, biggest, Bad);
            float net = stats.NetPerMinute;
            Text(new Rect(left, y + 4f, columnWidth, 24f), "Net", Cream, medium);
            Text(new Rect(left, y + 4f, columnWidth, 24f), $"{net:+0;-0;0}/min", net >= 0f ? Good : Bad, large, TextAnchor.UpperRight);
            y += 34f;
            if (stats.Wasted.PerMinute >= 1f)
            {
                Text(new Rect(left, y, columnWidth, 14f), $"Wasted {stats.Wasted.PerMinute:0}/min: storage full!", Bad, small);
                y += 18f;
            }

            y += 8f;
            Text(new Rect(left, y, columnWidth, 14f), "Storage", Muted, small);
            y += 18f;
            int capacity = Mathf.Max(1, colony.StorageCapacity);
            StackedBar(new Rect(left, y, columnWidth, 12f), new[] { (colony.Food / (float)capacity, Gold), (colony.Leaves / (float)capacity, LeafOrange) });
            y += 20f;
            Legend(left, y, Gold, $"Food {colony.Food}");
            Legend(left + 92f, y, LeafOrange, $"Leaves {colony.Leaves}");
            Text(new Rect(left, y, columnWidth, 14f), $"Free {colony.FreeSpace}", Muted, small, TextAnchor.UpperRight);
            y += 26f;

            Text(new Rect(left, y, columnWidth, 14f), "Fungus gardens", Muted, small);
            y += 18f;
            int built = nest.CountBuilt(RoomType.Garden);
            int working = 0;
            foreach (Room room in nest.Rooms)
            {
                if (room.Type == RoomType.Garden && room.IsBuilt && room.IsWorking) working++;
            }
            Text(new Rect(left, y, columnWidth, 14f), $"{built} built, {working} working   leaves in +{stats.ForagedLeaves.PerMinute:0}/min", Cream, small);

            // Right column: workers and brood.
            y = top;
            Text(new Rect(right, y, columnWidth, 14f), "Workers", Muted, small);
            y += 18f;
            int foraging = colony.CountWorkers(WorkerJob.Foraging);
            int nursing = colony.CountWorkers(WorkerJob.Nursing);
            int idle = colony.CountWorkers(WorkerJob.Idle);
            float total = Mathf.Max(1, foraging + nursing + idle);
            Color nurseColor = new Color(0.95f, 0.6f, 0.75f);
            StackedBar(new Rect(right, y, columnWidth, 12f), new[] { (foraging / total, Gold), (nursing / total, nurseColor), (idle / total, Muted) });
            y += 20f;
            Legend(right, y, Gold, $"Foraging {foraging}");
            Legend(right + 100f, y, nurseColor, $"Nursing {nursing}");
            Legend(right + 194f, y, Muted, $"Idle {idle}");
            y += 24f;

            float trip = stats.AverageTripTime;
            string rating = trip <= 0f ? "-" : trip < 20f ? "fast" : trip < 30f ? "ok" : "slow";
            Color ratingColor = trip <= 0f ? Muted : trip < 20f ? Good : trip < 30f ? Gold : Bad;
            Text(new Rect(right, y, columnWidth, 14f), trip > 0f ? $"Average trip {trip:0}s" : "Average trip -", Cream, small);
            Text(new Rect(right, y, columnWidth, 14f), rating, ratingColor, small, TextAnchor.UpperRight);
            y += 26f;

            Text(new Rect(right, y, columnWidth, 14f), "Brood in cradles", Muted, small);
            y += 18f;
            int shown = 0;
            foreach (Brood brood in colony.Brood)
            {
                if (shown++ >= 6) break;
                Sprite icon = brood.Stage == BroodStage.Egg ? game.Art.Egg : game.Art.Larva[0];
                Icon(new Vector2(right + 9f, y + 8f), icon);
                Text(new Rect(right + 24f, y + 2f, 90f, 14f), brood.Caste.ToString(), Cream, small);
                Bar(new Rect(right + 104f, y + 4f, columnWidth - 180f, 8f), brood.Progress, Brood.CasteColor(brood.Caste));
                if (brood.IsWaitingForFood) Text(new Rect(right, y + 2f, columnWidth, 14f), "hungry", Bad, small, TextAnchor.UpperRight);
                else if (brood.HasNurse) Text(new Rect(right, y + 2f, columnWidth, 14f), "feeding", Good, small, TextAnchor.UpperRight);
                y += 20f;
            }
            if (shown == 0)
            {
                Text(new Rect(right, y, columnWidth, 14f), "None. Raise ants with 1, 2, 3.", Muted, small);
                y += 18f;
            }
            if (colony.RaiseQueue.Count > 0)
                Text(new Rect(right, y + 2f, columnWidth, 14f), $"Queued: {colony.RaiseQueue.Count}", Muted, small);

            // Bottom: every tip.
            float tipsTop = panel.y + 330f;
            Text(new Rect(left, tipsTop, width, 14f), "Advisor", Muted, small);
            y = tipsTop + 18f;
            IReadOnlyList<Advisor.Tip> tips = colony.Advisor.Tips;
            for (int i = 0; i < tips.Count && y < panel.yMax - 20f; i++)
            {
                y += Text(new Rect(left, y, width - 32f, 40f), "- " + tips[i].Text, TipColor(tips[i].Urgency), small) + 4f;
            }
        }

        float FlowRow(float x, float y, float width, string label, float value, float biggest, Color color)
        {
            Text(new Rect(x, y, 90f, 14f), label, Cream, small);
            Bar(new Rect(x + 86f, y + 3f, width - 150f, 8f), Mathf.Abs(value) / biggest, color);
            Text(new Rect(x, y, width, 14f), $"{value:+0;-0;0}", color, small, TextAnchor.UpperRight);
            return y + 20f;
        }

        void Legend(float x, float y, Color color, string text)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y + 2f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = previous;
            Text(new Rect(x + 12f, y, 120f, 14f), text, Cream, small);
        }

        bool CloseButton(Rect rect)
        {
            rect = Snap(rect);
            if (game.UiSkin != null && game.UiSkin.HasSlots)
            {
                bool closed = SlotButton(rect, true, false, lockedLook: false, out _, out bool held);
                Text(new Rect(rect.x, rect.y + (held ? 1f : -1f), rect.width, rect.height), "X", Cream, medium, TextAnchor.MiddleCenter);
                return closed;
            }
            bool clicked = HudButton(rect, buttonStyle);
            Text(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), "X", Cream, medium, TextAnchor.MiddleCenter);
            return clicked;
        }

        // ---------- Missions (M) ----------

        /// <summary>
        /// Jobs for workers and builders: each row shows how many ants are on that mission, with - and + to move one
        /// ant between it and the free ants (who choose their own work).
        /// </summary>
        void DrawMissions(float screenWidth)
        {
            const float width = 580f;
            // Measure first (rows grow with their descriptions), then draw.
            if (!missionsPositionSet)
            {
                // The guide occupies y=96..212; open below it during a tutorial.
                float top = game.Tutorial != null && game.Tutorial.IsActive ? 228f : 92f;
                missionsPosition = new Vector2((screenWidth - width) * 0.5f, top);
                missionsPositionSet = true;
            }
            var panel = new Rect(missionsPosition.x, missionsPosition.y, width, 0f);
            float contentBottom = MissionSection(panel, panel.y + 52f, "Workers", AntRole.Worker, Missions.WorkerMissions, Color.white, draw: false);
            contentBottom = MissionSection(panel, contentBottom + 6f, "Builders", AntRole.Builder, Missions.BuilderMissions, BuilderTint, draw: false);
            panel.height = contentBottom - panel.y + 10f;
            DragWindow(ref panel, screenWidth, "MissionsDrag", ref missionsDragControl, ref missionsDragOffset);
            missionsPosition = panel.position;
            Panel(panel);

            Text(new Rect(panel.x + 16f, panel.y + 12f, width - 240f, 24f), "Missions", Gold, large);
            Text(new Rect(panel.xMax - 212f, panel.y + 18f, 160f, 16f), "Drag title to move", Muted, small, TextAnchor.MiddleRight);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 10f, 28f, 28f)))
            {
                showMissions = false;
                StopMissionsDrag();
            }

            float y = panel.y + 52f;
            y = MissionSection(panel, y, "Workers", AntRole.Worker, Missions.WorkerMissions, Color.white, draw: true);
            MissionSection(panel, y + 6f, "Builders", AntRole.Builder, Missions.BuilderMissions, BuilderTint, draw: true);
        }

        void DragWindow(ref Rect panel, float screenWidth, string controlName, ref int dragControl, ref Vector2 dragOffset)
        {
            int control = GUIUtility.GetControlID(controlName.GetHashCode(), FocusType.Passive);
            Event input = Event.current;
            Vector2 mouse = PointerPosition();
            var title = new Rect(panel.x, panel.y, panel.width - 48f, 44f);
            if (GUI.enabled && !HasConfirmation && input.type == EventType.MouseDown && input.button == 0 && title.Contains(mouse))
            {
                GUIUtility.hotControl = dragControl = control;
                dragOffset = mouse - panel.position;
                input.Use();
            }
            if (dragControl != 0 && GUIUtility.hotControl == dragControl)
            {
                if (input.type == EventType.MouseDrag)
                {
                    panel.position = mouse - dragOffset;
                    input.Use();
                }
                else if (input.type == EventType.MouseUp && input.button == 0)
                {
                    StopWindowDrag(ref dragControl);
                    input.Use();
                }
                // While captured, releasing outside the window must not command units.
                uiRects.Add(new Rect(0f, 0f, screenWidth, screenHeight));
            }
            else dragControl = 0;
            panel.x = Mathf.Clamp(panel.x, Margin, Mathf.Max(Margin, screenWidth - panel.width - Margin));
            panel.y = Mathf.Clamp(panel.y, Margin, Mathf.Max(Margin, screenHeight - panel.height - Margin));
            panel = Snap(panel);
        }

        /// <summary>One caste's missions. With <paramref name="draw"/> false it only measures; returns the y below it.</summary>
        float MissionSection(Rect panel, float y, string title, AntRole role, Mission[] missions, Color tint, bool draw)
        {
            const float minRowHeight = 46f;
            const float buttonsWidth = 120f;
            float textWidth = panel.width - 32f - buttonsWidth - 16f;
            if (!draw)
            {
                y += 32f;
                foreach (Mission mission in missions)
                    y += Mathf.Max(minRowHeight, 22f + font.MeasureHeight(Missions.DescriptionOf(mission), small, textWidth) + 8f);
                return y;
            }

            Colony colony = game.Colony;
            float left = panel.x + 16f;
            float innerWidth = panel.width - 32f;
            int free = colony.CountOnMission(Mission.Auto, role);

            // Section header: a darker strip with the caste and how many are still free to assign.
            Rect strip = Snap(new Rect(left - 4f, y, innerWidth + 8f, 24f));
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.3f);
            GUI.DrawTexture(strip, Texture2D.whiteTexture);
            GUI.color = previous;
            var caste = CasteArt(role);
            Icon(new Vector2(left + 10f, y + 12f), caste.sprite, caste.tint, maxSize: 18f);
            Text(new Rect(left + 26f, y, 200f, 24f), $"{title}  {colony.CountAnts(role)}", Cream, medium, TextAnchor.MiddleLeft);
            Text(new Rect(left, y, innerWidth - 4f, 24f), $"{free} free", free > 0 ? Good : Muted, small, TextAnchor.MiddleRight);
            y += 32f;

            foreach (Mission mission in missions)
            {
                int count = colony.CountOnMission(mission, role);
                int busy = colony.CountBusyOnMission(mission, role);
                Text(new Rect(left, y + 2f, textWidth, 16f), Missions.NameOf(mission), count > 0 ? Gold : Cream, medium);
                if (count > 0)
                {
                    // How many are actually working, so an idle mission (e.g. no hungry larvae) is easy to spot.
                    string activity = busy == count ? "all working" : busy == 0 ? "waiting for work" : $"{busy} working, {count - busy} waiting";
                    float nameWidth = font.Measure(Missions.NameOf(mission), medium);
                    Text(new Rect(left + nameWidth + 12f, y + 5f, textWidth - nameWidth - 12f, 14f), activity, busy > 0 ? Good : Muted, small);
                }
                float descriptionHeight = Text(new Rect(left, y + 22f, textWidth, 200f), Missions.DescriptionOf(mission), Muted, small);
                float rowHeight = Mathf.Max(minRowHeight, 22f + descriptionHeight + 8f);

                float buttonsX = panel.xMax - 16f - buttonsWidth;
                if (SmallButton(new Rect(buttonsX, y + 4f, 32f, 30f), "-", count > 0)) colony.ReleaseMission(mission);
                Text(new Rect(buttonsX + 32f, y + 4f, 56f, 30f), count.ToString(), count > 0 ? Cream : Muted, large, TextAnchor.MiddleCenter);
                // + takes a free ant first, otherwise one from another mission.
                bool canAdd = count < colony.CountAnts(role);
                if (SmallButton(new Rect(buttonsX + 88f, y + 4f, 32f, 30f), "+", canAdd)) colony.AssignMission(mission);
                y += rowHeight;
            }
            return y;
        }

        // ---------- Top right: music picker ----------

        /// <summary>Buttons this narrow or narrower make no click sound.</summary>
        const float SilentButtonSize = 40f;

        /// <summary>Two compact HUD buttons; the playlist and wave option live in the music window.</summary>
        float DrawMusicControls(float screenWidth, float top)
        {
            if (game.State != GameState.Playing) return 0f;
            const float width = 34f;
            const float height = 34f;
            const float gap = 8f;
            float x = screenWidth - Margin - width * 2f - gap;
            var music = Snap(new Rect(x, top, width, height));
            var pause = Snap(new Rect(x + width + gap, top, width, height));
            uiRects.Add(music);
            uiRects.Add(pause);
            if (HudIconButton(music, MusicGlyph, game.Audio != null, showMusic))
            {
                bool open = !showMusic;
                ClosePanels();
                showMusic = open;
                if (open) musicFirstTrack = Mathf.Max(0, game.Audio.PlaylistTrack - 2);
            }
            if (HudIconButton(pause, game.IsPaused ? PlayGlyph : PauseGlyph, true, game.IsPaused))
            {
                ClosePanels();
                game.TogglePause();
            }
            return top + height;
        }

        // Code-drawn pixel icons stay crisp and do not depend on font glyph availability.
        static readonly string[] MusicGlyph =
        {
            "....########", "....########", "....##....##", "....##....##",
            "....##....##", "....##....##", "....##....##", "..####..####",
            ".#####.#####", ".####..####.", "............", "............"
        };
        static readonly string[] PauseGlyph =
        {
            ".###..###.", ".###..###.", ".###..###.", ".###..###.", ".###..###.",
            ".###..###.", ".###..###.", ".###..###.", ".###..###.", ".###..###."
        };
        static readonly string[] PlayGlyph =
        {
            "..#.......", "..##......", "..###.....", "..####....", "..#####...",
            "..#####...", "..####....", "..###.....", "..##......", "..#......."
        };

        bool HudIconButton(Rect rect, string[] glyph, bool enabled, bool active)
        {
            bool clicked;
            bool held;
            if (game.UiSkin != null && game.UiSkin.HasSlots)
                clicked = SlotButton(rect, enabled, active, lockedLook: false, out _, out held);
            else
            {
                bool previous = GUI.enabled;
                GUI.enabled = previous && enabled;
                clicked = HudButton(rect, buttonStyle);
                held = MouseOver(rect) && GUIUtility.hotControl != 0;
                GUI.enabled = previous;
            }
            if (enabled && MouseOver(rect)) NoteHover(rect, particles: false);
            float pixel = Mathf.Max(1f, Mathf.Floor(Mathf.Min((rect.width - 14f) / glyph[0].Length,
                (rect.height - 14f) / glyph.Length) * guiScale)) / guiScale;
            float left = rect.center.x - glyph[0].Length * pixel * 0.5f;
            float top = rect.center.y - glyph.Length * pixel * 0.5f + (held ? pixel : 0f);
            for (int row = 0; row < glyph.Length; row++)
                for (int column = 0; column < glyph[row].Length; column++)
                    if (glyph[row][column] == '#')
                        FillRect(Snap(new Rect(left + column * pixel, top + row * pixel, pixel, pixel)), enabled ? Cream : Muted);
            return clicked;
        }

        /// <summary>A tick box with a label on a dark strip. Returns true when clicked.</summary>
        bool Tick(Rect rect, string label, bool on)
        {
            bool hovered = MouseOver(rect);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, hovered ? 0.55f : 0.4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            uiRects.Add(rect);

            const float box = 12f;
            var boxRect = Snap(new Rect(rect.x + 6f, rect.center.y - box * 0.5f, box, box));
            GUI.color = Cream;
            GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.16f, 0.1f, 0.06f);
            GUI.DrawTexture(Snap(new Rect(boxRect.x + 2f, boxRect.y + 2f, box - 4f, box - 4f)), Texture2D.whiteTexture);
            if (on)
            {
                GUI.color = Good;
                GUI.DrawTexture(Snap(new Rect(boxRect.x + 3f, boxRect.y + 3f, box - 6f, box - 6f)), Texture2D.whiteTexture);
            }
            GUI.color = previous;

            Text(new Rect(boxRect.xMax + 6f, rect.y, rect.width - box - 16f, rect.height), label, on ? Cream : Muted, small, TextAnchor.MiddleLeft);
            return HudButton(rect, GUIStyle.none);
        }

        /// <summary>A centred music player. Selecting a track keeps the window open.</summary>
        void DrawMusicWindow(float screenWidth)
        {
            AudioManager audio = game.Audio;
            if (audio == null) { showMusic = false; return; }
            AudioClip[] playlist = audio.Playlist;
            const float rowHeight = 28f;
            UiSkin skin = game.UiSkin;
            bool hasPortraits = skin != null && skin.musicPortraits != null && skin.musicPortraits.Length > 0;
            float galleryWidth = hasPortraits && screenWidth >= 700f ? 200f : 0f;
            float width = Mathf.Min(520f + galleryWidth, screenWidth - Margin * 2f);
            float playerWidth = width - galleryWidth;
            float height = Mathf.Min(232f + playlist.Length * rowHeight, screenHeight - Margin * 2f);
            if (!musicPositionSet)
            {
                musicPosition = new Vector2((screenWidth - width) * 0.5f, (screenHeight - height) * 0.5f);
                musicPositionSet = true;
            }
            var panel = new Rect(musicPosition.x, musicPosition.y, width, height);
            DragWindow(ref panel, screenWidth, "MusicDrag", ref musicDragControl, ref musicDragOffset);
            musicPosition = panel.position;
            Panel(panel);
            Text(new Rect(panel.x + 18f, panel.y + 14f, width - 240f, 28f), "Music", Gold, large);
            Text(new Rect(panel.xMax - 212f, panel.y + 20f, 160f, 16f), "Drag title to move", Muted, small, TextAnchor.MiddleRight);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 12f, 28f, 28f)))
            {
                ClosePanels();
                return;
            }

            string playing = audio.NowPlaying != null ? PrettyTrackName(audio.NowPlaying.name) : "No track playing";
            Text(new Rect(panel.x + 18f, panel.y + 52f, playerWidth - 36f, 20f), "Now playing: " + playing, Cream, medium);
            string status = audio.Muted ? "Audio is muted in Settings."
                : audio.PlaylistPlaying ? "Choose a track below."
                : "Event music is playing; your selection resumes afterwards.";
            Text(new Rect(panel.x + 18f, panel.y + 78f, playerWidth - 36f, 26f), status, Muted, small);
            float half = (playerWidth - 44f) * 0.5f;
            bool hasTracks = playlist.Length > 0;
            if (SmallButton(new Rect(panel.x + 18f, panel.y + 110f, half, 34f), "< Previous", hasTracks))
            {
                audio.PickTrack(audio.PlaylistTrack < 0 ? 0 : audio.PlaylistTrack - 1);
                musicFirstTrack = Mathf.Max(0, audio.PlaylistTrack - 2);
            }
            if (SmallButton(new Rect(panel.x + 26f + half, panel.y + 110f, half, 34f), "Next >", hasTracks))
            {
                audio.PickTrack(audio.PlaylistTrack + 1);
                musicFirstTrack = Mathf.Max(0, audio.PlaylistTrack - 2);
            }
            if (Tick(new Rect(panel.x + 18f, panel.y + 154f, playerWidth - 36f, 28f), "Wave music", audio.WaveMusic))
                audio.SetWaveMusic(!audio.WaveMusic);

            float top = panel.y + 192f;
            int visibleRows = Mathf.Max(1, Mathf.FloorToInt((panel.yMax - 40f - top) / rowHeight));
            int maxFirst = Mathf.Max(0, playlist.Length - visibleRows);
            musicFirstTrack = Mathf.Clamp(musicFirstTrack, 0, maxFirst);
            if (Event.current.type == EventType.ScrollWheel && panel.Contains(PointerPosition()))
            {
                musicFirstTrack = Mathf.Clamp(musicFirstTrack + (Event.current.delta.y > 0f ? 1 : -1), 0, maxFirst);
                Event.current.Use();
            }
            if (!hasTracks)
                Text(new Rect(panel.x + 18f, top, playerWidth - 36f, 24f), "No colony tracks available.", Muted, small);
            for (int slot = 0; slot < visibleRows && musicFirstTrack + slot < playlist.Length; slot++)
            {
                int i = musicFirstTrack + slot;
                if (playlist[i] == null) continue;
                var row = Snap(new Rect(panel.x + 18f, top + slot * rowHeight, playerWidth - 36f, rowHeight));
                bool current = i == audio.PlaylistTrack;
                bool hovered = MouseOver(row);
                if (current || hovered)
                {
                    Color previous = GUI.color;
                    GUI.color = current ? new Color(1f, 0.8f, 0.3f, 0.25f) : new Color(1f, 1f, 1f, 0.1f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = previous;
                }
                Text(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), $"{i + 1}. {PrettyTrackName(playlist[i].name)}",
                    current ? Gold : hovered ? Color.white : Cream, small, TextAnchor.MiddleLeft);
                if (HudButton(row, GUIStyle.none)) audio.PickTrack(i);
            }
            if (galleryWidth > 0f)
                DrawMusicGallery(new Rect(panel.x + playerWidth + 4f, panel.y + 52f, galleryWidth - 22f, height - 70f), audio, skin);
            if (maxFirst > 0)
            {
                float y = panel.yMax - 32f;
                if (SmallButton(new Rect(panel.x + 18f, y, 32f, 26f), "^", musicFirstTrack > 0)) musicFirstTrack--;
                if (SmallButton(new Rect(panel.x + playerWidth - 50f, y, 32f, 26f), "v", musicFirstTrack < maxFirst)) musicFirstTrack++;
                Text(new Rect(panel.x + 56f, y, playerWidth - 112f, 26f),
                    $"{musicFirstTrack + 1}-{Mathf.Min(playlist.Length, musicFirstTrack + visibleRows)} / {playlist.Length}   (scroll)",
                    Muted, small, TextAnchor.MiddleCenter);
            }
        }

        void DrawMusicGallery(Rect area, AudioManager audio, UiSkin skin)
        {
            UiSkin.MusicPortrait[] portraits = skin.musicPortraits;
            AudioClip track = audio.NowPlaying;
            if (portraitIndex < 0 || portraitTrack != track || portraitSelection != audio.PlaylistTrack)
            {
                previousPortrait = portraitIndex;
                portraitIndex = (portraitIndex + 1) % portraits.Length;
                portraitTrack = track;
                portraitSelection = audio.PlaylistTrack;
                portraitChangedAt = Time.unscaledTime;
            }
            portraitIndex %= portraits.Length;
            WoodBox(area);
            Text(new Rect(area.x + 8f, area.y + 12f, area.width - 16f, 20f), "The colony", Gold, medium, TextAnchor.MiddleCenter);
            var image = new Rect(area.x + 10f, area.y + 44f, area.width - 20f, area.height - 86f);
            float blend = previousPortrait < 0 ? 1f : Mathf.Clamp01((Time.unscaledTime - portraitChangedAt) / 0.6f);
            Color before = GUI.color;
            Texture2D previousImage = previousPortrait >= 0 && previousPortrait < portraits.Length
                ? MusicPortraitImage(portraits[previousPortrait]) : null;
            if (previousImage != null)
            {
                GUI.color = new Color(before.r, before.g, before.b, before.a * (1f - blend));
                GUI.DrawTexture(image, previousImage, ScaleMode.ScaleToFit);
            }
            UiSkin.MusicPortrait current = portraits[portraitIndex];
            Texture2D currentImage = MusicPortraitImage(current);
            if (currentImage != null)
            {
                GUI.color = new Color(before.r, before.g, before.b, before.a * blend);
                GUI.DrawTexture(image, currentImage, ScaleMode.ScaleToFit);
            }
            GUI.color = before;
            Text(new Rect(area.x + 8f, area.yMax - 32f, area.width - 16f, 20f), current?.caption ?? "", Cream, medium, TextAnchor.MiddleCenter);
        }

        static Texture2D MusicPortraitImage(UiSkin.MusicPortrait portrait)
        {
            if (portrait == null) return null;
            return portrait.musicImage != null ? portrait.musicImage : portrait.image;
        }

        // ---------- Tutorial ----------

        /// <summary>
        /// The tutorial pop-up over a dimmed, paused game: the step's title and text, its goal, "Got it" and
        /// "Skip tutorial". Enter or Space also mean "Got it".
        /// </summary>
        void DrawTutorialPopup(float screenWidth, Tutorial tutorial)
        {
            Tutorial.Step step = tutorial.Current;
            if (step == null) return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = previous;
            var everything = new Rect(0f, 0f, screenWidth, screenHeight);
            uiRects.Add(everything);
            // Every other button is off while the pop-up is open.
            if (Event.current.type == EventType.Repaint) nextCoverRects.Add(everything);

            const float width = 480f;
            const float padding = 22f;
            float textWidth = width - padding * 2f;
            string body = step.Body;
            if (!tutorial.CanSkipStep) body += "\n\n" + tutorial.SkipExplanation;
            float bodyHeight = font.MeasureHeight(body, medium, textWidth);
            bool hasGoal = !string.IsNullOrEmpty(step.Goal);
            float goalHeight = hasGoal ? font.MeasureHeight("Your goal: " + step.Goal, small, textWidth) + 12f : 0f;
            const float buttonHeight = 46f;
            float height = padding + 32f + 10f + bodyHeight + 14f + goalHeight + buttonHeight + padding;
            var panel = Snap(new Rect((screenWidth - width) * 0.5f, Mathf.Max(70f, (screenHeight - height) * 0.42f), width, height));
            Panel(panel);

            float x = panel.x + padding;
            float y = panel.y + padding;
            Text(new Rect(x, y, textWidth - 60f, 32f), step.Title, Gold, large, TextAnchor.MiddleLeft);
            Text(new Rect(x, y, textWidth, 32f), $"{tutorial.StepNumber}/{tutorial.StepCount}", Muted, small, TextAnchor.MiddleRight);
            y += 42f;
            y += Text(new Rect(x, y, textWidth, bodyHeight + 4f), body, Cream, medium) + 14f;
            if (hasGoal)
            {
                Text(new Rect(x, y, textWidth, goalHeight), "Your goal: " + step.Goal, Good, small);
                y += goalHeight;
            }

            bool last = tutorial.StepNumber >= tutorial.StepCount;
            float third = (textWidth - 16f) / 3f;
            if (!last && SmallButton(new Rect(x, y, third, buttonHeight), "Skip tutorial", true)) { tutorial.Skip(); return; }
            if (!last && SmallButton(new Rect(x + third + 8f, y, third, buttonHeight), tutorial.CanSkipStep ? "Skip step" : "Required step", tutorial.CanSkipStep && tutorial.CanContinue)) { tutorial.SkipStep(); return; }
            if (SmallButton(new Rect(x + (third + 8f) * 2f, y, third, buttonHeight), last ? "Let's go!" : hasGoal ? "Try it" : "Continue", tutorial.CanContinue)) tutorial.Continue();
        }

        /// <summary>While the player works on a step: a small goal bar under the wave banner, and the matching button blinking.</summary>
        void DrawTutorialGoal(float screenWidth, Tutorial tutorial)
        {
            Tutorial.Step step = tutorial.Current;
            if (step == null || string.IsNullOrEmpty(step.Goal)) return;

            string label = $"Tutorial {tutorial.StepNumber}/{tutorial.StepCount}";
            float width = Mathf.Min(screenWidth - 2f * Margin, Mathf.Max(520f, font.Measure(step.Goal, medium) + 40f));
            // Under the wave banner and the line where messages appear.
            var bar = Snap(new Rect((screenWidth - width) * 0.5f, Margin + 34f + 52f, width, 116f));
            WoodBox(bar);
            uiRects.Add(bar);
            Text(new Rect(bar.x, bar.y + 6f, bar.width, 14f), label, Gold, small, TextAnchor.UpperCenter);
            Text(new Rect(bar.x + 10f, bar.y + 20f, bar.width - 20f, 20f),
                tutorial.GoalCompleted ? "Done! Take your time, then choose Next." : step.Goal,
                tutorial.GoalCompleted ? Good : Cream, medium, TextAnchor.MiddleCenter);
            Text(new Rect(bar.x + 10f, bar.y + 43f, bar.width - 20f, 30f), tutorial.ProgressHint, Muted, small, TextAnchor.UpperCenter);
            float buttonWidth = (bar.width - 38f) / 4f;
            float buttonX = bar.x + 10f;
            if (SmallButton(new Rect(buttonX, bar.y + 76f, buttonWidth, 34f), "Read again", true)) { tutorial.Review(); return; }
            buttonX += buttonWidth + 6f;
            if (SmallButton(new Rect(buttonX, bar.y + 76f, buttonWidth, 34f), tutorial.CanSkipStep ? "Skip step" : "Required step", tutorial.CanSkipStep)) { tutorial.SkipStep(); return; }
            buttonX += buttonWidth + 6f;
            if (SmallButton(new Rect(buttonX, bar.y + 76f, buttonWidth, 34f), "Skip tutorial", true)) { tutorial.Skip(); return; }
            buttonX += buttonWidth + 6f;
            if (SmallButton(new Rect(buttonX, bar.y + 76f, buttonWidth, 34f), "Next", tutorial.GoalCompleted)) { tutorial.Advance(); return; }

            if (step.ShowsGatherDemo) DrawGatherDemo(tutorial.DemoFood);

            // The button to press blinks (stepped, a few times a second) with a bouncing arrow over it.
            if (!string.IsNullOrEmpty(step.HighlightKey) && actionRects.TryGetValue(step.HighlightKey, out Rect button))
            {
                int frame = Mathf.FloorToInt(Time.unscaledTime * 3f);
                if ((frame & 1) == 0)
                {
                    const float thickness = 3f;
                    Rect outer = new Rect(button.x - 5f, button.y - 5f, button.width + 10f, button.height + 10f);
                    FillRect(new Rect(outer.x, outer.y, outer.width, thickness), Gold);
                    FillRect(new Rect(outer.x, outer.yMax - thickness, outer.width, thickness), Gold);
                    FillRect(new Rect(outer.x, outer.y, thickness, outer.height), Gold);
                    FillRect(new Rect(outer.xMax - thickness, outer.y, thickness, outer.height), Gold);
                }
                float bounce = (frame & 1) == 0 ? 0f : 4f;
                Text(new Rect(button.x, button.y - 30f + bounce, button.width, 20f), "V", Gold, large, TextAnchor.UpperCenter);
            }
        }

        Sprite demoCursor;
        Sprite demoCursorPressed;

        /// <summary>
        /// A pixel cursor acting out the Gather goal over a food pile, in a stepped loop (6 frames a second):
        /// it glides in, presses, holds and drags a box over the pile, lets go, and the pile shows as picked.
        /// </summary>
        void DrawGatherDemo(FoodSource food)
        {
            if (food == null || game.CameraRig.View != CameraView.Surface || game.Gather.MarkedCount > 0) return;
            Camera view = game.CameraRig.Camera;
            if (view == null) return;

            if (demoCursor == null)
            {
                string[] arrow =
                {
                    "k.......",
                    "kk......",
                    "kwk.....",
                    "kwwk....",
                    "kwwwk...",
                    "kwwwwk..",
                    "kwwwwwk.",
                    "kwwwkkkk",
                    "kwkwk...",
                    "kk.kwk..",
                    "k...kwk.",
                    ".....k..",
                };
                demoCursor = PixelArt.FromMap("Demo Cursor", arrow);
                demoCursorPressed = PixelArt.FromMap("Demo Cursor Pressed", System.Array.ConvertAll(arrow, row => row.Replace('w', 'y')));
            }

            Vector3 screen = view.WorldToScreenPoint(food.Position);
            if (screen.z < 0f) return;
            var target = new Vector2(screen.x / guiScale, (Screen.height - screen.y) / guiScale);
            SpriteRenderer pile = food.GetComponentInChildren<SpriteRenderer>();
            float worldHalf = pile != null ? pile.bounds.extents.x : 0.5f;
            float half = Mathf.Max(18f, worldHalf * Screen.height / (view.orthographicSize * 2f) / guiScale + 8f);

            const int loop = 18;
            int frame = Mathf.FloorToInt(Time.unscaledTime * 6f) % loop;
            Vector2 from = target + new Vector2(90f, 70f);
            Vector2 cornerA = target - new Vector2(half, half);
            Vector2 cornerB = target + new Vector2(half, half);

            Vector2 cursor;
            bool pressed = false;
            bool dragging = false;
            bool picked = false;
            if (frame < 4) cursor = Vector2.Lerp(from, cornerA, (frame + 1) / 4f);          // glide in
            else if (frame == 4) { cursor = cornerA; pressed = true; }                       // press
            else if (frame < 10) { cursor = Vector2.Lerp(cornerA, cornerB, (frame - 4) / 5f); pressed = dragging = true; } // hold and drag
            else if (frame < 15) { cursor = cornerB; picked = true; }                        // let go: picked
            else return;                                                                       // a short rest

            Color gold = Gold;
            if (dragging)
            {
                Rect box = Rect.MinMaxRect(cornerA.x, cornerA.y, cursor.x, cursor.y);
                FillRect(box, new Color(gold.r, gold.g, gold.b, 0.15f));
                Outline(box, gold, 2f);
            }
            if (picked && (frame & 1) == 0) Outline(Rect.MinMaxRect(cornerA.x, cornerA.y, cornerB.x, cornerB.y), gold, 3f);
            if (pressed && frame == 4) Outline(new Rect(cursor.x - 6f, cursor.y - 6f, 12f, 12f), Cream, 2f); // click ring

            // The arrow's tip is its top-left pixel; drawn at whole pixels (3 UI units each).
            const float pixel = 3f;
            Sprite sprite = pressed ? demoCursorPressed : demoCursor;
            var rect = Snap(new Rect(cursor.x, cursor.y, sprite.rect.width * pixel, sprite.rect.height * pixel));
            GUI.DrawTexture(rect, sprite.texture);
            Text(new Rect(cursor.x + 26f, cursor.y + 18f, 120f, 14f), picked ? "picked!" : pressed ? "hold + drag" : "", gold, small);
        }

        void Outline(Rect rect, Color color, float thickness)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            FillRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            FillRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        // ---------- Sound (menus) ----------

        /// <summary>The player's volumes: master, music and effects in steps of 10%, and mute. Remembered between sessions.</summary>
        void DrawSound(float screenWidth)
        {
            AudioManager audio = game.Audio;
            if (audio == null) return;

            const float width = 420f;
            const float rowHeight = 44f;
            var panel = new Rect((screenWidth - width) * 0.5f, Mathf.Max(60f, screenHeight * 0.5f - 150f), width, 52f + rowHeight * 3f + 160f);
            Panel(panel);
            Text(new Rect(panel.x + 16f, panel.y + 12f, width - 70f, 24f), "Settings", Gold, large);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 10f, 28f, 28f))) showSound = false;

            float master = audio.PlayerMaster;
            float musicVolume = audio.PlayerMusic;
            float effects = audio.PlayerEffects;
            float y = panel.y + 54f;
            master = VolumeRow(panel, y, "Master", master);
            musicVolume = VolumeRow(panel, y + rowHeight, "Music", musicVolume);
            effects = VolumeRow(panel, y + rowHeight * 2f, "Effects", effects);
            if (!Mathf.Approximately(master, audio.PlayerMaster) || !Mathf.Approximately(musicVolume, audio.PlayerMusic) || !Mathf.Approximately(effects, audio.PlayerEffects))
                audio.SetPlayerVolumes(master, musicVolume, effects);

            y += rowHeight * 3f + 4f;
            if (SmallButton(new Rect(panel.x + 16f, y, 150f, 34f), audio.Muted ? "Unmute" : "Mute all", true)) audio.SetMuted(!audio.Muted);
            AudioClip track = audio.NowPlaying;
            string trackName = track != null ? PrettyTrackName(track.name) : "-";
            Text(new Rect(panel.x + 16f, y + 46f, width - 32f, 14f), $"Now playing: {trackName}", Muted, small);
            if (SmallButton(new Rect(panel.x + 16f, y + 70f, width - 32f, 36f),
                "Autosave after waves: " + (game.AutoSaveAfterWave ? "ON" : "OFF"), true))
                game.SetAutoSaveAfterWave(!game.AutoSaveAfterWave);
            Text(new Rect(panel.x + 16f, y + 112f, width - 32f, 24f),
                "Uses the same slot as Save game. F5 still saves manually.", Muted, small);
        }

        /// <summary>A label, a bar and - / + buttons. Returns the new volume.</summary>
        float VolumeRow(Rect panel, float y, string label, float volume)
        {
            float left = panel.x + 16f;
            Text(new Rect(left, y + 8f, 100f, 20f), label, Cream, medium);
            float barX = left + 104f;
            float barWidth = panel.width - 32f - 104f - 120f;
            Bar(new Rect(barX, y + 12f, barWidth, 10f), volume, Gold);

            float buttonsX = panel.xMax - 16f - 112f;
            int steps = Mathf.RoundToInt(volume * 10f);
            if (SmallButton(new Rect(buttonsX, y + 2f, 32f, 30f), "-", steps > 0)) steps--;
            Text(new Rect(buttonsX + 32f, y + 2f, 48f, 30f), $"{steps * 10}", Cream, small, TextAnchor.MiddleCenter);
            if (SmallButton(new Rect(buttonsX + 80f, y + 2f, 32f, 30f), "+", steps < 10)) steps++;
            return steps / 10f;
        }

        /// <summary>"11_fallen_leaves_loop" becomes "Fallen leaves".</summary>
        static string PrettyTrackName(string clipName)
        {
            string name = clipName;
            int underscore = name.IndexOf('_');
            if (underscore > 0 && char.IsDigit(name[0])) name = name.Substring(underscore + 1);
            name = name.Replace("_loop", "").Replace('_', ' ');
            return name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1) : clipName;
        }

        // ---------- Help (H) ----------

        static readonly string[] HelpBasics =
        {
            "Workers bring food home. Berries and acorns are food; leaves become food in a Fungus Garden.",
            "Every ant eats. Keep the net food per minute above zero.",
            "Press 1 2 3 to raise ants: the queen lays an egg in a cradle and workers feed the larva.",
            "Defend the ant hill from 10 waves. If the queen dies, you lose.",
        };

        static readonly string[,] HelpSurfaceKeys =
        {
            { "WASD / wheel", "pan / zoom" },
            { "Drag / click", "select soldiers" },
            { "Double-click", "all soldiers of that kind" },
            { "Right-click", "move or attack" },
            { "E / Q", "all / halve soldiers" },
            { "Z", "raise an acid spitter" },
            { "4 / 5", "wall / tower" },
            { "6", "gather: pick food" },
        };

        static readonly string[,] HelpNestKeys =
        {
            { "WASD / wheel", "pan / zoom" },
            { "4", "dig (drag)" },
            { "5 6 7", "storage, cradle, garden" },
            { "X", "remove (drag)" },
            { "Right-click", "put tool down" },
        };

        const string HelpEverywhere = "Everywhere:  1 2 3 raise ants   Tab switch view   N next wave   R report   M missions   F5 save   F9 load   Esc pause";

        void DrawHelp(float screenWidth)
        {
            const float width = 600f;
            // Measure first (wrapped lines take more room), then draw.
            var panel = new Rect((screenWidth - width) * 0.5f, 92f, width, 0f);
            panel.height = HelpContent(panel, draw: false) - panel.y + 14f;
            Panel(panel);
            Text(new Rect(panel.x + 16f, panel.y + 12f, width - 70f, 24f), "How to play", Gold, large);
            if (CloseButton(new Rect(panel.xMax - 40f, panel.y + 10f, 28f, 28f))) showHelp = false;
            HelpContent(panel, draw: true);
        }

        /// <summary>Lays out the help text; draws it only with <paramref name="draw"/>. Returns the y below it.</summary>
        float HelpContent(Rect panel, bool draw)
        {
            float textWidth = panel.width - 32f;
            float left = panel.x + 16f;
            float y = panel.y + 48f;
            foreach (string line in HelpBasics) y += Paragraph(new Rect(left, y, textWidth, 0f), "- " + line, Cream, small, draw) + 5f;

            y += 8f;
            float column = (panel.width - 48f) * 0.5f;
            float right = left + column + 16f;
            if (draw)
            {
                Text(new Rect(left, y, column, 20f), "Surface", Gold, medium);
                Text(new Rect(right, y, column, 20f), "Nest", Gold, medium);
            }
            y += 24f;

            float surfaceBottom = KeyRows(left, y, column, HelpSurfaceKeys, draw);
            float nestBottom = KeyRows(right, y, column, HelpNestKeys, draw);
            y = Mathf.Max(surfaceBottom, nestBottom) + 12f;
            y += Paragraph(new Rect(left, y, textWidth, 0f), HelpEverywhere, Muted, small, draw);
            return y;
        }

        /// <summary>A column of key / action rows; the key column is as wide as its longest key. Returns the y below it.</summary>
        float KeyRows(float x, float y, float width, string[,] rows, bool draw)
        {
            float keyWidth = 0f;
            for (int i = 0; i < rows.GetLength(0); i++) keyWidth = Mathf.Max(keyWidth, font.Measure(rows[i, 0], small));
            keyWidth += 10f;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float actionHeight = Paragraph(new Rect(x + keyWidth, y, width - keyWidth, 0f), rows[i, 1], Cream, small, draw);
                if (draw) Text(new Rect(x, y, keyWidth, 14f), rows[i, 0], Gold, small);
                y += Mathf.Max(actionHeight, PixelFont.LineHeight * small) + 4f;
            }
            return y;
        }

        /// <summary>Wrapped text at the top of <paramref name="rect"/> (its height is ignored); draws only with <paramref name="draw"/>. Returns its height.</summary>
        float Paragraph(Rect rect, string text, Color color, float size, bool draw)
        {
            rect.height = 1000f;
            return draw ? Text(rect, text, color, size) : font.MeasureHeight(text, size, rect.width);
        }

        // ---------- World labels and overlays ----------

        /// <summary>
        /// In the nest view, the room under the mouse gets a short label: what it is and how it's doing.
        /// Other rooms stay unlabelled, except for a small red "!" over hungry larvae so they aren't missed.
        /// </summary>
        void DrawRoomLabels(float scale)
        {
            if (game.CameraRig.View != CameraView.Nest || game.CameraRig.Fade > 0.5f) return;

            Camera viewCamera = game.CameraRig.Camera;
            Room hovered = HoveredRoom();
            foreach (Room room in game.Nest.Rooms)
            {
                bool hungry = room.IsBuilt && room.Type == RoomType.Cradle && room.Brood != null && room.Brood.IsWaitingForFood;
                if (room != hovered && !hungry) continue;

                Vector3 screenPoint = viewCamera.WorldToScreenPoint(room.LabelPoint);
                if (screenPoint.z < 0f) continue;

                // Screen space has y going up; GUI space has y going down.
                var anchor = new Vector2(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
                if (room == hovered)
                {
                    // A small dark card behind the text, so it reads over any soil.
                    string label = DescribeRoom(room, out Color color);
                    float textWidth = font.Measure(label, medium);
                    float textHeight = 7f * medium;
                    Rect card = Snap(new Rect(anchor.x - textWidth * 0.5f - 10f, anchor.y - textHeight - 14f, textWidth + 20f, textHeight + 14f));
                    GUI.Box(card, GUIContent.none, cardStyle); // not a click-blocking panel: it follows the mouse
                    Text(new Rect(card.x, card.y + 7f, card.width, textHeight), label, color, medium, TextAnchor.UpperCenter);
                }
                else
                {
                    Text(new Rect(anchor.x - 40f, anchor.y - 7f * large, 80f, 7f * large), "!", Bad, large, TextAnchor.UpperCenter);
                }
            }
        }

        /// <summary>
        /// The details card of the clicked room: what it does and how it's doing right now (stored food for storage,
        /// the next harvest for a garden, the young one in a cradle, the queen in her chamber). The room gets a gold
        /// frame. Click elsewhere, the X or Esc to close it.
        /// </summary>
        void DrawRoomInfo(float scale)
        {
            Room room = selectedRoom;
            if (room == null) return;
            if (room.IsRemoved || game.CameraRig.View != CameraView.Nest || game.CameraRig.Fade > 0.5f)
            {
                if (room.IsRemoved) selectedRoom = null;
                return;
            }

            Camera viewCamera = game.CameraRig.Camera;
            Rect world = room.WorldRect;
            Vector3 low = viewCamera.WorldToScreenPoint(world.min);
            Vector3 high = viewCamera.WorldToScreenPoint(world.max);
            var frame = Rect.MinMaxRect(low.x / scale, (Screen.height - high.y) / scale, high.x / scale, (Screen.height - low.y) / scale);
            if (Event.current.type == EventType.Repaint) Outline(Snap(frame), Gold, 2f);

            var lines = new List<(string text, Color color)>();
            float? barFraction = null;
            Color barColor = Gold;
            string barLabel = "";
            GameSettings settings = game.Settings;
            Colony colony = game.Colony;

            if (!room.IsBuilt)
            {
                lines.Add(("Being built", Gold));
                lines.Add((colony.CountAnts(AntRole.Builder) > 0 ? "Builders are on it." : "Needs a builder (3).", Muted));
                barFraction = room.BuildProgress;
                barLabel = $"{Mathf.RoundToInt(room.BuildProgress * 100f)}%";
            }
            else
            {
                switch (room.Type)
                {
                    case RoomType.Storage:
                    {
                        int capacity = Mathf.Max(1, colony.StorageCapacity);
                        int stored = colony.Food + colony.Leaves;
                        lines.Add(($"Holds {settings.storageRoomCapacity} more food or leaves", Cream));
                        lines.Add(($"Colony stores: {colony.Food} food, {colony.Leaves} leaves", Cream));
                        lines.Add((colony.FreeSpace > 0 ? $"{colony.FreeSpace} free space" : "Full! Build another storage.", colony.FreeSpace > 0 ? Good : Bad));
                        barFraction = stored / (float)capacity;
                        barLabel = $"{stored}/{capacity}";
                        break;
                    }
                    case RoomType.Garden:
                    {
                        string state = room.IsWorking ? "Growing fungus" : colony.Leaves <= 0 ? "Idle: no leaves" : "Idle: storage full";
                        lines.Add((state, room.IsWorking ? Good : Bad));
                        lines.Add(($"1 leaf -> {settings.gardenFoodPerLeaf} food every {settings.gardenCycleTime:0.#}s", Cream));
                        lines.Add(($"Leaves waiting: {colony.Leaves}", colony.Leaves > 0 ? Cream : Muted));
                        barFraction = room.IsWorking ? room.GardenTimer / Mathf.Max(0.01f, settings.gardenCycleTime) : 0f;
                        barColor = Good;
                        barLabel = "next food";
                        break;
                    }
                    case RoomType.Cradle:
                    {
                        Brood brood = room.Brood;
                        if (brood == null)
                        {
                            lines.Add((room.AwaitingEgg ? "The queen is coming to lay an egg." : "Empty: ready for an egg.", Muted));
                            lines.Add(("Raise ants with 1 2 3 or Z.", Muted));
                        }
                        else
                        {
                            string what = brood.Stage == BroodStage.Egg ? "egg" : "larva";
                            lines.Add(($"{brood.Caste} {what}", Gold));
                            if (brood.Stage == BroodStage.Larva)
                                lines.Add(($"Fed {brood.FoodFed}/{brood.FoodNeeded}", brood.IsWaitingForFood ? Bad : Cream));
                            lines.Add((brood.IsWaitingForFood ? "Hungry! Needs a larva feeder." : brood.HasNurse ? "Being fed." : "Growing.", brood.IsWaitingForFood ? Bad : Good));
                            barFraction = brood.Progress;
                            barColor = Brood.CasteColor(brood.Caste);
                            barLabel = "grown";
                        }
                        break;
                    }
                    case RoomType.Queen:
                    {
                        lines.Add(($"Health {Mathf.CeilToInt(colony.QueenHealth)}/{Mathf.CeilToInt(colony.QueenMaxHealth)}", Cream));
                        lines.Add(($"Eggs queued: {colony.RaiseQueue.Count}", Cream));
                        lines.Add(($"Holds {settings.baseStorage} food or leaves", Muted));
                        barFraction = colony.QueenHealth / Mathf.Max(1f, colony.QueenMaxHealth);
                        barLabel = "health";
                        break;
                    }
                    default:
                        lines.Add((room.Info.Description, Cream));
                        break;
                }
            }

            // The card sits above the room (or below it near the top of the screen), kept on screen.
            const float width = 250f;
            float textWidth = width - 20f;
            float height = 10f + 22f;
            foreach (var line in lines) height += font.MeasureHeight(line.text, small, textWidth) + 3f;
            if (barFraction.HasValue) height += 18f;
            height += 8f;
            float screenWidth = Screen.width / scale;
            float x = Mathf.Clamp(frame.center.x - width * 0.5f, Margin, screenWidth - width - Margin);
            float y = frame.y - height - 8f;
            if (y < Margin + 60f) y = frame.yMax + 8f;
            y = Mathf.Clamp(y, Margin, screenHeight - height - 110f);
            var card = Snap(new Rect(x, y, width, height));
            WoodBox(card);
            uiRects.Add(card);

            float left = card.x + 10f;
            float cursor = card.y + 8f;
            Text(new Rect(left, cursor, textWidth - 28f, 20f), room.Info.Name, Gold, medium, TextAnchor.MiddleLeft);
            if (SmallButton(new Rect(card.xMax - 30f, card.y + 6f, 22f, 22f), "X", true)) selectedRoom = null;
            cursor += 24f;
            foreach (var line in lines)
                cursor += Text(new Rect(left, cursor, textWidth, 200f), line.text, line.color, small) + 3f;
            if (barFraction.HasValue)
            {
                float labelWidth = font.Measure(barLabel, small) + 8f;
                GoldBar(new Rect(left, cursor + 4f, textWidth - labelWidth, 10f), Mathf.Clamp01(barFraction.Value), null);
                if (barColor != Gold)
                {
                    float filled = (textWidth - labelWidth - 2f) * Mathf.Clamp01(barFraction.Value);
                    if (filled > 0f) FillRect(new Rect(left + 1f, cursor + 5f, filled, 8f), barColor);
                }
                Text(new Rect(left, cursor, textWidth, 18f), barLabel, Muted, small, TextAnchor.MiddleRight);
            }
        }

        Room HoveredRoom()
        {
            if (Mouse.current == null) return null;
            Vector2 world = game.CameraRig.ScreenToWorld(Mouse.current.position.ReadValue());
            return game.Nest.RoomAt(game.Nest.WorldToTile(world));
        }

        static string DescribeRoom(Room room, out Color color)
        {
            color = Gold;
            if (!room.IsBuilt) return $"{room.Info.Name} (to build)";

            switch (room.Type)
            {
                case RoomType.Cradle:
                    Brood brood = room.Brood;
                    if (brood == null) return "Cradle (free)";
                    if (brood.IsWaitingForFood)
                    {
                        color = Bad;
                        return $"{brood.Caste} larva - hungry";
                    }
                    return brood.Stage == BroodStage.Egg ? $"{brood.Caste} egg" : $"{brood.Caste} larva";
                case RoomType.Garden:
                    if (!room.IsWorking) color = Muted;
                    return room.IsWorking ? "Garden" : "Garden (no leaves)";
                default:
                    return room.Info.Name;
            }
        }

        /// <summary>Which enemy each edge arrow last jumped to, so clicking again moves on to the next one.</summary>
        readonly int[] warningClicks = new int[4];

        /// <summary>
        /// Arrows at the screen edges counting enemies off screen on that side. Click one to look at the nearest of
        /// them; click again for the next.
        /// </summary>
        void DrawEnemyWarnings(float screenWidth)
        {
            IReadOnlyList<Enemy> enemies = game.Waves.Enemies;
            if (enemies.Count == 0 || game.CameraRig.View == CameraView.Nest) return;

            // Point at enemies that are off screen, on whichever side they are.
            Camera viewCamera = game.CameraRig.Camera;
            Vector2 center = viewCamera.transform.position;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;

            // 0 left, 1 right, 2 above, 3 below.
            var sides = new List<Enemy>[] { new List<Enemy>(), new List<Enemy>(), new List<Enemy>(), new List<Enemy>() };
            foreach (Enemy enemy in enemies)
            {
                if (enemy == null) continue;
                Vector2 offset = enemy.Position - center;
                if (Mathf.Abs(offset.x) <= halfWidth && Mathf.Abs(offset.y) <= halfHeight) continue;

                // Sort by which edge of the screen the enemy is beyond the most.
                if (Mathf.Abs(offset.x) / halfWidth > Mathf.Abs(offset.y) / halfHeight) sides[offset.x < 0f ? 0 : 1].Add(enemy);
                else sides[offset.y > 0f ? 2 : 3].Add(enemy);
            }

            float middle = screenHeight * 0.45f;
            var rects = new[]
            {
                new Rect(Margin, middle, 180f, 22f),
                new Rect(screenWidth - Margin - 180f, middle, 180f, 22f),
                new Rect(screenWidth * 0.5f - 100f, 52f, 200f, 22f),
                new Rect(screenWidth * 0.5f - 100f, screenHeight - 130f, 200f, 22f),
            };
            for (int side = 0; side < 4; side++)
            {
                int count = sides[side].Count;
                if (count == 0) continue;
                string noun = count == 1 ? "enemy" : "enemies";
                string label = side == 0 ? $"< {count} {noun}" : side == 1 ? $"{count} {noun} >" : side == 2 ? $"^ {count} {noun} ^" : $"v {count} {noun} v";
                TextAnchor anchor = side == 0 ? TextAnchor.MiddleLeft : side == 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                Rect rect = rects[side];
                bool hovered = MouseOver(rect);
                if (hovered)
                {
                    Color previous = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.35f);
                    GUI.DrawTexture(Snap(rect), Texture2D.whiteTexture);
                    GUI.color = previous;
                }
                Text(rect, label, hovered ? Cream : Bad, medium, anchor);
                uiRects.Add(rect);
                if (HudButton(rect, GUIStyle.none)) JumpToEnemy(sides[side], side, center);
            }
        }

        /// <summary>Turns the camera to the nearest enemy of a side, or the next one on each further click.</summary>
        void JumpToEnemy(List<Enemy> enemies, int side, Vector2 from)
        {
            enemies.Sort((a, b) => Vector2.Distance(a.Position, from).CompareTo(Vector2.Distance(b.Position, from)));
            Enemy enemy = enemies[warningClicks[side] % enemies.Count];
            warningClicks[side]++;
            game.CameraRig.FocusSurface(enemy.Position);
        }

        /// <summary>The box being dragged to select soldiers.</summary>
        /// <summary>
        /// When the mouse is over an ant: a small card by the cursor with its name, its kind and mission, what it's
        /// up to (WORKING, IDLE, FIGHTING...) and its health.
        /// </summary>
        void DrawAntCard()
        {
            if (Event.current.type != EventType.Repaint || game.CameraRig.Fade > 0f || Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (IsPointerOverUi(mouse) || game.Selection.IsDragging || game.Gather.IsDragging) return;

            Ant ant = AntUnderMouse(game.CameraRig.ScreenToWorld(mouse));
            if (ant == null) return;

            string kind = ant.Role == AntRole.Spitter ? "Acid spitter" : ant.Role.ToString();
            bool hasMission = ant.Role == AntRole.Worker || ant.Role == AntRole.Builder;
            string mission = hasMission ? (ant.Mission == Mission.Auto ? "Free" : Missions.NameOf(ant.Mission)) : "";
            string status = ant.Status.ToString().ToUpperInvariant();
            Color statusColor = ant.Status == AntState.Idle ? Muted
                : ant.Status == AntState.Fighting || ant.Status == AntState.Fleeing ? Bad
                : ant.Status == AntState.Working ? Good : Gold;

            const float width = 210f;
            string activity = ant.Activity;
            float activityHeight = string.IsNullOrEmpty(activity) ? 0f : font.MeasureHeight(activity, small, width - 20f) + 2f;
            float height = 10f + 20f + 16f + 18f + activityHeight + 16f + 6f;

            // Beside the cursor, kept on screen.
            var guiMouse = new Vector2(mouse.x / guiScale, (Screen.height - mouse.y) / guiScale);
            float screenWidth = Screen.width / guiScale;
            float x = guiMouse.x + 18f;
            float y = guiMouse.y + 14f;
            if (x + width > screenWidth - 4f) x = guiMouse.x - width - 12f;
            if (y + height > screenHeight - 4f) y = guiMouse.y - height - 8f;
            var card = Snap(new Rect(x, y, width, height));
            WoodBox(card);

            float left = card.x + 10f;
            float textWidth = card.width - 20f;
            float line = card.y + 8f;
            Text(new Rect(left, line, textWidth, 20f), ant.Name, Gold, medium, TextAnchor.MiddleLeft);
            line += 20f;
            Text(new Rect(left, line, textWidth, 14f), hasMission ? $"{kind} - {mission}" : kind, Cream, small, TextAnchor.MiddleLeft);
            line += 16f;
            Text(new Rect(left, line, textWidth, 16f), status, statusColor, medium, TextAnchor.MiddleLeft);
            line += 18f;
            if (activityHeight > 0f)
            {
                Text(new Rect(left, line, textWidth, activityHeight), activity, Muted, small);
                line += activityHeight;
            }
            GoldBar(new Rect(left, line + 4f, textWidth, 8f), ant.Health.Current / Mathf.Max(1f, ant.Health.Max), BarMissing);
        }

        /// <summary>The ant whose body is under <paramref name="point"/> in the view on screen, or null.</summary>
        Ant AntUnderMouse(Vector2 point)
        {
            bool surface = game.CameraRig.View == CameraView.Surface;
            Ant best = null;
            float bestDistance = float.MaxValue;
            foreach (Ant ant in game.Colony.Ants)
            {
                if (ant == null || ant.IsOnSurface != surface) continue;
                float distance = Vector2.Distance(ant.BodyCenter, point);
                float reach = Mathf.Max(0.35f, ant.BodyWidth * 0.55f);
                if (distance < reach && distance < bestDistance)
                {
                    best = ant;
                    bestDistance = distance;
                }
            }
            return best;
        }

        void DrawSelectionBox(float scale)
        {
            SoldierSelection selection = game.Selection;
            GatherOrders gather = game.Gather;
            if ((!selection.IsDragging && !gather.IsDragging) || game.CameraRig.View != CameraView.Surface) return;

            Rect drag = gather.IsDragging ? gather.DragRect : selection.DragRect;
            var rect = new Rect(drag.x / scale, (Screen.height - drag.yMax) / scale, drag.width / scale, drag.height / scale);

            Color previous = GUI.color;
            Color edge = AutumnPalette.Selection;
            GUI.color = new Color(edge.r, edge.g, edge.b, 0.15f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = edge;
            float line = PixelFont.Snap(2f, scale);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void DrawMessage(float screenWidth)
        {
            float visibility = game.MessageVisibility;
            if (visibility <= 0f || string.IsNullOrEmpty(game.Message)) return;

            Color color = Gold;
            color.a = visibility;
            Text(new Rect(screenWidth * 0.5f - 300f, 54f, 600f, 40f), game.Message, color, medium, TextAnchor.UpperCenter);
        }

        void DrawEndScreen(float screenWidth)
        {
            DrawDarkOverlay(screenWidth);
            uiRects.Add(new Rect(0f, 0f, screenWidth, screenHeight));

            bool won = game.State == GameState.Won;
            int totalWaves = game.Waves.TotalWaves;
            int survivedWaves = won ? totalWaves : Mathf.Max(0, game.Waves.CurrentWave - 1);

            string title = won ? "The colony survived!" : "The queen has fallen";
            string subtitle = won ? $"All {totalWaves} waves defeated." : $"You held out for {survivedWaves} of {totalWaves} waves.";

            float centerY = screenHeight * 0.38f;
            if (!TitleBanner(screenWidth * 0.5f, centerY + 4f, 560f, title, LeafOrange))
                Text(new Rect(0f, centerY - 50f, screenWidth, 40f), title, LeafOrange, huge, TextAnchor.UpperCenter);
            Text(new Rect(0f, centerY + 10f, screenWidth, 20f), subtitle, Cream, medium, TextAnchor.UpperCenter);

            bool planks = game.UiSkin != null && game.UiSkin.HasMenuButtons;
            float width = planks ? 250f : 220f;
            float height = planks ? 54f : 48f;
            float x = screenWidth * 0.5f - width * 0.5f;
            if (MenuButton(new Rect(x, centerY + 46f, width, height), "Play again  [R]"))
                RequestNewGame();
            if (MenuButton(new Rect(x, centerY + 56f + height, width, height), "Main menu"))
                game.OpenMainMenu();
        }

        /// <summary>The title menu, over the paused world.</summary>
        void DrawMainMenu(float screenWidth)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = previous;
            uiRects.Add(new Rect(0f, 0f, screenWidth, screenHeight));

            const string title = "Autumn Ant Colony";
            const string tagline = "Dig, gather and defend your queen through the fall.";
            float centerX = screenWidth * 0.5f;
            bool planks = game.UiSkin != null && game.UiSkin.HasMenuButtons;
            float buttonWidth = planks ? 280f : 260f;
            float buttonHeight = planks ? 56f : 46f;
            const float gap = 12f;
            float x = centerX - buttonWidth * 0.5f;
            float y;

            UiSkin skin = game.UiSkin;
            if (skin != null && skin.headerBanner != null)
            {
                const float bannerWidth = 600f;
                float bannerHeight = bannerWidth * skin.headerBanner.height / skin.headerBanner.width;
                float bannerTop = Mathf.Max(8f, screenHeight * 0.05f);
                TitleBanner(centerX, bannerTop + bannerHeight, bannerWidth, title, LeafOrange);
                Text(new Rect(0f, bannerTop + bannerHeight + 4f, screenWidth, 20f), tagline, Cream, medium, TextAnchor.UpperCenter);
                y = bannerTop + bannerHeight + 36f;
            }
            else
            {
                float top = screenHeight * 0.18f;
                Icon(new Vector2(centerX, top), CasteArt(AntRole.Worker).sprite, null, maxSize: 64f);
                Text(new Rect(0f, top + 40f, screenWidth, 50f), title, LeafOrange, huge, TextAnchor.UpperCenter);
                Text(new Rect(0f, top + 92f, screenWidth, 20f), tagline, Cream, medium, TextAnchor.UpperCenter);
                y = top + 140f;
            }

            float stackBottom = y + buttonHeight * 5f + gap * 4f;
            float ground = DrawMenuQueen(x, y, stackBottom);
            DrawMenuAnts(x + buttonWidth + MenuQueenGap * 2f, screenWidth - Margin * 2f, ground);

            float half = (buttonWidth - gap) * 0.5f;
            if (MenuButton(new Rect(x, y, half, buttonHeight), "New game")) RequestNewGame();
            if (MenuButton(new Rect(x + half + gap, y, half, buttonHeight), "Tutorial")) RequestNewGame(tutorial: true);
            y += buttonHeight + gap;
            if (SmallButton(new Rect(x, y, buttonWidth, buttonHeight), "Continue", game.HasSave)) game.LoadGame();
            y += buttonHeight + gap;
            if (MenuButton(new Rect(x, y, buttonWidth, buttonHeight), "How to play")) ToggleHelp();
            y += buttonHeight + gap;
            if (MenuButton(new Rect(x, y, buttonWidth, buttonHeight), "Settings")) ToggleSound();
            y += buttonHeight + gap;
            if (MenuButton(new Rect(x, y, buttonWidth, buttonHeight), "Quit")) game.QuitGame();
            y += buttonHeight + gap + 6f;

            Text(new Rect(0f, y, screenWidth, 14f), "In game:  F5 save   F9 load   Esc pause", Muted, small, TextAnchor.UpperCenter);
            if (game.Message.Length > 0 && game.MessageVisibility > 0f)
                Text(new Rect(0f, y + 22f, screenWidth, 14f), game.Message, Bad, small, TextAnchor.UpperCenter);
        }

        /// <summary>How wide the queen beside the title menu may be, in UI units (she shrinks on narrow screens).</summary>
        const float MenuQueenWidth = 252f;
        /// <summary>Space between the queen's image and the menu buttons, in UI units (her art has some empty space on that side too).</summary>
        const float MenuQueenGap = 12f;
        /// <summary>Two wing beats: half, up, half, rest, twice (as in <see cref="QueenAnimator"/>).</summary>
        static readonly int[] MenuQueenFlap = { 1, 2, 1, 0, 1, 2, 1, 0 };

        /// <summary>
        /// The queen beside the title menu, left of the buttons and facing them, standing on a small shadow. She plays
        /// her own breathing idle from her prefab and beats her wings every few seconds. Scaled by whole screen pixels so
        /// the pixel art stays crisp; skipped if there is no room for her or no queen art.
        /// Returns the ground line she stands on (where the menu's ants walk too).
        /// </summary>
        float DrawMenuQueen(float buttonsLeft, float stackTop, float stackBottom)
        {
            float fallbackGround = Mathf.Round(((stackTop + stackBottom) * 0.5f + 66f) * guiScale) / guiScale;
            if (Event.current.type != EventType.Repaint) return fallbackGround;
            Sprite sprite = MenuQueenSprite(Time.unscaledTime);
            if (sprite == null) return fallbackGround;

            Rect spriteRect = sprite.rect;
            float right = buttonsLeft - MenuQueenGap;
            float room = Mathf.Min(MenuQueenWidth, right - Margin * 2f);
            int screenPixels = Mathf.FloorToInt(room * guiScale / spriteRect.width);
            if (screenPixels < 2) return fallbackGround;
            float pixel = screenPixels / guiScale;

            var size = new Vector2(spriteRect.width, spriteRect.height) * pixel;
            float left = Mathf.Round((right - size.x) * guiScale) / guiScale;
            float top = Mathf.Round(((stackTop + stackBottom) * 0.5f - size.y * 0.5f) * guiScale) / guiScale;
            var destination = new Rect(left, top, size.x, size.y);

            // A stepped oval shadow under her feet, centred on her body (her frames leave empty columns on the right).
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            float centerX = destination.x + Mathf.Round(spriteRect.width * 0.44f) * pixel;
            float feet = destination.yMax - pixel;
            float wide = Mathf.Round(size.x * 0.84f / pixel) * pixel;
            float narrow = Mathf.Round(size.x * 0.62f / pixel) * pixel;
            GUI.DrawTexture(new Rect(centerX - narrow * 0.5f, feet - pixel, narrow, pixel), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - wide * 0.5f, feet, wide, pixel * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - narrow * 0.5f, feet + pixel * 2f, narrow, pixel), Texture2D.whiteTexture);

            Texture texture = sprite.texture;
            var uv = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(destination, texture, uv);
            GUI.color = previous;
            return destination.yMax;
        }

        /// <summary>One ant walking to and fro beside the title menu: its caste, speed (UI units per second at a 5-unit pixel), rest at each end, head start.</summary>
        static readonly (AntRole role, float speed, float rest, float phase)[] MenuAnts =
        {
            (AntRole.Worker, 38f, 1.1f, 0f),
            (AntRole.Builder, 30f, 1.8f, 6.3f),
            (AntRole.Soldier, 46f, 0.7f, 2.4f),
            (AntRole.Spitter, 34f, 1.4f, 9.1f),
        };

        /// <summary>
        /// The queen's young walking to and fro right of the title menu's buttons, on the queen's ground line. Each ant
        /// walks its own pace and rests at the ends; they sit a few pixels apart front to back, so passing looks natural.
        /// Skipped if the strip is too narrow.
        /// </summary>
        void DrawMenuAnts(float stripLeft, float stripRight, float ground)
        {
            if (Event.current.type != EventType.Repaint || game.Prefabs == null) return;

            int screenPixels = Mathf.Max(2, Mathf.RoundToInt(5f * guiScale));
            float pixel = screenPixels / guiScale;
            float time = Time.unscaledTime;
            Color previous = GUI.color;

            // Back to front: later ants stand a little lower, i.e. nearer.
            for (int i = 0; i < MenuAnts.Length; i++)
            {
                (AntRole role, float speed, float rest, float phase) = MenuAnts[i];
                Ant prefab = game.Prefabs.AntFor(role);
                Sprite[] frames = prefab != null ? prefab.SideWalk : null;
                bool facesLeft = prefab != null && prefab.SideViewFacesLeft;
                Color tint = Color.white;
                if (frames == null)
                {
                    frames = game.Art.AntSide.Walk;
                    facesLeft = game.Art.AntSideFacesLeft;
                    tint = CasteArt(role).tint;
                }
                Sprite still = frames.Length > 1 && frames[1] != null ? frames[1] : System.Array.Find(frames, s => s != null);
                if (still == null) continue;

                Rect stillRect = still.rect;
                var size = new Vector2(stillRect.width, stillRect.height) * pixel;
                float length = stripRight - stripLeft - size.x;
                if (length < size.x) break;

                // To the right, rest, back to the left, rest.
                float pace = speed * pixel / 5f;
                float walk = length / pace;
                float t = Mathf.Repeat(time + phase, 2f * (walk + rest));
                float along, travelled = 0f;
                bool moving = true, goingRight = true;
                if (t < walk) { along = t * pace; travelled = along; }
                else if (t < walk + rest) { along = length; moving = false; }
                else if (t < walk * 2f + rest) { travelled = (t - walk - rest) * pace; along = length - travelled; goingRight = false; }
                else { along = 0f; moving = false; goingRight = false; }

                Sprite sprite = still;
                if (moving)
                {
                    int frame = Mathf.FloorToInt(travelled / (pixel * 3f)) % frames.Length;
                    if (frames[frame] != null) sprite = frames[frame];
                }

                float feet = ground + i * pixel * 3f;
                float left = Mathf.Round((stripLeft + along) * guiScale) / guiScale;
                float top = Mathf.Round((feet - size.y) * guiScale) / guiScale;
                var destination = new Rect(left, top, size.x, size.y);

                // A small stepped shadow under its legs.
                GUI.color = new Color(0f, 0f, 0f, 0.3f);
                float shadow = Mathf.Round(stillRect.width * 0.75f) * pixel;
                GUI.DrawTexture(new Rect(destination.center.x - shadow * 0.5f, destination.yMax - pixel, shadow, pixel * 2f), Texture2D.whiteTexture);

                Rect spriteRect = sprite.rect;
                Texture texture = sprite.texture;
                var uv = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                    spriteRect.width / texture.width, spriteRect.height / texture.height);
                // Turn the ant to face the way it walks (or the way it last walked, while resting).
                if (goingRight == facesLeft) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                GUI.color = tint;
                GUI.DrawTextureWithTexCoords(destination, texture, uv);
            }
            GUI.color = previous;
        }

        /// <summary>The queen's idle frame at <paramref name="time"/>, with a wing flap every few seconds; her plain sprite without frames.</summary>
        Sprite MenuQueenSprite(float time)
        {
            GameObject prefab = game.Prefabs != null ? game.Prefabs.queen : null;
            if (prefab == null) return null;

            if (prefab.TryGetComponent(out QueenAnimator art) && art.idle != null && art.idle.Length > 0)
            {
                int wing = 0;
                float flapEvery = Mathf.Max(0.5f, (art.flapEveryMin + Mathf.Max(art.flapEveryMin, art.flapEveryMax)) * 0.5f);
                int flapStep = Mathf.FloorToInt(Mathf.Repeat(time, flapEvery) * art.flapFps);
                if (flapStep < MenuQueenFlap.Length) wing = MenuQueenFlap[flapStep];

                int frame = Mathf.FloorToInt(time * art.idleFps) % art.idle.Length;
                Sprite sprite = art.idle[frame]?.Get(wing);
                if (sprite != null) return sprite;
            }

            SpriteRenderer body = prefab.GetComponentInChildren<SpriteRenderer>();
            return body != null ? body.sprite : null;
        }

        void DrawConfirmation(float screenWidth)
        {
            DrawDarkOverlay(screenWidth);
            uiRects.Add(new Rect(0f, 0f, screenWidth, screenHeight));
            var panel = new Rect((screenWidth - 500f) * 0.5f, Mathf.Max(12f, (screenHeight - 350f) * 0.5f), 500f, 350f);
            Panel(panel);
            bool leaving = confirmation == Confirmation.MainMenu;
            Text(new Rect(panel.x + 20f, panel.y + 20f, 460f, 32f),
                leaving ? "Return to main menu?" : "Replace saved colony?", Gold, large);
            string body = leaving
                ? "Progress since your last save will be lost. Save your colony before leaving?"
                : "Starting a new game or tutorial replaces your saved colony. You will no longer be able to continue that game.";
            Text(new Rect(panel.x + 20f, panel.y + 65f, 460f, 80f), body, Cream, medium);
            float y = panel.y + 150f;
            if (MenuButton(new Rect(panel.x + 20f, y, 460f, 44f), leaving ? "Save and return" : "Replace and start"))
            {
                if (leaving)
                {
                    if (game.SaveGame()) { CancelConfirmation(); game.OpenMainMenu(); return; }
                    confirmationError = "Save failed. Your game is still open. Try again or cancel.";
                }
                else
                {
                    bool tutorial = confirmation == Confirmation.Tutorial;
                    CancelConfirmation();
                    game.StartNewGame(tutorial);
                    return;
                }
            }
            y += 54f;
            if (leaving && MenuButton(new Rect(panel.x + 20f, y, 460f, 44f), "Leave without saving"))
            {
                CancelConfirmation(); game.OpenMainMenu(); return;
            }
            if (leaving) y += 54f;
            if (MenuButton(new Rect(panel.x + 20f, y, 460f, 44f), "Cancel  [Esc]")) CancelConfirmation();
            if (!string.IsNullOrEmpty(confirmationError))
                Text(new Rect(panel.x + 20f, panel.yMax - 34f, 460f, 30f), confirmationError, Bad, small);
        }

        void DrawPauseScreen(float screenWidth)
        {
            DrawDarkOverlay(screenWidth);
            uiRects.Add(new Rect(0f, 0f, screenWidth, screenHeight));

            float centerY = screenHeight * 0.38f;
            if (!TitleBanner(screenWidth * 0.5f, centerY + 14f, 420f, "Paused", LeafOrange))
                Text(new Rect(0f, centerY - 40f, screenWidth, 40f), "Paused", LeafOrange, huge, TextAnchor.UpperCenter);

            bool planks = game.UiSkin != null && game.UiSkin.HasMenuButtons;
            float width = planks ? 260f : 240f;
            float height = planks ? 52f : 44f;
            float x = screenWidth * 0.5f - width * 0.5f;
            float y = centerY + 20f;
            if (MenuButton(new Rect(x, y, width, height), "Resume  [Esc]")) game.TogglePause();
            y += height + 10f;
            if (MenuButton(new Rect(x, y, width, height), "Save game  [F5]")) game.SaveGame();
            y += height + 10f;
            if (MenuButton(new Rect(x, y, width, height), "Settings")) ToggleSound();
            y += height + 10f;
            if (SmallButton(new Rect(x, y, width, height), "Load game  [F9]", game.HasSave)) game.LoadGame();
            y += height + 10f;
            if (MenuButton(new Rect(x, y, width, height), "Main menu")) RequestMainMenu();
            Text(new Rect(0f, y + height + 8f, screenWidth, 14f), "Unsaved progress is lost when you leave.", Muted, small, TextAnchor.UpperCenter);
            if (game.Message.Length > 0 && game.MessageVisibility > 0f)
                Text(new Rect(0f, y + height + 28f, screenWidth, 14f), game.Message, Cream, small, TextAnchor.UpperCenter);
        }

        /// <summary>
        /// The header banner with a title on its plank, centred on <paramref name="centerX"/> with its bottom at
        /// <paramref name="bottom"/>. The title shrinks until it fits the plank. False if the skin has no banner.
        /// </summary>
        bool TitleBanner(float centerX, float bottom, float width, string title, Color color)
        {
            UiSkin skin = game.UiSkin;
            Texture2D banner = skin != null ? skin.headerBanner : null;
            if (banner == null) return false;

            float pixel = width / banner.width;
            float height = banner.height * pixel;
            Rect rect = Snap(new Rect(centerX - width * 0.5f, bottom - height, width, height));
            GUI.DrawTexture(rect, banner, ScaleMode.StretchToFill);
            uiRects.Add(rect);

            Vector4 plank = skin.headerBannerPlank;
            Rect textArea = Rect.MinMaxRect(rect.x + plank.x * pixel, rect.y + plank.z * pixel, rect.xMax - plank.y * pixel, rect.yMax - plank.w * pixel);
            float size = huge;
            foreach (float candidate in new[] { huge, PixelFont.Snap(4f, guiScale), large, medium, small })
            {
                size = candidate;
                if (font.Measure(title, candidate) <= textArea.width) break;
            }
            Text(textArea, title, color, size, TextAnchor.MiddleCenter);
            return true;
        }

        bool MenuButton(Rect rect, string label)
        {
            rect = Snap(rect);
            if (game.UiSkin != null && (game.UiSkin.HasSlots || game.UiSkin.HasMenuButtons)) return SmallButton(rect, label, true);
            bool clicked = HudButton(rect, buttonStyle);
            if (MouseOver(rect)) NoteHover(rect);
            bool pressed = MouseOver(rect) && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;
            var face = new Rect(rect.x, rect.y + (pressed ? FramePixel : -FramePixel), rect.width, rect.height);
            Text(face, label, Cream, medium, TextAnchor.MiddleCenter);
            return clicked;
        }

        // ---------- Drawing helpers ----------

        float Text(Rect rect, string text, Color color, float size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            return font.Draw(rect, text, FitOneLine(rect, text, size), color, alignment);
        }

        /// <summary>
        /// A rect less than two lines tall holds a one-line label: if the text is too wide, it is drawn a little smaller
        /// instead of wrapping onto whatever is below it.
        /// </summary>
        float FitOneLine(Rect rect, string text, float size)
        {
            if (rect.height >= PixelFont.LineHeight * size * 2f || text.IndexOf('\n') >= 0) return size;
            float step = 1f / guiScale;
            float smallest = Mathf.Max(step, size * 0.5f);
            while (size - step >= smallest - 0.001f && font.Measure(text, size) > rect.width) size -= step;
            return size;
        }

        void Panel(Rect rect, bool card = false)
        {
            rect = Snap(rect);
            UiSkin skin = game.UiSkin;
            if (!card && skin != null && skin.panel != null)
            {
                // The image reaches a little past the content, so its rim sits around it rather than under it.
                // It stays on screen: next to an edge it reaches past the content on the far side only.
                float bleed = skin.panelBleed;
                Rect outer = Rect.MinMaxRect(
                    Mathf.Max(0f, rect.x - bleed), Mathf.Max(0f, rect.y - bleed),
                    Mathf.Max(rect.xMax + bleed, Mathf.Max(0f, rect.x - bleed) + rect.width + bleed * 2f),
                    Mathf.Max(rect.yMax + bleed, Mathf.Max(0f, rect.y - bleed) + rect.height + bleed * 2f));
                outer = Snap(outer);
                NineSlice(outer, skin.panel, skin.panelBorder, skin.frameScale);
                uiRects.Add(outer);
                if (drawingCover && Event.current.type == EventType.Repaint) nextCoverRects.Add(outer);
                return;
            }

            DropShadow(rect);
            GUI.Box(rect, GUIContent.none, card ? cardStyle : panelStyle);
            uiRects.Add(rect);
            if (drawingCover && Event.current.type == EventType.Repaint) nextCoverRects.Add(rect);
        }

        /// <summary>
        /// Draws an image stretched over a rect in nine parts: the corners (<paramref name="border"/>, in image pixels:
        /// x left, y right, z top, w bottom) keep their shape at <paramref name="scale"/> UI units per pixel, the edges
        /// stretch one way and the middle both. The scale shrinks if the rect is too small for the corners.
        /// </summary>
        void NineSlice(Rect rect, Texture2D texture, Vector4 border, float scale)
        {
            float width = texture.width;
            float height = texture.height;
            scale = Mathf.Min(scale, rect.width / Mathf.Max(1f, border.x + border.y), rect.height / Mathf.Max(1f, border.z + border.w));

            // Column and row edges, in image pixels (rows from the top) and in UI units.
            float[] sourceX = { 0f, border.x, width - border.y, width };
            float[] sourceY = { 0f, border.z, height - border.w, height };
            float[] targetX = { rect.x, rect.x + border.x * scale, rect.xMax - border.y * scale, rect.xMax };
            float[] targetY = { rect.y, rect.y + border.z * scale, rect.yMax - border.w * scale, rect.yMax };
            for (int i = 0; i < 4; i++)
            {
                targetX[i] = Mathf.Round(targetX[i] * guiScale) / guiScale;
                targetY[i] = Mathf.Round(targetY[i] * guiScale) / guiScale;
            }

            for (int column = 0; column < 3; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    var target = Rect.MinMaxRect(targetX[column], targetY[row], targetX[column + 1], targetY[row + 1]);
                    if (target.width <= 0f || target.height <= 0f) continue;

                    // Texture coordinates go bottom-up.
                    var uv = new Rect(sourceX[column] / width, 1f - sourceY[row + 1] / height,
                        (sourceX[column + 1] - sourceX[column]) / width, (sourceY[row + 1] - sourceY[row]) / height);
                    GUI.DrawTextureWithTexCoords(target, texture, uv);
                }
            }
        }

        /// <summary>Text without the dark drop shadow, for dark ink on light paper.</summary>
        float InkText(Rect rect, string text, Color color, float size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            return font.Draw(rect, text, size, color, alignment, shadow: false);
        }

        /// <summary>A hard pixel shadow down and to the right, with the same cut corners as the frames.</summary>
        void DropShadow(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            float offset = FramePixel;
            GUI.DrawTexture(new Rect(rect.x + offset * 2f, rect.y + offset, rect.width - offset * 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + offset, rect.y + offset * 2f, rect.width, rect.height - offset * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Rounds a rect to whole screen pixels.</summary>
        Rect Snap(Rect rect)
        {
            float x = Mathf.Round(rect.x * guiScale) / guiScale;
            float y = Mathf.Round(rect.y * guiScale) / guiScale;
            float xMax = Mathf.Round(rect.xMax * guiScale) / guiScale;
            float yMax = Mathf.Round(rect.yMax * guiScale) / guiScale;
            return Rect.MinMaxRect(x, y, xMax, yMax);
        }

        /// <summary>A pixel button with a centred label. Greyed out (and never clicked) when not enabled.</summary>
        bool SmallButton(Rect rect, string label, bool enabled)
        {
            rect = Snap(rect);
            // Only wide (menu) buttons drop leaves; small ones like + and - don't.
            bool wide = rect.width > rect.height * 1.4f;
            if (game.UiSkin != null && game.UiSkin.HasMenuButtons && wide)
            {
                bool plankClicked = PlankButton(rect, enabled, out bool hovered, out bool held);
                // The plank sits a little below the middle of the image.
                var labelRect = new Rect(rect.x, rect.y + rect.height * 0.03f + (held ? 1f : 0f), rect.width, rect.height);
                Text(labelRect, label, !enabled ? Muted : hovered ? Color.white : Cream, medium, TextAnchor.MiddleCenter);
                return plankClicked;
            }
            if (game.UiSkin != null && game.UiSkin.HasSlots)
            {
                bool slotClicked = SlotButton(rect, enabled, false, lockedLook: false, out bool hovered, out bool held);
                if (hovered && wide) NoteHover(rect);
                var labelRect = new Rect(rect.x, rect.y + (held ? 1f : -1f), rect.width, rect.height);
                Text(labelRect, label, !enabled ? Muted : hovered ? Color.white : Cream, medium, TextAnchor.MiddleCenter);
                return slotClicked;
            }
            bool clicked = HudButton(rect, enabled ? buttonStyle : disabledButtonStyle);
            if (enabled && wide && MouseOver(rect)) NoteHover(rect);
            bool pressed = enabled && MouseOver(rect) && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;
            var face = new Rect(rect.x, rect.y + (pressed ? FramePixel : -FramePixel) * 0.5f, rect.width, rect.height);
            Text(face, label, enabled ? Cream : Muted, medium, TextAnchor.MiddleCenter);
            return clicked && enabled;
        }

        /// <summary>Draws a sprite centred on a point, scaled by whole pixels so it stays crisp.</summary>
        void Icon(Vector2 center, Sprite sprite, Color? tint = null, float maxSize = 18f)
        {
            if (sprite == null) return;

            Rect spriteRect = sprite.rect;
            // Whole screen pixels per sprite pixel, counted as an integer (float steps drifted to ~0 at some scales).
            float longest = Mathf.Max(spriteRect.width, spriteRect.height);
            int screenPixels = Mathf.Max(1, Mathf.RoundToInt(iconPixel * guiScale));
            while (screenPixels > 1 && longest * screenPixels / guiScale > maxSize) screenPixels--;
            float pixel = screenPixels / guiScale;
            if (longest * pixel > maxSize) pixel = maxSize / longest; // big painted sprites: shrink to fit

            var size = new Vector2(spriteRect.width, spriteRect.height) * pixel;
            var destination = Snap(new Rect(center - size * 0.5f, size));
            Texture texture = sprite.texture;
            var uv = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);

            Color previous = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(destination, texture, uv);
            GUI.color = previous;
        }

        /// <summary>A pixel-style bar: dark outline, empty track, fill with a lighter top row.</summary>
        void Bar(Rect rect, float fraction, Color fillColor)
        {
            StackedBar(rect, new[] { (Mathf.Clamp01(fraction), fillColor) });
        }

        /// <summary>A bar made of several coloured parts side by side (e.g. food and leaves in storage).</summary>
        void StackedBar(Rect rect, (float fraction, Color color)[] parts)
        {
            rect = Snap(rect);
            float pixel = PixelFont.Snap(2f, guiScale);
            Color previous = GUI.color;

            GUI.color = AutumnPalette.Outline;
            GUI.DrawTexture(new Rect(rect.x - pixel, rect.y - pixel, rect.width + pixel * 2f, rect.height + pixel * 2f), Texture2D.whiteTexture);
            GUI.color = AutumnPalette.Tunnel;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float x = rect.x;
            foreach ((float fraction, Color color) in parts)
            {
                // Snap to whole bar pixels so it fills in chunky steps.
                float width = Mathf.Floor(Mathf.Clamp01(fraction) * rect.width / pixel) * pixel;
                width = Mathf.Min(width, rect.xMax - x);
                if (width <= 0f) continue;

                GUI.color = color;
                GUI.DrawTexture(new Rect(x, rect.y, width, rect.height), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(color, Color.white, 0.4f);
                GUI.DrawTexture(new Rect(x, rect.y, width, pixel), Texture2D.whiteTexture);
                x += width;
            }
            GUI.color = previous;
        }

        /// <summary>Black cover while the camera cuts between the Surface and Nest scenes.</summary>
        void DrawSceneFade(float screenWidth)
        {
            float fade = game.CameraRig.Fade;
            if (fade <= 0f) return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void DrawDarkOverlay(float screenWidth)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void CreateStylesOnce()
        {
            if (panelStyle != null) return;

            font = new PixelFont();

            panelStyle = new GUIStyle { border = FrameBorder() };
            panelStyle.normal.background = PixelFrame(new Color(0.16f, 0.09f, 0.06f, 0.94f), AutumnPalette.Brown, AutumnPalette.DarkBrown, pressed: false);

            cardStyle = new GUIStyle { border = FrameBorder() };
            cardStyle.normal.background = PixelFrame(new Color(0.1f, 0.06f, 0.04f, 0.88f), AutumnPalette.DarkBrown, AutumnPalette.Outline, pressed: false);

            buttonStyle = new GUIStyle { border = FrameBorder() };
            buttonStyle.normal.background = PixelFrame(AutumnPalette.Brown, AutumnPalette.Orange, AutumnPalette.DarkBrown, pressed: false);
            buttonStyle.hover.background = PixelFrame(AutumnPalette.BurntOrange, AutumnPalette.Gold, AutumnPalette.Brown, pressed: false);
            buttonStyle.active.background = PixelFrame(AutumnPalette.Brown, AutumnPalette.Orange, AutumnPalette.DarkBrown, pressed: true);

            // A tool button stays lit while that tool is in hand.
            activeButtonStyle = new GUIStyle(buttonStyle);
            activeButtonStyle.normal.background = buttonStyle.hover.background;

            Color grey = AutumnPalette.Hex("5e4f45");
            Color greyLight = AutumnPalette.Hex("7d6c5f");
            Color greyDark = AutumnPalette.Hex("3d322b");
            disabledButtonStyle = new GUIStyle { border = FrameBorder() };
            disabledButtonStyle.normal.background = PixelFrame(grey, greyLight, greyDark, pressed: false);
            disabledButtonStyle.hover.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.active.background = PixelFrame(grey, greyLight, greyDark, pressed: true);
        }

        static RectOffset FrameBorder()
        {
            int edge = FrameEdgeCells * FramePixel;
            return new RectOffset(edge, edge, edge, edge);
        }

        /// <summary>
        /// A bevelled pixel-art frame: outline with cut corners, a light top-left edge and a thick shadow
        /// at the bottom so it looks raised. Pressed frames swap to an inset look.
        /// </summary>
        Texture2D PixelFrame(Color fill, Color light, Color shadow, bool pressed)
        {
            int size = FrameCells * FramePixel;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            int last = FrameCells - 1;
            for (int cellX = 0; cellX < FrameCells; cellX++)
            {
                for (int cellY = 0; cellY < FrameCells; cellY++) // cellY = 0 is the top row
                {
                    bool edgeX = cellX == 0 || cellX == last;
                    bool edgeY = cellY == 0 || cellY == last;

                    Color color = fill;
                    if (edgeX && edgeY) color = Color.clear;
                    else if (edgeX || edgeY) color = AutumnPalette.Outline;
                    else if (pressed && (cellY <= 2 || cellX == 1)) color = shadow;
                    else if (!pressed && (cellY == 1 || cellX == 1)) color = light;
                    else if (!pressed && (cellY >= last - 2 || cellX == last - 1)) color = shadow;

                    for (int x = 0; x < FramePixel; x++)
                    {
                        for (int y = 0; y < FramePixel; y++)
                            texture.SetPixel(cellX * FramePixel + x, (last - cellY) * FramePixel + y, color);
                    }
                }
            }

            texture.Apply();
            createdTextures.Add(texture);
            return texture;
        }
    }
}
