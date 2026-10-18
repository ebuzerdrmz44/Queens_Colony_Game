using System;
using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>Things the player did that a tutorial step may be waiting for (reported by the code that does them).</summary>
    public enum TutorialSignal
    {
        AntQueued,
        SoldierQueued,
        DigMarked,
        RoomPlaced,
        WallPlaced,
        TowerPlaced,
        FoodMarked,
        FoodDelivered,
        WorkerHatched,
        TileDug,
        RoomBuilt,
        StructureBuilt,
    }

    /// <summary>
    /// A guided first game. Each step opens a pop-up that pauses the game and explains one thing; after "Got it"
    /// the game runs and a small goal bar (top middle) says what to do, with the matching button blinking, until the
    /// player has done it. The player chooses Next to open the following explanation. Waves wait until finished or skipped.
    /// It runs only from "Tutorial" in the main menu; "Skip tutorial" ends it at any step.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        const string DoneKey = "tutorial.done";
        // Prevent the click/key that opened an explanation from dismissing it as well.
        const float ReadDelay = 0.5f;

        public class Step
        {
            public string Title;
            public string Body;
            /// <summary>What to do now, shown in the goal bar. Empty for steps that only explain.</summary>
            public string Goal = "";
            /// <summary>The key of the action button to make blink (e.g. "6"), or empty.</summary>
            public string HighlightKey = "";
            public Func<bool> IsDone;
            /// <summary>Runs when the player starts on the goal (after "Got it").</summary>
            public Action OnGoalStart;
            public Action OnEnter;
            public bool CanSkip = true;
            public Action OnSkip;
            /// <summary>Show the little cursor that clicks, holds and drags over <see cref="DemoFood"/>.</summary>
            public bool ShowsGatherDemo;
        }

        /// <summary>The food pile near the ant hill the gather demo points at.</summary>
        public FoodSource DemoFood { get; private set; }

        ColonyGame game;
        List<Step> steps;
        int index = -1;
        float popupOpenedAt;
        bool goalStarted;

        public bool GoalCompleted { get; private set; }
        public bool CanSkipStep => IsActive && Current != null && Current.CanSkip;
        public string SkipExplanation => CanSkipStep ? "" : "This step prepares later tasks. Finish it, or skip the whole tutorial.";
        public string ProgressHint
        {
            get
            {
                if (GoalCompleted) return "You can keep looking around. Nothing advances until you choose Next.";
                switch (index)
                {
                    case 1: return game.Gather.MarkedCount == 0 ? "Press 6, then select the nearby food. Marked piles have a gold ring." : "Follow a worker: it collects food, enters the nest and stores its load.";
                    case 2: return "Read the job descriptions in Missions. Close the panel when you are ready.";
                    case 3: return "Eggs need a free cradle, food and a worker to feed them. M opens job assignments.";
                    case 4: return "You are already underground. Find the queen, tunnels and rooms, then choose Next.";
                    case 5: return "Dig next to an existing tunnel. Builders need a connected path to reach the soil.";
                    case 6: return "Choose empty, dug-out space. A Storage room needs two adjacent tiles.";
                    case 7: return "Press Tab once. The nest keeps working while you watch the surface.";
                    case 8: return "Walls need stone and a builder. Keep a gap so workers can get through.";
                    case 9: return "E selects soldiers. Right-click the ground to move them; watch them respond.";
                    default: return "Use Read again for help, or Skip tutorial to start the waves.";
                }
            }
        }
        public bool CanContinue => Time.unscaledTime - popupOpenedAt >= ReadDelay;
        readonly HashSet<TutorialSignal> signals = new HashSet<TutorialSignal>();

        public bool IsActive { get; private set; }
        /// <summary>A pop-up is open: the game is paused and only the pop-up takes input.</summary>
        public bool ShowingPopup { get; private set; }
        /// <summary>Waves don't count down while the tutorial runs.</summary>
        public bool HoldsWaves => IsActive;
        public Step Current => IsActive && index >= 0 && index < steps.Count ? steps[index] : null;
        public int StepNumber => index + 1;
        public int StepCount => steps?.Count ?? 0;
        /// <summary>The player hasn't finished or skipped the tutorial before.</summary>
        public static bool IsFirstTime => PlayerPrefs.GetInt(DoneKey, 0) == 0;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            steps = BuildSteps();
        }

        public void Begin()
        {
            IsActive = true;
            index = -1;
            NextStep();
        }

        /// <summary>Ends the tutorial without marking it done (e.g. a game was loaded).</summary>
        public void Stop()
        {
            if (ShowingPopup) game.SetTutorialPause(false);
            IsActive = false;
            ShowingPopup = false;
            GoalCompleted = false;
            goalStarted = false;
        }

        public void Skip()
        {
            Stop();
            MarkDone();
            game.ShowMessage("Tutorial skipped. H shows the controls any time.");
        }

        /// <summary>Closes the pop-up ("Got it"): the game runs and the player works on the goal.</summary>
        public void Continue()
        {
            if (!ShowingPopup || !CanContinue) return;
            ShowingPopup = false;
            game.SetTutorialPause(false);

            if (Current != null && Current.IsDone == null) NextStep(); // an explanation only
            else if (!goalStarted)
            {
                goalStarted = true;
                Current?.OnGoalStart?.Invoke();
            }
        }

        /// <summary>Finds an unmarked food pile close to the ant hill, or puts one there, and turns the camera to it.</summary>
        void PrepareGatherDemo()
        {
            const float nearDistance = 5.5f;
            Vector2 hill = game.Surface.EntrancePoint;
            DemoFood = null;
            float best = nearDistance;
            foreach (FoodSource food in game.Food.Sources)
            {
                float distance = Vector2.Distance(food.Position, hill);
                if (distance < best && food.HasUnclaimedUnits)
                {
                    best = distance;
                    DemoFood = food;
                }
            }
            if (DemoFood == null) DemoFood = game.Food.SpawnNear(hill, 3f, 4.5f);

            if (game.CameraRig.View != CameraView.Surface) game.CameraRig.ToggleView();
            if (DemoFood != null) game.CameraRig.FocusSurface(Vector2.Lerp(hill, DemoFood.Position, 0.5f));
        }

        void PrepareBroodView()
        {
            Vector2 queen = game.Nest.QueenRoom.StandPoint;
            Vector2 focus = queen;
            foreach (Room room in game.Nest.Rooms)
            {
                if (room.Type != RoomType.Cradle || !room.IsBuilt || room.IsRemoved) continue;
                focus = Vector2.Lerp(queen, room.StandPoint, 0.5f);
                if (room.Brood == null && !room.AwaitingEgg) break;
            }
            game.CameraRig.FocusNest(focus);
            if (game.CameraRig.View != CameraView.Nest) game.CameraRig.ToggleView();
        }

        public void Notify(TutorialSignal signal)
        {
            if (IsActive && goalStarted) signals.Add(signal);
        }

        bool Saw(TutorialSignal signal) => signals.Contains(signal);

        void Update()
        {
            if (!IsActive || ShowingPopup || GoalCompleted || game == null || game.InMainMenu || game.IsPaused || game.State != GameState.Playing) return;

            Step step = Current;
            if (step?.IsDone != null && step.IsDone())
            {
                if (game.Audio != null) game.Audio.Play(Sound.UiConfirm);
                GoalCompleted = true;
            }
        }

        void NextStep()
        {
            index++;
            GoalCompleted = false;
            goalStarted = false;
            signals.Clear();
            if (index >= steps.Count)
            {
                IsActive = false;
                game.Waves.EnsurePreparationTime(45f);
                MarkDone();
                return;
            }

            Current?.OnEnter?.Invoke();
            ShowingPopup = true;
            popupOpenedAt = Time.unscaledTime;
            game.SetTutorialPause(true);
        }

        public void Advance()
        {
            if (IsActive && !ShowingPopup && GoalCompleted && !game.IsPaused) NextStep();
        }

        public void SkipStep()
        {
            if (!CanSkipStep || game.IsPaused || (ShowingPopup && !CanContinue)) return;
            Current.OnSkip?.Invoke();
            // Explanation skips must release the old pause before opening the next explanation.
            ShowingPopup = false;
            game.SetTutorialPause(false);
            NextStep();
        }

        public void Review()
        {
            if (!IsActive) return;
            ShowingPopup = true;
            popupOpenedAt = Time.unscaledTime;
            game.SetTutorialPause(true);
        }

        public void Write(SaveData data)
        {
            data.tutorialActive = IsActive;
            data.tutorialStep = IsActive ? index : -1;
            data.tutorialGoalStarted = goalStarted;
            data.tutorialSignals = 0;
            foreach (TutorialSignal signal in signals) data.tutorialSignals |= 1 << (int)signal;
            data.tutorialGoalCompleted = GoalCompleted || (IsActive && goalStarted && Current?.IsDone != null && Current.IsDone());
        }

        public void Read(SaveData data)
        {
            if (!data.tutorialActive || data.tutorialStep < 0 || data.tutorialStep >= steps.Count) return;
            IsActive = true;
            index = data.tutorialStep;
            GoalCompleted = data.tutorialGoalCompleted;
            // Re-read on loading, preserving actions and completed outcomes from this step.
            goalStarted = data.tutorialGoalStarted;
            signals.Clear();
            foreach (TutorialSignal signal in Enum.GetValues(typeof(TutorialSignal)))
                if ((data.tutorialSignals & (1 << (int)signal)) != 0) signals.Add(signal);
            if (index == 1 && goalStarted) PrepareGatherDemo();
            Current?.OnEnter?.Invoke();
            Review();
        }

        static void MarkDone()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        List<Step> BuildSteps()
        {
            return new List<Step>
            {
                new Step
                {
                    Title = "Welcome to the colony",
                    Body = "Your queen lives in the nest underground. Keep her alive through 10 waves of hungry bugs.\n\n" +
                           "This tutorial pauses while you read. Try each task, watch what happens, then choose Next when you are ready. Waves wait until you finish. You can re-read or skip at any time.",
                },
                new Step
                {
                    Title = "Gather food",
                    CanSkip = false,
                    Body = "Everything runs on food: ants eat, and new ants cost food.\n\n" +
                           "Pick which food piles your workers gather: press 6 (Gather), then click a pile, or hold the mouse button " +
                           "and drag a box over several. There's food right next to the ant hill: watch the little cursor.",
                    Goal = "Mark food with 6, then watch a worker bring it home",
                    HighlightKey = "6",
                    IsDone = () => Saw(TutorialSignal.FoodMarked) && Saw(TutorialSignal.FoodDelivered),
                    OnGoalStart = PrepareGatherDemo,
                    ShowsGatherDemo = true,
                },
                new Step
                {
                    Title = "Missions",
                    Body = "Every worker and builder can have a mission. Food gatherers fetch the marked food, larva feeders look after " +
                           "the young, diggers dig. Ants without a mission choose their own work.",
                    Goal = "Press M to open Missions",
                    HighlightKey = "M",
                    IsDone = () => game.Hud.IsMissionsOpen,
                },
                new Step
                {
                    Title = "Raise ants",
                    OnEnter = PrepareBroodView,
                    Body = "Press 1 for a worker, 2 for a soldier, 3 for a builder. It costs food.\n\n" +
                           "We have moved underground so you can watch: the queen walks to a free cradle and lays an egg. Workers feed the larva until a new ant hatches. You can use Skip step if you do not want to wait.",
                    Goal = "Press 1, then wait for your new worker to hatch",
                    HighlightKey = "1",
                    IsDone = () => Saw(TutorialSignal.AntQueued) && Saw(TutorialSignal.WorkerHatched),
                },
                new Step
                {
                    Title = "Explore the nest",
                    OnSkip = () => { if (game.CameraRig.View != CameraView.Nest) game.CameraRig.ToggleView(); },
                    Body = "You are already underground after watching the brood. Take a moment to find the queen, tunnels and rooms. Tab switches between the meadow above and the nest below; return underground before continuing.",
                    Goal = "Explore the underground nest, then choose Next",
                    IsDone = () => game.CameraRig.View == CameraView.Nest,
                },
                new Step
                {
                    Title = "Dig",
                    CanSkip = false,
                    Body = "Press 4 and drag a box over brown soil next to a tunnel. Builders dig it out.\n\n" +
                           "Digging finds stone (grey, pebbly soil gives the most), sometimes seeds or grubs. Stone builds walls and towers.",
                    Goal = "Mark soil with 4 and watch a builder finish digging",
                    HighlightKey = "4",
                    IsDone = () => Saw(TutorialSignal.DigMarked) && Saw(TutorialSignal.TileDug),
                },
                new Step
                {
                    Title = "Rooms",
                    Body = "Rooms go on dug-out tunnel:\n" +
                           "5 Storage holds food and leaves.\n6 Cradle holds one egg.\n7 Fungus Garden turns leaves into food.",
                    Goal = "Place a room (5, 6 or 7) and wait for construction",
                    HighlightKey = "5",
                    IsDone = () => Saw(TutorialSignal.RoomPlaced) && Saw(TutorialSignal.RoomBuilt),
                },
                new Step
                {
                    Title = "Back to the meadow",
                    OnSkip = () => { if (game.CameraRig.View != CameraView.Surface) game.CameraRig.ToggleView(); },
                    Body = "Enemies come from the edges of the meadow and attack the ant hill. Let's get ready.",
                    Goal = "Press Tab to go back up",
                    HighlightKey = "Tab",
                    IsDone = () => game.CameraRig.View == CameraView.Surface,
                },
                new Step
                {
                    Title = "Defences",
                    Body = "Press 4 and drag to build a line of walls (stone). Press 5 to place an acid tower (a little food and stone): " +
                           "it shoots enemies in range.\n\nAnts can't walk through walls either: leave a way out for your workers!",
                    Goal = "Place a wall (4) or tower (5), then let a builder finish",
                    HighlightKey = "4",
                    IsDone = () => (Saw(TutorialSignal.WallPlaced) || Saw(TutorialSignal.TowerPlaced)) && Saw(TutorialSignal.StructureBuilt),
                },
                new Step
                {
                    Title = "Soldiers",
                    Body = "Soldiers fight. Press 2 for a biter, Z for a spitter that spits acid from a distance (keep it behind the biters).\n\n" +
                           "Drag a box over them to select them, right-click to send them somewhere or at an enemy. " +
                           "E selects all soldiers, Q halves the group.",
                    Goal = "Raise a soldier (2 or Z) or select your soldiers",
                    HighlightKey = "2",
                    OnGoalStart = () => game.Selection.Clear(),
                    IsDone = () => Saw(TutorialSignal.SoldierQueued) || game.Selection.Selected.Count > 0,
                },
                new Step
                {
                    Title = "You're ready",
                    Body = "The advisor (top right) warns you about problems. R opens the colony report, H the controls, " +
                           "Esc pauses.\n\nYou have at least 45 seconds to prepare before the first wave. Good luck!",
                },
            };
        }
    }
}
