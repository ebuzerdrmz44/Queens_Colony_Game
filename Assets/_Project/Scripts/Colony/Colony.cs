using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The colony's economy and population:
    /// - food and leaves in storage (leaves only become food in a fungus garden),
    /// - upkeep: every ant eats a little all the time, and starves if the food runs out,
    /// - raising ants: the player queues castes, the queen lays an egg in a free cradle, workers feed the larva,
    /// - the queen, and the rally flag where new soldiers gather.
    /// </summary>
    public class Colony : MonoBehaviour
    {
        const float RallyFlagWidth = 0.75f;
        /// <summary>Size and colour of the queen's body when it is built in code (also used by the prefab generator).</summary>
        public const float QueenWidth = 2.6f;
        public static readonly Color QueenTint = new Color(1f, 0.82f, 0.45f);
        const int MaxQueued = 12;

        readonly List<Ant> ants = new List<Ant>();
        readonly List<AntRole> raiseQueue = new List<AntRole>();
        ColonyGame game;
        HealthBar queenHealthBar;
        Transform rallyFlag;
        float food;
        float eggTimer;
        Queen queen;

        /// <summary>The queen's object (hidden and shown by the intro).</summary>
        public Transform QueenTransform => queen != null ? queen.transform : null;

        /// <summary>
        /// How far the queen's head is in front of the middle of her body (she faces right; the head is the front
        /// quarter of the art), so she can be placed with her head on a spot.
        /// </summary>
        public static float QueenHeadOffset(SpriteRenderer body) => body != null ? body.bounds.size.x * 0.26f : 0f;
        float starvationTick;

        public IReadOnlyList<Ant> Ants => ants;
        public IReadOnlyList<AntRole> RaiseQueue => raiseQueue;
        /// <summary>Created in <see cref="Init"/>: it reads the clock, which Unity forbids while a component is being constructed.</summary>
        public ColonyStats Stats { get; private set; }
        public Advisor Advisor { get; private set; }

        public int Food => Mathf.FloorToInt(food);
        public int Leaves { get; private set; }
        /// <summary>Building material from digging. Kept apart from food storage, with no limit.</summary>
        public int Stone { get; private set; }
        public float QueenHealth { get; private set; }
        public Vector2 RallyPoint { get; private set; }
        public bool IsStarving { get; private set; }
        /// <summary>How many eggs the player has ever queued (all castes / soldiers only), for the goals.</summary>
        public int EggsOrdered { get; private set; }
        public int SoldiersOrdered { get; private set; }

        public int Population => ants.Count;
        public float QueenMaxHealth => game.Settings.queenMaxHealth;
        public bool IsQueenDead => QueenHealth <= 0f;

        /// <summary>Food and leaves share the same storage space.</summary>
        public int StorageCapacity =>
            game.Settings.baseStorage + game.Nest.CountBuilt(RoomType.Storage) * game.Settings.storageRoomCapacity;
        public int FreeSpace => Mathf.Max(0, StorageCapacity - Food - Leaves);
        public bool IsStorageFull => FreeSpace <= 0;

        /// <summary>Food eaten per minute by everyone right now.</summary>
        public float UpkeepPerMinute => ants.Count * game.Settings.antFoodPerMinute + game.Settings.queenFoodPerMinute;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            GameSettings settings = game.Settings;

            Stats = new ColonyStats();
            food = Mathf.Min(settings.startingFood, StorageCapacity);
            Stone = settings.startingStone;
            QueenHealth = settings.queenMaxHealth;
            Advisor = new Advisor(game);

            CreateQueen();
            CreateRallyFlag();
            SetRallyPoint(game.Surface.EntrancePoint + new Vector2(2.5f, -1.5f));

            for (int i = 0; i < settings.startingWorkers; i++) SpawnAnt(AntRole.Worker, RandomQueenRoomPoint());
            // All but one of the first workers start as food gatherers, so marking food (Gather, 6) is all it takes to begin.
            for (int i = 0; i < settings.startingWorkers - 1; i++) AssignMission(Mission.Forage);
            for (int i = 0; i < settings.startingSoldiers; i++) SpawnAnt(AntRole.Soldier, RandomQueenRoomPoint());
            for (int i = 0; i < settings.startingBuilders; i++) SpawnAnt(AntRole.Builder, RandomQueenRoomPoint());
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            // The queen slowly recovers while no enemy is biting at the entrance.
            if (!IsQueenDead && !game.Waves.IsNestUnderAttack && !IsStarving)
                QueenHealth = Mathf.Min(QueenMaxHealth, QueenHealth + game.Settings.queenRegenPerSecond * deltaTime);
            queenHealthBar.SetFraction(QueenHealth / QueenMaxHealth);

            EatUpkeep(deltaTime);
            RunGardens(deltaTime);
            LayEggs(deltaTime);
            GrowBrood(deltaTime);
            Advisor.Update(deltaTime);
        }

        // ---------- Player actions ----------

        /// <summary>Adds an ant of this caste to the queen's queue. It costs food only as the egg is laid and the larva fed.</summary>
        public void QueueRaise(AntRole role)
        {
            if (raiseQueue.Count >= MaxQueued)
            {
                game.ShowMessage("The queen's queue is full.");
                return;
            }

            raiseQueue.Add(role);
            game.Tutorial.Notify(TutorialSignal.AntQueued);
            if (role == AntRole.Soldier || role == AntRole.Spitter) game.Tutorial.Notify(TutorialSignal.SoldierQueued);
            EggsOrdered++;
            if (role == AntRole.Soldier) SoldiersOrdered++;
            game.ShowMessage($"The queen will lay a {role.ToString().ToLower()} egg ({game.Settings.RaiseCost(role)} food in all).");
        }

        public int CountQueued(AntRole role)
        {
            int count = 0;
            foreach (AntRole queued in raiseQueue)
            {
                if (queued == role) count++;
            }
            return count;
        }

        /// <summary>Takes food for something the player bought, or explains why it can't be afforded.</summary>
        public bool TrySpendFood(int cost)
        {
            if (Food < cost)
            {
                game.ShowMessage($"Not enough food (need {cost}).");
                game.Audio.Play(Sound.UiError);
                return false;
            }

            food -= cost;
            Stats.Spent.Add(cost);
            return true;
        }

        public void SetRallyPoint(Vector2 point)
        {
            RallyPoint = game.Surface.Clamp(point);
            rallyFlag.position = RallyPoint;
        }

        // ---------- Used by ants, rooms and enemies ----------

        /// <summary>Stores food; returns how much fit. Whatever didn't fit is lost.</summary>
        public int AddFood(int amount, bool foraged)
        {
            int accepted = Mathf.Min(amount, FreeSpace);
            food += accepted;
            if (foraged)
            {
                Stats.ForagedFood.Add(accepted);
                if (accepted > 0) game.Tutorial.Notify(TutorialSignal.FoodDelivered);
            }
            Stats.Wasted.Add(amount - accepted);
            return accepted;
        }

        public void AddStone(int amount)
        {
            Stone += Mathf.Max(0, amount);
        }

        /// <summary>Pays a price in food and stone, or explains which is missing (and pays nothing).</summary>
        public bool TrySpend(int foodCost, int stoneCost)
        {
            if (Food < foodCost || Stone < stoneCost)
            {
                string missing = Food < foodCost ? $"food (need {foodCost})" : $"stone (need {stoneCost}, dig for it)";
                game.ShowMessage($"Not enough {missing}.");
                game.Audio.Play(Sound.UiError);
                return false;
            }

            food -= foodCost;
            Stone -= stoneCost;
            Stats.Spent.Add(foodCost);
            return true;
        }

        /// <summary>Stores leaves; returns how many fit.</summary>
        public int AddLeaves(int amount)
        {
            int accepted = Mathf.Min(amount, FreeSpace);
            Leaves += accepted;
            Stats.ForagedLeaves.Add(accepted);
            if (accepted > 0) game.Tutorial.Notify(TutorialSignal.FoodDelivered);
            Stats.Wasted.Add(amount - accepted);
            return accepted;
        }

        /// <summary>Takes food out of storage for a larva. Returns false if there isn't enough.</summary>
        public bool TryTakeFood(int amount)
        {
            if (Food < amount) return false;
            food -= amount;
            Stats.Spent.Add(amount);
            return true;
        }

        /// <summary>Puts food back that was taken out but not used (e.g. the larva was gone).</summary>
        public void ReturnFood(int amount)
        {
            food = Mathf.Min(StorageCapacity - Leaves, food + amount);
        }

        public void DamageQueen(float amount)
        {
            QueenHealth = Mathf.Max(0f, QueenHealth - amount);
        }

        public void RemoveAnt(Ant ant)
        {
            ants.Remove(ant);
        }

        /// <summary>Ants of the mission's caste on that mission (<see cref="Mission.Auto"/> counts that caste's free ants).</summary>
        public int CountOnMission(Mission mission, AntRole role)
        {
            int count = 0;
            foreach (Ant ant in ants)
            {
                if (ant.Role == role && ant.Mission == mission) count++;
            }
            return count;
        }

        /// <summary>True if an ant on <paramref name="mission"/> is waiting for work.</summary>
        public bool HasIdleAntOnMission(Mission mission)
        {
            foreach (Ant ant in ants)
            {
                if (ant.Mission == mission && !ant.IsBusy) return true;
            }
            return false;
        }

        /// <summary>
        /// Moves one ant of the right caste onto <paramref name="mission"/>: a free one if there is one, otherwise one
        /// taken from the busiest other mission. False if every ant of that caste is already on it.
        /// </summary>
        public bool AssignMission(Mission mission)
        {
            AntRole role = Missions.RoleOf(mission);
            if (MoveOneAnt(role, Mission.Auto, mission)) return true;

            Mission busiest = Mission.Auto;
            int most = 0;
            foreach (Mission other in role == AntRole.Builder ? Missions.BuilderMissions : Missions.WorkerMissions)
            {
                if (other == mission) continue;
                int count = CountOnMission(other, role);
                if (count > most)
                {
                    most = count;
                    busiest = other;
                }
            }
            return most > 0 && MoveOneAnt(role, busiest, mission);
        }

        /// <summary>Takes one ant off <paramref name="mission"/> and lets it choose its own work again.</summary>
        public bool ReleaseMission(Mission mission)
        {
            return MoveOneAnt(Missions.RoleOf(mission), mission, Mission.Auto);
        }

        /// <summary>Moves one ant from one mission to another, preferring one that isn't in the middle of a job.</summary>
        bool MoveOneAnt(AntRole role, Mission from, Mission to)
        {
            Ant chosen = null;
            foreach (Ant ant in ants)
            {
                if (ant.Role != role || ant.Mission != from) continue;
                chosen = ant;
                if (!ant.IsBusy) break;
            }
            if (chosen == null) return false;

            chosen.Mission = to;
            return true;
        }

        /// <summary>Ants of the mission's caste on that mission that are doing a job right now.</summary>
        public int CountBusyOnMission(Mission mission, AntRole role)
        {
            int count = 0;
            foreach (Ant ant in ants)
            {
                if (ant.Role == role && ant.Mission == mission && ant.IsBusy) count++;
            }
            return count;
        }

        public int CountAnts(AntRole role)
        {
            int count = 0;
            foreach (Ant ant in ants)
            {
                if (ant.Role == role) count++;
            }
            return count;
        }

        public int CountWorkers(WorkerJob job)
        {
            int count = 0;
            foreach (Ant ant in ants)
            {
                if (ant is WorkerAnt worker && worker.Job == job) count++;
            }
            return count;
        }

        /// <summary>Every soldier, in the nest or on the surface.</summary>
        public IEnumerable<SoldierAnt> Soldiers
        {
            get
            {
                foreach (Ant ant in ants)
                {
                    if (ant is SoldierAnt soldier) yield return soldier;
                }
            }
        }

        /// <summary>Every egg and larva in the nest's cradles.</summary>
        public IEnumerable<Brood> Brood
        {
            get
            {
                foreach (Room room in game.Nest.Rooms)
                {
                    if (room.Brood != null) yield return room.Brood;
                }
            }
        }

        /// <summary>A larva that needs food and that no worker is bringing food to yet, or null.</summary>
        public Brood FindHungryLarva()
        {
            foreach (Brood brood in Brood)
            {
                if (brood.IsHungry && !brood.HasNurse) return brood;
            }
            return null;
        }

        /// <summary>
        /// The closest ant on the surface whose body is within <paramref name="maxGap"/> of the body of a creature
        /// of <paramref name="radius"/> at <paramref name="point"/>, or null.
        /// </summary>
        public Ant FindNearestSurfaceAnt(Vector2 point, float radius, float maxGap)
        {
            Ant nearest = null;
            float nearestDistance = maxGap;

            foreach (Ant ant in ants)
            {
                if (!ant.IsOnSurface) continue;

                float distance = Vector2.Distance(ant.Position, point) - radius - ant.Radius;
                if (distance <= nearestDistance)
                {
                    nearest = ant;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        public Vector2 RandomQueenRoomPoint() => game.Nest.RandomPointIn(game.Nest.QueenRoom);

        // ---------- Economy ----------

        void EatUpkeep(float deltaTime)
        {
            float eaten = UpkeepPerMinute / 60f * deltaTime;
            float available = Mathf.Min(food, eaten);
            food -= available;
            Stats.Upkeep.Add(available);

            IsStarving = food <= 0.001f && eaten > available;
            if (!IsStarving) return;

            // Starving ants lose health once a second, so the warning has time to be seen.
            starvationTick += deltaTime;
            if (starvationTick < 1f) return;
            starvationTick = 0f;

            float damage = game.Settings.starvationDamagePerSecond;
            for (int i = ants.Count - 1; i >= 0; i--) ants[i].Health.TakeDamage(damage, starvation: true);
            DamageQueen(damage * 0.5f);
        }

        void RunGardens(float deltaTime)
        {
            GameSettings settings = game.Settings;
            foreach (Room room in game.Nest.Rooms)
            {
                if (room.Type != RoomType.Garden || !room.IsBuilt) continue;

                // A garden needs a leaf to work on, and room in storage for the food (the leaf frees one spot).
                room.IsWorking = Leaves > 0 && FreeSpace + 1 >= settings.gardenFoodPerLeaf;
                if (!room.IsWorking) continue;

                room.GardenTimer += deltaTime;
                if (room.GardenTimer < settings.gardenCycleTime) continue;

                room.GardenTimer = 0f;
                Leaves--;
                food += settings.gardenFoodPerLeaf;
                Stats.GardenFood.Add(settings.gardenFoodPerLeaf);
                game.PlayEffect(EffectKind.GardenMadeFood, room.StandPoint);
            }
        }

        void LayEggs(float deltaTime)
        {
            if (raiseQueue.Count == 0) return;

            // One egg at a time: she lays it in person, then walks home.
            if (queen != null && queen.IsBusy) return;

            eggTimer += deltaTime;
            if (eggTimer < game.Settings.eggLayInterval) return;

            Room cradle = FindFreeCradle();
            if (cradle == null || Food < game.Settings.eggCost)
            {
                eggTimer = game.Settings.eggLayInterval * 0.8f; // try again shortly; the advisor explains why
                return;
            }

            eggTimer = 0f;
            food -= game.Settings.eggCost;
            Stats.Spent.Add(game.Settings.eggCost);
            AntRole caste = raiseQueue[0];
            raiseQueue.RemoveAt(0);
            if (queen != null) queen.LayEggIn(cradle, caste);
            else PlaceEgg(cradle, caste);
        }

        /// <summary>The egg appears in the cradle (called by the queen when it pops out).</summary>
        public void PlaceEgg(Room cradle, AntRole caste)
        {
            cradle.Brood = new Brood(game, cradle, caste);
            game.PlayEffect(EffectKind.EggLaid, cradle.StandPoint);
        }

        /// <summary>An egg the queen couldn't lay (its cradle was removed): back to the front of the queue, food refunded.</summary>
        public void ReturnEgg(AntRole caste)
        {
            raiseQueue.Insert(0, caste);
            food += game.Settings.eggCost;
        }

        public Room FindFreeCradle()
        {
            Room best = game.Nest.FindNearestRoom(RoomType.Cradle, game.Nest.QueenRoom.StandPoint, room => room.Brood == null && !room.AwaitingEgg, out _);
            return best;
        }

        void GrowBrood(float deltaTime)
        {
            foreach (Room room in game.Nest.Rooms)
            {
                Brood brood = room.Brood;
                if (brood == null) continue;

                brood.Update(deltaTime);
                if (!brood.IsReadyToHatch) continue;

                SpawnAnt(brood.Caste, room.StandPoint);
                if (brood.Caste == AntRole.Worker) game.Tutorial.Notify(TutorialSignal.WorkerHatched);
                game.PlayEffect(EffectKind.AntHatched, room.StandPoint);
                brood.Destroy();
                room.Brood = null;
                game.ShowMessage($"A new {brood.Caste.ToString().ToLower()} has grown up.");
            }
        }

        // ---------- Set-up ----------

        /// <summary>Adds a grown ant at once (for test set-ups such as <see cref="ColonyGame.SetUpBossTest"/>).</summary>
        public Ant AddAntNow(AntRole role, Vector2 position) => SpawnAnt(role, position);

        Ant SpawnAnt(AntRole role, Vector2 position)
        {
            Ant ant;
            Ant prefab = game.Prefabs.AntFor(role);
            Transform area = game.AreaRoot(position);
            if (prefab != null)
            {
                ant = Instantiate(prefab, area, false);
                ant.name = role.ToString();
            }
            else
            {
                var antObject = new GameObject(role.ToString());
                antObject.transform.SetParent(area, false);
                switch (role)
                {
                    case AntRole.Worker: ant = antObject.AddComponent<WorkerAnt>(); break;
                    case AntRole.Soldier: ant = antObject.AddComponent<SoldierAnt>(); break;
                    case AntRole.Spitter: ant = antObject.AddComponent<SpitterAnt>(); break;
                    default: ant = antObject.AddComponent<BuilderAnt>(); break;
                }
            }

            ants.Add(ant);
            ant.Init(game, position);
            return ant;
        }

        // ---------- Saving ----------

        public void Write(SaveData data)
        {
            data.food = food;
            data.leaves = Leaves;
            data.stone = Stone;
            data.queenHealth = QueenHealth;
            data.eggTimer = eggTimer;
            data.rallyPoint = RallyPoint;
            foreach (AntRole role in raiseQueue) data.raiseQueue.Add((int)role);
            if (queen != null && queen.IsCarryingEgg)
            {
                // An egg on its way to a cradle is saved as not laid yet.
                data.raiseQueue.Insert(0, (int)queen.CarriedCaste);
                data.food += game.Settings.eggCost;
            }
            foreach (Ant ant in ants)
            {
                data.ants.Add(new AntSave
                {
                    role = (int)ant.Role,
                    mission = (int)ant.Mission,
                    position = ant.Position,
                    health = ant.Health.Current,
                });
            }
        }

        /// <summary>Replaces the fresh colony's resources and ants with the saved ones.</summary>
        public void Read(SaveData data)
        {
            food = data.food;
            Leaves = data.leaves;
            Stone = data.stone;
            QueenHealth = Mathf.Clamp(data.queenHealth, 1f, QueenMaxHealth);
            queenHealthBar.SetFraction(QueenHealth / QueenMaxHealth);
            eggTimer = data.eggTimer;
            SetRallyPoint(data.rallyPoint);
            raiseQueue.Clear();
            foreach (int role in data.raiseQueue) raiseQueue.Add((AntRole)role);

            foreach (Ant ant in ants)
            {
                ant.gameObject.SetActive(false);
                Destroy(ant.gameObject);
            }
            ants.Clear();

            foreach (AntSave saved in data.ants)
            {
                Ant ant = SpawnAnt((AntRole)saved.role, saved.position);
                ant.Mission = (Mission)saved.mission;
                ant.Health.SetCurrent(saved.health);
            }
        }

        void CreateQueen()
        {
            Room chamber = game.Nest.QueenRoom;
            GameObject prefab = game.Prefabs.queen;
            Transform queen = prefab != null ? Instantiate(prefab, game.NestRoot, false).transform : new GameObject().transform;
            queen.name = "Queen";
            queen.SetParent(game.NestRoot, false);
            queen.position = game.Nest.FootprintBottomCenter(chamber.Origin, chamber.Size) + new Vector2(0f, 0.08f);

            SpriteRenderer body = UnitBody.FindOrCreate(game.Art, queen, game.Art.Ant, QueenWidth, SortingOrder.Queen, QueenTint);
            // She faces right with her head at the middle of the nest (over the shaft), her abdomen behind her.
            queen.position -= new Vector3(QueenHeadOffset(body), 0f, 0f);

            // She never walks, but she breathes and looks about. A UnitAnimator on the queen prefab keeps its own settings.
            bool tuned = queen.TryGetComponent(out UnitAnimator _);
            var frames = new CreatureFrames(new[] { body.sprite }, new[] { body.sprite });
            UnitAnimator idle = UnitAnimator.Attach(queen, body, BodyView.Side, frames);
            if (!queen.TryGetComponent(out this.queen)) this.queen = queen.gameObject.AddComponent<Queen>();
            this.queen.Init(game, this, idle);
            // Her own frame-by-frame art (idle, walk, laying, wing flaps), if the prefab has it.
            if (queen.TryGetComponent(out QueenAnimator queenFrames)) queenFrames.Init(body, this.queen);
            if (!tuned)
            {
                idle.idleFps = 3f;
                idle.breathPixels = new[] { 0, 0, 1, 1, 2, 2, 1, 1 };
                idle.fidgetEvery = 5f;
            }
            queenHealthBar = HealthBar.Create(game.Art, queen, GameArt.Height(body) + 0.25f, 1.6f, AutumnPalette.Gold, alwaysVisible: true);
        }

        void CreateRallyFlag()
        {
            SpriteRenderer flag = game.Art.CreateSprite("Rally Flag", game.SurfaceRoot, game.Art.RallyFlag, SortingOrder.RallyFlag);
            GameArt.ScaleToWidth(flag, RallyFlagWidth);

            // Parent the sprite under an anchor at ground level so moving the flag is a single position change.
            rallyFlag = new GameObject("Rally Point").transform;
            rallyFlag.SetParent(game.SurfaceRoot, false);
            flag.transform.SetParent(rallyFlag, false);
            flag.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(flag), 0f);
        }
    }
}
