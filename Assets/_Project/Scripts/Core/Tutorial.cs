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
    }

    /// <summary>
    /// A guided first game. Each step opens a pop-up that pauses the game and explains one thing; after "Got it"
    /// the game runs and a small goal bar (top middle) says what to do, with the matching button blinking, until the
    /// player has done it. Then the next pop-up opens. Waves wait until the tutorial is finished or skipped.
    /// It runs on the first new game (and from "Tutorial" in the main menu); "Skip tutorial" ends it at any step.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        const string DoneKey = "tutorial.done";
        /// <summary>A short breath between finishing a goal and the next pop-up.</summary>
        const float NextStepDelay = 0.8f;

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
            /// <summary>Show the little cursor that clicks, holds and drags over <see cref="DemoFood"/>.</summary>
            public bool ShowsGatherDemo;
        }

        /// <summary>The food pile near the ant hill the gather demo points at.</summary>
        public FoodSource DemoFood { get; private set; }

        ColonyGame game;
        List<Step> steps;
        int index = -1;
        float nextStepTimer = -1f;
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
            nextStepTimer = -1f;
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
            if (!ShowingPopup) return;
            ShowingPopup = false;
            game.SetTutorialPause(false);

            if (Current != null && Current.IsDone == null) NextStep(); // an explanation only
            else Current?.OnGoalStart?.Invoke();
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

        public void Notify(TutorialSignal signal)
        {
            if (IsActive) signals.Add(signal);
        }

        bool Saw(TutorialSignal signal) => signals.Contains(signal);

        void Update()
        {
            if (!IsActive || ShowingPopup || game == null || game.InMainMenu || game.State != GameState.Playing) return;

            if (nextStepTimer >= 0f)
            {
                nextStepTimer -= Time.unscaledDeltaTime;
                if (nextStepTimer < 0f) NextStep();
                return;
            }

            Step step = Current;
            if (step?.IsDone != null && step.IsDone())
            {
                if (game.Audio != null) game.Audio.Play(Sound.UiConfirm);
                nextStepTimer = NextStepDelay;
            }
        }

        void NextStep()
        {
            index++;
            signals.Clear();
            if (index >= steps.Count)
            {
                IsActive = false;
                MarkDone();
                return;
            }

            ShowingPopup = true;
            game.SetTutorialPause(true);
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
                           "This tutorial pauses the game while you read. The waves wait until it's done.",
                },
                new Step
                {
                    Title = "Gather food",
                    Body = "Everything runs on food: ants eat, and new ants cost food.\n\n" +
                           "Pick which food piles your workers gather: press 6 (Gather), then click a pile, or hold the mouse button " +
                           "and drag a box over several. There's food right next to the ant hill: watch the little cursor.",
                    Goal = "Press 6, then click or drag over the food",
                    HighlightKey = "6",
                    IsDone = () => game.Gather.MarkedCount > 0,
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
                    Body = "Press 1 for a worker, 2 for a soldier, 3 for a builder. It costs food.\n\n" +
                           "The queen walks to a free cradle and lays an egg. Workers feed the larva until a new ant hatches.",
                    Goal = "Press 1 to raise a worker",
                    HighlightKey = "1",
                    IsDone = () => Saw(TutorialSignal.AntQueued),
                },
                new Step
                {
                    Title = "The nest",
                    Body = "The colony has two views: the meadow above and the nest below. Tab switches between them.",
                    Goal = "Press Tab to look underground",
                    HighlightKey = "Tab",
                    IsDone = () => game.CameraRig.View == CameraView.Nest,
                },
                new Step
                {
                    Title = "Dig",
                    Body = "Press 4 and drag a box over brown soil next to a tunnel. Builders dig it out.\n\n" +
                           "Digging finds stone (grey, pebbly soil gives the most), sometimes seeds or grubs. Stone builds walls and towers.",
                    Goal = "Press 4 and drag over brown soil",
                    HighlightKey = "4",
                    IsDone = () => Saw(TutorialSignal.DigMarked),
                },
                new Step
                {
                    Title = "Rooms",
                    Body = "Rooms go on dug-out tunnel:\n" +
                           "5 Storage holds food and leaves.\n6 Cradle holds one egg.\n7 Fungus Garden turns leaves into food.",
                    Goal = "Place a room (5, 6 or 7) on dug tunnel",
                    HighlightKey = "5",
                    IsDone = () => Saw(TutorialSignal.RoomPlaced),
                },
                new Step
                {
                    Title = "Back to the meadow",
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
                    Goal = "Build a wall (4) or a tower (5)",
                    HighlightKey = "4",
                    IsDone = () => Saw(TutorialSignal.WallPlaced) || Saw(TutorialSignal.TowerPlaced),
                },
                new Step
                {
                    Title = "Soldiers",
                    Body = "Soldiers fight. Press 2 for a biter, Z for a spitter that spits acid from a distance (keep it behind the biters).\n\n" +
                           "Drag a box over them to select them, right-click to send them somewhere or at an enemy. " +
                           "E selects all soldiers, Q halves the group.",
                    Goal = "Raise a soldier (2 or Z) or select your soldiers",
                    HighlightKey = "2",
                    IsDone = () => Saw(TutorialSignal.SoldierQueued) || game.Selection.Selected.Count > 0,
                },
                new Step
                {
                    Title = "You're ready",
                    Body = "The advisor (top right) warns you about problems. R opens the colony report, H the controls, " +
                           "Esc pauses.\n\nThe first wave starts its countdown now. Good luck!",
                },
            };
        }
    }
}
