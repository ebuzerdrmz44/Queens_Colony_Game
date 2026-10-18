using UnityEngine;

namespace AntColony
{
    public enum WorkerJob
    {
        Idle,
        Foraging,
        Nursing,
    }

    /// <summary>
    /// Workers keep the colony fed. Whenever one is free it picks, in this order:
    /// 1. nursing: fetch food from the nearest Storage room and carry it to a hungry larva,
    /// 2. foraging: walk to the best food pile on the surface and carry a load to the nearest Storage room,
    /// 3. resting in the queen's chamber (when storage is full or there is nothing to fetch).
    /// The nest layout matters: storage rooms close to the entrance and to the cradles mean shorter trips.
    /// </summary>
    public class WorkerAnt : Ant
    {
        enum State
        {
            Resting,
            GoingToFood,
            CarryingFoodHome,
            FetchingFoodForLarva,
            CarryingFoodToLarva,
        }

        const float RestTime = 1.5f;

        State state;
        float restTimeLeft;
        FoodSource targetFood;
        Brood targetLarva;
        FoodKind carriedKind;
        int carriedAmount;
        float tripStartTime;
        SpriteRenderer carriedFood;

        public override AntRole Role => AntRole.Worker;
        public WorkerJob Job { get; private set; }
        public override bool IsBusy => Job != WorkerJob.Idle;

        public override string Activity
        {
            get
            {
                if (IsFleeing) return base.Activity;
                bool leaves = state == State.GoingToFood ? targetFood != null && targetFood.Kind == FoodKind.Leaves : carriedKind == FoodKind.Leaves;
                switch (state)
                {
                    case State.GoingToFood: return leaves ? "going for leaves" : "going for food";
                    case State.CarryingFoodHome: return leaves ? "carrying leaves home" : "carrying food home";
                    case State.FetchingFoodForLarva: return "fetching food for a larva";
                    case State.CarryingFoodToLarva: return "feeding a larva";
                    default: return Mission == Mission.Forage || Mission == Mission.Leaves ? "waiting for marked food (6)" : "resting";
                }
            }
        }
        protected override float Speed => Game.Settings.workerSpeed;
        protected override float MaxHealth => Game.Settings.workerHealth;
        protected override bool SteersSmoothly => true;
        public override float BodyWidth => 0.9f;
        public override Color Tint => Color.white;

        protected override void OnSpawned()
        {
            carriedFood = Game.Art.FindOrCreateSprite("Carried Food", transform, Game.Art.Crumb, SortingOrder.CarriedFood, out _);
            carriedFood.enabled = false;
            Rest();
        }

        protected override void OnViewChanged(BodyView view)
        {
            // On its back in the nest; held over its middle when seen from above.
            float height = view == BodyView.Side ? BodyHeight + 0.05f : 0f;
            carriedFood.transform.localPosition = new Vector3(0f, height, 0f);
        }

        void Update()
        {
            if (FleeFromEnemies()) return;

            switch (state)
            {
                case State.Resting: UpdateResting(); break;
                case State.GoingToFood: UpdateGoingToFood(); break;
                case State.CarryingFoodHome: UpdateCarryingFoodHome(); break;
                case State.FetchingFoodForLarva: UpdateFetchingFoodForLarva(); break;
                case State.CarryingFoodToLarva: UpdateCarryingFoodToLarva(); break;
            }
        }

        // ---------- Choosing a job ----------

        void UpdateResting()
        {
            if (!FollowRoute()) return; // still walking to the queen's chamber

            restTimeLeft -= Time.deltaTime;
            if (restTimeLeft > 0f) return;
            restTimeLeft = RestTime;

            switch (Mission)
            {
                case Mission.Nurse: TryStartNursing(); break;
                case Mission.Forage: TryStartForaging(FoodKind.Food); break;
                case Mission.Leaves: TryStartForaging(FoodKind.Leaves); break;
                default:
                    // Free workers stay in the nest: they feed larvae, leaving them to the assigned feeders
                    // while one of those is free to go.
                    if (!Game.Colony.HasIdleAntOnMission(Mission.Nurse)) TryStartNursing();
                    break;
            }
        }

        bool TryStartNursing()
        {
            Colony colony = Game.Colony;
            int amount = Game.Settings.foodPerFeeding;
            if (colony.Food < amount) return false;

            Brood larva = colony.FindHungryLarva();
            if (larva == null) return false;

            // The queen's chamber doubles as storage when there is no Storage room.
            Room storage = NearestStorage();
            Vector2 pickup = storage != null ? Game.Nest.RandomPointIn(storage) : colony.RandomQueenRoomPoint();
            if (!GoTo(pickup)) return false;

            larva.HasNurse = true;
            targetLarva = larva;
            state = State.FetchingFoodForLarva;
            Job = WorkerJob.Nursing;
            return true;
        }

        bool TryStartForaging(FoodKind preferred)
        {
            if (Game.Colony.IsStorageFull) return false;

            // Gatherers only go out for the piles the player marked with the Gather tool: their own kind first,
            // any other marked pile when none of their kind is left.
            targetFood = Game.Food.ClaimBestFood(preferred, markedOnly: true) ?? Game.Food.ClaimBestFood(null, markedOnly: true);
            if (targetFood == null) return false;

            if (!GoTo(targetFood.RandomPickupPoint()))
            {
                targetFood.ReleaseClaim();
                targetFood = null;
                return false;
            }
            state = State.GoingToFood;
            Job = WorkerJob.Foraging;
            tripStartTime = Time.time;
            return true;
        }

