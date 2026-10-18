using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AntColony
{
    /// <summary>
    /// Keyboard and mouse shortcuts. Uses the Input System package, which is the project's active input handler.
    /// The on-screen buttons in <see cref="GameHud"/> call the same actions.
    ///
    /// Everywhere: 1/2/3 raise a worker/soldier/builder, Z an acid spitter, Tab switches view, N calls the next wave,
    /// R colony report, M missions, H help, F5 save, F9 load, Esc stops a tool, closes a panel or pauses.
    /// Surface: 4 walls, 5 towers, 6 gather (select food piles), E selects all soldiers, Q halves the selected soldiers,
    /// mouse selects and commands soldiers.
    /// Nest: 4 dig, 5 storage, 6 cradle, 7 fungus garden, X remove.
    /// </summary>
    public class ColonyControls : MonoBehaviour
    {
        ColonyGame game;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        void Update()
        {
            if (game == null || game.Colony == null) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (game.Hud.HasConfirmation)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) game.Hud.CancelConfirmation();
                return;
            }

            if (game.Intro != null && game.Intro.IsPlaying)
            {
                // The intro takes no other input; any of these skips it.
                bool skip = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (skip) game.Intro.Skip();
                return;
            }

#if UNITY_EDITOR
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                game.SetUpBossTest(); // editor-only test shortcut
                return;
            }
#endif
            if (game.InMainMenu)
            {
                // The menu's own buttons do the rest; Esc only closes the help page.
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) game.Hud.ClosePanels();
                return;
            }

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame) game.SaveGame();
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                game.LoadGame();
                return;
            }

            if (game.State != GameState.Playing)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) game.Hud.ClosePanels();
                else if (keyboard != null && keyboard.rKey.wasPressedThisFrame) game.Hud.RequestNewGame();
                return;
            }

            if (game.Tutorial.ShowingPopup && !game.IsPaused)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { game.TogglePause(); return; }
                // The pop-up's buttons do the rest; Enter / Space mean "Got it".
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) game.Tutorial.Continue();
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Esc first puts down a tool, then closes an open panel (report, missions, help); only then does it pause.
                if (game.BuildMode.IsActive) game.BuildMode.Stop();
                else if (game.Gather.IsActive) game.Gather.Stop();
                else if (game.NestTools.IsActive) game.NestTools.Stop();
                else if (!game.Hud.ClosePanels()) game.TogglePause();
            }
            if (game.IsPaused) return;

            if (keyboard != null) HandleKeyboard(keyboard);
            if (mouse != null) HandleMouse(mouse, keyboard);
        }

        void HandleKeyboard(Keyboard keyboard)
        {
            Colony colony = game.Colony;
            if (Pressed(keyboard.digit1Key, keyboard.numpad1Key)) colony.QueueRaise(AntRole.Worker);
            if (Pressed(keyboard.digit2Key, keyboard.numpad2Key)) colony.QueueRaise(AntRole.Soldier);
            if (Pressed(keyboard.digit3Key, keyboard.numpad3Key)) colony.QueueRaise(AntRole.Builder);
            if (Pressed(keyboard.zKey, null)) colony.QueueRaise(AntRole.Spitter);
            if (Pressed(keyboard.tabKey, null)) game.CameraRig.ToggleView();
            if (Pressed(keyboard.nKey, null)) game.Waves.StartNextWave();
            if (Pressed(keyboard.rKey, null)) game.Hud.ToggleReport();
            if (Pressed(keyboard.mKey, null)) game.Hud.ToggleMissions();
            if (keyboard.hKey.wasPressedThisFrame) game.Hud.ToggleHelp();

            if (game.CameraRig.View == CameraView.Surface)
            {
                if (Pressed(keyboard.digit4Key, keyboard.numpad4Key)) game.BuildMode.Toggle(StructureKind.Wall);
                if (Pressed(keyboard.digit5Key, keyboard.numpad5Key)) game.BuildMode.Toggle(StructureKind.Tower);
                if (Pressed(keyboard.digit6Key, keyboard.numpad6Key)) game.Gather.Toggle();
                if (keyboard.eKey.wasPressedThisFrame) game.Selection.SelectAll();
                if (keyboard.qKey.wasPressedThisFrame) game.Selection.Halve();
            }
            else
            {
                NestTools tools = game.NestTools;
                if (Pressed(keyboard.digit4Key, keyboard.numpad4Key)) tools.Toggle(NestTool.Dig);
                if (Pressed(keyboard.digit5Key, keyboard.numpad5Key)) tools.Toggle(NestTool.Storage);
                if (Pressed(keyboard.digit6Key, keyboard.numpad6Key)) tools.Toggle(NestTool.Cradle);
                if (Pressed(keyboard.digit7Key, keyboard.numpad7Key)) tools.Toggle(NestTool.Garden);
                if (Pressed(keyboard.xKey, null)) tools.Toggle(NestTool.Remove);
            }

            float horizontal = Axis(keyboard.dKey, keyboard.rightArrowKey, keyboard.aKey, keyboard.leftArrowKey);
            float vertical = Axis(keyboard.wKey, keyboard.upArrowKey, keyboard.sKey, keyboard.downArrowKey);
            game.CameraRig.Pan(new Vector2(horizontal, vertical));
        }

        /// <summary>True when a shortcut (or its alternative key) was pressed this frame.</summary>
        static bool Pressed(KeyControl key, KeyControl alternativeKey)
        {
            return key.wasPressedThisFrame || (alternativeKey != null && alternativeKey.wasPressedThisFrame);
        }

        void HandleMouse(Mouse mouse, Keyboard keyboard)
        {
            bool onSurface = game.CameraRig.View == CameraView.Surface;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !game.Hud.IsPointerOverUi(mouse.position.ReadValue()))
                game.CameraRig.Zoom(Mathf.Sign(scroll));

            if (!onSurface)
            {
                game.Selection.CancelDrag();
                if (game.NestTools.IsActive) game.NestTools.HandleMouse(mouse);
                else if (mouse.leftButton.wasPressedThisFrame && !game.Hud.IsPointerOverUi(mouse.position.ReadValue()))
                {
                    // Click a room for its details; click bare soil or tunnel to close them.
                    Vector2 world = game.CameraRig.ScreenToWorld(mouse.position.ReadValue());
                    game.Hud.SelectRoom(game.Nest.RoomAt(game.Nest.WorldToTile(world)));
                }
                return;
            }

            if (game.BuildMode.IsActive)
            {
                game.Selection.CancelDrag();
                game.BuildMode.HandleMouse(mouse);
                return;
            }

            if (game.Gather.IsActive)
            {
                game.Selection.CancelDrag();
                game.Gather.HandleMouse(mouse, keyboard != null && keyboard.shiftKey.isPressed);
                return;
            }

            bool shiftHeld = keyboard != null && keyboard.shiftKey.isPressed;
            game.Selection.HandleMouse(mouse, shiftHeld);
        }

        /// <summary>+1 while a positive key is held, -1 while a negative key is held, 0 for both or neither.</summary>
        static float Axis(KeyControl positive, KeyControl positiveAlt, KeyControl negative, KeyControl negativeAlt)
        {
            float value = 0f;
            if (positive.isPressed || positiveAlt.isPressed) value += 1f;
            if (negative.isPressed || negativeAlt.isPressed) value -= 1f;
            return value;
        }
    }
}