        /// <summary>
        /// Drops a trip the new mission doesn't allow (food already in the jaws is still brought home) and
        /// looks for new work right away.
        /// </summary>
        protected override void OnMissionChanged()
        {
            bool allowsForaging = Mission == Mission.Forage || Mission == Mission.Leaves;
            bool allowsNursing = Mission == Mission.Auto || Mission == Mission.Nurse;

            switch (state)
            {
                case State.GoingToFood:
                    if (allowsForaging && targetFood != null) return;
                    if (targetFood != null) targetFood.ReleaseClaim();
                    targetFood = null;
                    break;

                case State.FetchingFoodForLarva:
                case State.CarryingFoodToLarva:
                    if (allowsNursing) return;
                    if (state == State.CarryingFoodToLarva) Game.Colony.ReturnFood(carriedAmount);
                    if (targetLarva != null) targetLarva.HasNurse = false;
                    targetLarva = null;
                    DropCarried();
                    break;

                case State.CarryingFoodHome:
                    return; // finish delivering what it carries

                default:
                    if (!allowsForaging && !allowsNursing) return;
                    break;
            }

            Rest(goToQueen: false);
            restTimeLeft = 0f;
        }

        // ---------- Foraging ----------

        void UpdateGoingToFood()
        {
            if (targetFood == null)
            {
                Rest();
                return;
            }

            if (!FollowRoute()) return;

            carriedKind = targetFood.Kind;
            carriedAmount = targetFood.Load;
            Game.PlayEffect(EffectKind.FoodPickedUp, targetFood.Position);
            targetFood.TakeClaimedUnit();
            targetFood = null;
            ShowCarried(carriedKind);

            Room storage = NearestStorage();
            GoTo(storage != null ? Game.Nest.RandomPointIn(storage) : Game.Colony.RandomQueenRoomPoint());
            state = State.CarryingFoodHome;
        }

        void UpdateCarryingFoodHome()
        {
            if (!FollowRoute()) return;

            Colony colony = Game.Colony;
            if (carriedKind == FoodKind.Leaves) colony.AddLeaves(carriedAmount);
            else colony.AddFood(carriedAmount, foraged: true);
            colony.Stats.AddTrip(Time.time - tripStartTime);
            Game.PlayEffect(EffectKind.FoodStored, Position);
            Game.Audio.Play(carriedKind == FoodKind.Leaves ? Sound.LeavesStored : Sound.FoodStored, Position);

            DropCarried();
            Rest(goToQueen: false);
            restTimeLeft = 0.2f; // go straight back to work
        }

        // ---------- Nursing ----------

        void UpdateFetchingFoodForLarva()
        {
            if (!LarvaStillHungry())
            {
                StopNursing();
                return;
            }

            if (!FollowRoute()) return;

            int amount = Game.Settings.foodPerFeeding;
            if (!Game.Colony.TryTakeFood(amount))
            {
                StopNursing();
                return;
            }

            carriedKind = FoodKind.Food;
            carriedAmount = amount;
            ShowCarried(FoodKind.Food);
            GoTo(targetLarva.Cradle.StandPoint);
            state = State.CarryingFoodToLarva;
        }

        void UpdateCarryingFoodToLarva()
        {
            if (!LarvaStillHungry())
            {
                Game.Colony.ReturnFood(carriedAmount);
                StopNursing();
                return;
            }

            if (!FollowRoute()) return;

            targetLarva.Feed(carriedAmount);
            Game.PlayEffect(EffectKind.LarvaFed, targetLarva.Cradle.StandPoint);
            StopNursing();
        }

        /// <summary>The larva hatched, was destroyed or got fed by someone else.</summary>
        bool LarvaStillHungry()
        {
            return targetLarva != null && targetLarva.Cradle.Brood == targetLarva && targetLarva.IsHungry;
        }

        void StopNursing()
        {
            if (targetLarva != null) targetLarva.HasNurse = false;
            targetLarva = null;
            DropCarried();
            Rest(goToQueen: false);
            restTimeLeft = 0.2f;
        }

        // ---------- Helpers ----------

        Room NearestStorage()
        {
            return Game.Nest.FindNearestRoom(RoomType.Storage, Position, null, out _);
        }

        void Rest(bool goToQueen = true)
        {
            state = State.Resting;
            Job = WorkerJob.Idle;
            restTimeLeft = RestTime;
            if (goToQueen) GoTo(Game.Colony.RandomQueenRoomPoint());
            else ClearRoute();
        }

        void ShowCarried(FoodKind kind)
        {
            carriedFood.sprite = kind == FoodKind.Leaves ? Game.Art.FallingLeaf : Game.Art.Crumb;
            carriedFood.color = kind == FoodKind.Leaves ? AutumnPalette.Orange : Color.white;
            carriedFood.transform.localScale = Vector3.one * (kind == FoodKind.Leaves ? 1.6f : 1f);
            carriedFood.enabled = true;
        }

        void DropCarried()
        {
            carriedFood.enabled = false;
            carriedAmount = 0;
        }

        protected override void OnDeath()
        {
            // Let another worker fetch the food this one had claimed.
            if (targetFood != null) targetFood.ReleaseClaim();
            if (targetLarva != null) targetLarva.HasNurse = false;
        }
    }
}
