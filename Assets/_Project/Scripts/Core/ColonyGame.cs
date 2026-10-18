using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AntColony
{
    public enum GameState
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>
    /// Entry point of the ant colony game. Put this on one GameObject in a scene and press Play:
    /// it builds the surface, the nest, the colony, the spawners, the camera and the HUD at runtime.
    /// Use the menu "Ant Colony > Create Game Scene" to set up a scene with it automatically.
    ///
    /// The world is split over three scenes that are loaded side by side:
    /// - this scene: the shared game logic (colony, camera, HUD, controls),
    /// - "Surface": the top-down overworld with food, enemies and the ant hill,
    /// - "Nest": the underground base with the chambers and the queen.
    /// Ants move between Surface and Nest through the entrance; Tab switches which one the camera shows.
    /// </summary>
    public class ColonyGame : MonoBehaviour
    {
        const float MessageDuration = 3f;

        [SerializeField] GameSettings settings = new GameSettings();

        [Tooltip("Drop sprites from your pixel-art pack here. Empty slots use generated autumn pixel art.")]
        [SerializeField] ArtOverrides art = new ArtOverrides();

        [Tooltip("Prefabs to spawn. Fill with \"Ant Colony > Generate Prefabs\". Empty slots are built in code.")]
        [SerializeField] PrefabLibrary prefabs = new PrefabLibrary();

        [Tooltip("Effect prefabs (e.g. particle systems) played at moments like bites, feeding larvae or finishing a room. Empty slots play nothing.")]
        [SerializeField] EffectLibrary effects = new EffectLibrary();

        [Tooltip("Images for the HUD panels, the advisor card and the action bar. Empty slots use the built-in pixel frames.")]
        [SerializeField] UiSkin uiSkin = new UiSkin();

        [Tooltip("Music, sound effects and volumes. Empty slots are filled from Assets/_Project/Art/Musics; changes while playing are heard at once.")]
        [SerializeField] GameAudio sound = new GameAudio();

        const string SurfaceSceneName = "Surface";
        const string NestSceneName = "Nest";

        Scene surfaceScene;
        Scene nestScene;
        Transform logicRoot;
        float messageTimeLeft;

        public GameSettings Settings => settings;
        public PrefabLibrary Prefabs => prefabs;
        public EffectLibrary Effects => effects;
        public UiSkin UiSkin => uiSkin;
        public GameAudio Sound => sound;
        public AudioManager Audio { get; private set; }

#if UNITY_EDITOR
        /// <summary>Picks up the HUD images in Assets/_Project/Art/UI for any empty skin slot.</summary>
        void OnValidate()
        {
            // Mark the scene changed when slots were filled, so saving it keeps them (they're needed in builds).
            if (FillEmptySlots()) UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Fills empty UI skin and audio slots from the project's Art folders. True if anything changed.</summary>
        bool FillEmptySlots()
        {
            if (uiSkin == null) uiSkin = new UiSkin();
            if (sound == null) sound = new GameAudio();
            bool changed = uiSkin.FillEmptyFromProject();
            changed |= sound.FillEmptyFromProject();
            return changed;
        }
#endif
        /// <summary>The sprite overrides from the Inspector (the prefab generator bakes them into the prefabs).</summary>
        public ArtOverrides ArtOverrides => art;
        public GameArt Art { get; private set; }
        public Surface Surface { get; private set; }
        public Nest Nest { get; private set; }
        public Colony Colony { get; private set; }
        public FoodSpawner Food { get; private set; }
        public WaveSpawner Waves { get; private set; }
        public CameraController CameraRig { get; private set; }
        public GameHud Hud { get; private set; }
        public SoldierSelection Selection { get; private set; }
        /// <summary>The Gather tool and the food piles marked with it.</summary>
        public GatherOrders Gather { get; private set; }
        public Tutorial Tutorial { get; private set; }
        public BuildMode BuildMode { get; private set; }
        public NestTools NestTools { get; private set; }
        public Defenses Defenses { get; private set; }

        /// <summary>Parent for everything that lives in the Surface scene.</summary>
        public Transform SurfaceRoot { get; private set; }
        /// <summary>Parent for everything that lives in the Nest scene.</summary>
        public Transform NestRoot { get; private set; }

        public GameState State { get; private set; }
        /// <summary>True while the title menu is showing (the world waits behind it, paused).</summary>
        public bool InMainMenu { get; private set; }
        /// <summary>True if a saved game exists (checked when the menu opens and after saving).</summary>
        public bool HasSave { get; private set; }
        public bool IsPaused { get; private set; }
        public string Message { get; private set; } = "";

        /// <summary>1 while a message is fresh, fading to 0 as it expires.</summary>
        public float MessageVisibility => Mathf.Clamp01(messageTimeLeft);

        void Awake()
        {
#if UNITY_EDITOR
            // Slots added by a code update stay empty until Unity validates this object; fill them for this play session.
            if (FillEmptySlots()) Debug.Log("Colony Game: filled empty UI Skin / Audio slots from the Art folders. Save the scene to keep them.");
#endif
            Art = new GameArt(art);

            Camera mainCamera = FindOrCreateCamera();
            CameraRig = GetOrAddComponent<CameraController>(mainCamera.gameObject);
            EnsureGlobalLight();

            Audio = gameObject.AddComponent<AudioManager>();
            Audio.Init(this, sound);
            gameObject.AddComponent<ColonyControls>().Init(this);
            Hud = gameObject.AddComponent<GameHud>();
            Hud.Init(this);
            Selection = gameObject.AddComponent<SoldierSelection>();
            Selection.Init(this);
            Gather = gameObject.AddComponent<GatherOrders>();
            Gather.Init(this);
            BuildMode = gameObject.AddComponent<BuildMode>();
            BuildMode.Init(this);
            NestTools = gameObject.AddComponent<NestTools>();
            NestTools.Init(this);
            Tutorial = gameObject.AddComponent<Tutorial>();
            Tutorial.Init(this);

            surfaceScene = GetOrCreateScene(SurfaceSceneName);
            nestScene = GetOrCreateScene(NestSceneName);
            BuildWorld();
            OpenMainMenu();
        }

        void Update()
        {
            if (messageTimeLeft > 0f) messageTimeLeft -= Time.unscaledDeltaTime;
            if (State != GameState.Playing || InMainMenu) return;

            if (Colony.IsQueenDead) EndGame(GameState.Lost);
            else if (Waves.AllWavesDefeated) EndGame(GameState.Won);
        }

        void OnDestroy()
        {
            // Never leave the editor (or the next scene) frozen.
            Time.timeScale = 1f;
        }

        /// <summary>The scene root a point belongs to: the surface if it is above ground, otherwise the nest.</summary>
        public Transform AreaRoot(Vector2 point) => Surface.IsOnSurface(point) ? SurfaceRoot : NestRoot;

        /// <summary>
        /// Spawns the effect prefab set for <paramref name="kind"/> and removes it once it has played.
        /// False if the slot is empty (so the caller can show a built-in effect instead).
        /// </summary>
        /// <param name="direction">
        /// Optional: the way the effect should point (e.g. a spit's flight). The prefab's "up" (as it looks in the
        /// prefab, e.g. a cone rotated -90 on X to spray upward) is turned to face it.
        /// </param>
        public bool PlayEffect(EffectKind kind, Vector2 position, Vector2? direction = null)
        {
            if (Audio != null) Audio.PlayFor(kind, position);
            GameObject prefab = effects.PrefabFor(kind);
            if (prefab == null) return false;

            // Keep the prefab's own rotation: particle systems are usually tilted so their cone sprays across the screen.
            Quaternion rotation = prefab.transform.rotation;
            if (direction.HasValue && direction.Value.sqrMagnitude > 0.0001f)
                rotation = Quaternion.FromToRotation(Vector3.up, direction.Value) * rotation;

            GameObject effect = Instantiate(prefab, position, rotation, AreaRoot(position));
            RaiseToEffectsOrder(effect);
            Destroy(effect, EffectLibrary.LifetimeOf(effect));
            return true;
        }

        /// <summary>Particle renderers drawn below <see cref="SortingOrder.Effects"/> are raised to it, so they show over units.</summary>
        public static void RaiseToEffectsOrder(GameObject effect)
        {
            foreach (ParticleSystemRenderer particles in effect.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                if (particles.sortingOrder < SortingOrder.Effects) particles.sortingOrder = SortingOrder.Effects;
            }
        }

        /// <summary>
        /// Starts a trail effect as a child of <paramref name="carrier"/> (e.g. a flying acid blob): it moves and turns
        /// with it. The prefab's Simulation Space decides the look: World leaves particles behind as a trail, Local keeps
        /// them with the blob. Null if the slot is empty. Call <see cref="DropTrail"/> when the carrier is gone.
        /// </summary>
        public GameObject AttachTrail(EffectKind kind, Transform carrier)
        {
            GameObject prefab = effects.PrefabFor(kind);
            if (prefab == null) return null;

            GameObject trail = Instantiate(prefab, carrier, false);
            trail.transform.localPosition = Vector3.zero;
            RaiseToEffectsOrder(trail);
            FollowTransformVelocity(trail);
            return trail;
        }

        /// <summary>
        /// Flying shots have no Rigidbody, so their particles must measure movement from the Transform; otherwise
        /// "Rate over Distance" sees no movement and emits nothing.
        /// </summary>
        public static void FollowTransformVelocity(GameObject carrier)
        {
            foreach (ParticleSystem particles in carrier.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            }
        }

        /// <summary>Lets go of a trail: it stays where it is, stops emitting, and is removed once its last particles fade.</summary>
        public void DropTrail(GameObject trail)
        {
            if (trail == null) return;
            trail.transform.SetParent(AreaRoot(trail.transform.position), true);

            float longest = 0f;
            foreach (ParticleSystem particles in trail.GetComponentsInChildren<ParticleSystem>())
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                longest = Mathf.Max(longest, particles.main.startLifetime.constantMax);
            }
            foreach (TrailRenderer line in trail.GetComponentsInChildren<TrailRenderer>())
            {
                line.emitting = false;
                longest = Mathf.Max(longest, line.time);
            }
            Destroy(trail, longest + 0.1f);
        }

        public void ShowMessage(string text)
        {
            Message = text;
            messageTimeLeft = MessageDuration;
        }

        public void TogglePause()
        {
            if (State != GameState.Playing || InMainMenu) return;

            IsPaused = !IsPaused;
            Time.timeScale = IsPaused || tutorialPause ? 0f : 1f;
        }

        bool tutorialPause;

        /// <summary>Freezes the game while a tutorial pop-up is open.</summary>
        public void SetTutorialPause(bool paused)
        {
            tutorialPause = paused;
            if (paused) Hud.ClosePanels();
            if (State == GameState.Playing && !InMainMenu) Time.timeScale = paused || IsPaused ? 0f : 1f;
        }

        // ---------- Menu, saving and loading ----------

        /// <summary>Shows the title menu over a paused world.</summary>
        public void OpenMainMenu()
        {
            InMainMenu = true;
            IsPaused = false;
            Time.timeScale = 0f;
            HasSave = SaveData.Exists;
            Message = "";
            messageTimeLeft = 0f;
            Hud.ClosePanels();
            Selection.Clear();
            BuildMode.Stop();
            NestTools.Stop();
            Gather.Stop();
            Tutorial.Stop();
        }

        /// <summary>A fresh game. The tutorial runs on the very first one, or whenever <paramref name="withTutorial"/> asks for it.</summary>
        public void StartNewGame(bool withTutorial = false)
        {
            Restart();
            InMainMenu = false;
            if (withTutorial || Tutorial.IsFirstTime) Tutorial.Begin();
        }

        /// <summary>Writes the running game to the save file (one slot). False if it couldn't.</summary>
        public bool SaveGame()
        {
            if (State != GameState.Playing || InMainMenu) return false;

            var data = new SaveData { worldSeed = settings.worldSeed, onSurface = CameraRig.View == CameraView.Surface };
            Nest.Write(data);
            Colony.Write(data);
            Defenses.Write(data);
            Food.Write(data);
            Waves.Write(data);
            try
            {
                data.WriteToDisk();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Could not save to {SaveData.FilePath}: {exception.Message}");
                ShowMessage("Could not save the game.");
                return false;
            }

            HasSave = true;
            ShowMessage("Game saved.");
            return true;
        }

        /// <summary>Rebuilds the world from the save file. False (with a message) if there is no usable save.</summary>
        public bool LoadGame()
        {
            SaveData data = SaveData.ReadFromDisk(out string problem);
            if (data == null)
            {
                ShowMessage(problem);
                return false;
            }
            if (data.worldSeed != settings.worldSeed)
            {
                // The rock and scenery come from the seed, so the save only fits the world it was made in.
                ShowMessage($"That save was made with world seed {data.worldSeed}; this world uses {settings.worldSeed}.");
                return false;
            }

            Restart();
            if (!Nest.Read(data))
            {
                ShowMessage("The saved nest doesn't fit this nest's size. Started a new game instead.");
                InMainMenu = false;
                return false;
            }
            Colony.Read(data);
            Defenses.Read(data);
            Food.Read(data);
            Waves.Read(data);
            if (data.onSurface != (CameraRig.View == CameraView.Surface)) CameraRig.ToggleView();

            InMainMenu = false;
            ShowMessage($"Loaded the game saved {data.savedAt}.");
            return true;
        }

        /// <summary>
        /// A test of the final wave: a fresh game skipped to just before the last wave (the boss comes with it),
        /// with plenty of food and stone, soldiers and spitters at the rally flag, and two towers by the ant hill.
        /// It is saved to the save slot, so Continue / F9 brings it back for another try.
        /// Editor: Ant Colony > Test > Boss Fight (in Play mode), or F8.
        /// </summary>
        public void SetUpBossTest()
        {
            StartNewGame();
            Tutorial.Stop();

            Colony.AddFood(Colony.StorageCapacity, foraged: false);
            Colony.AddStone(80);
            Vector2 flag = Colony.RallyPoint;
            for (int i = 0; i < 8; i++) Colony.AddAntNow(AntRole.Soldier, Surface.Clamp(flag + Random.insideUnitCircle * 2f));
            for (int i = 0; i < 5; i++) Colony.AddAntNow(AntRole.Spitter, Surface.Clamp(flag + Random.insideUnitCircle * 2.5f));

            Vector2 hill = Surface.EntrancePoint;
            foreach (Vector2 offset in new[] { new Vector2(2.5f, 1.5f), new Vector2(-2.5f, -1.5f) })
            {
                if (!Defenses.CanPlace(StructureKind.Tower, hill + offset)) continue;
                Defenses.PlaceBlueprint(StructureKind.Tower, hill + offset).Restore(true, 1f, settings.towerHealth);
            }

            Waves.SkipTo(Waves.TotalWaves - 1, 8f);
            if (CameraRig.View != CameraView.Surface) CameraRig.ToggleView();
            SaveGame();
            ShowMessage("Boss test ready: the final wave starts in 8 s. Saved: press F9 to try again.");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Throws the current world away and builds a fresh one.</summary>
        public void Restart()
        {
            // Deactivate first: Destroy only happens at the end of the frame, and the old ants and
            // enemies must not run any more code against the new world in the meantime.
            Selection.Clear();
            BuildMode.Stop();
            Gather.Reset();
            NestTools.Stop();
            Tutorial.Stop();
            foreach (Transform root in new[] { logicRoot, SurfaceRoot, NestRoot })
            {
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
            }
            BuildWorld();
        }

        void BuildWorld()
        {
            logicRoot = new GameObject("Colony Logic").transform;
            SurfaceRoot = CreateSceneRoot("Surface World", surfaceScene);
            NestRoot = CreateSceneRoot("Nest World", nestScene);

            // Order matters: later parts use the earlier ones.
            Surface = CreatePart<Surface>("Surface", SurfaceRoot);
            Surface.Build(this);

            Nest = CreatePart<Nest>("Nest", NestRoot);
            Nest.Build(this);

            Waves = CreatePart<WaveSpawner>("Enemies", SurfaceRoot);
            Waves.Init(this);

            Food = CreatePart<FoodSpawner>("Food", SurfaceRoot);
            Food.Init(this);

            Defenses = CreatePart<Defenses>("Defenses", SurfaceRoot);
            Defenses.Init(this);

            Colony = CreatePart<Colony>("Colony", logicRoot);
            Colony.Init(this);
            CreatePart<Crowd>("Crowd", logicRoot).Init(this);

            CameraRig.Init(this, CameraRig.GetComponent<Camera>());
            CreatePart<FallingLeaves>("Falling Leaves", SurfaceRoot).Init(this, CameraRig.Camera);

            State = GameState.Playing;
            IsPaused = false;
            Time.timeScale = 1f;
            ShowMessage("Autumn is here. Gather food before the first wave!");
        }

        void EndGame(GameState result)
        {
            State = result;
            IsPaused = false;
            Time.timeScale = 0f;
        }

        static T CreatePart<T>(string partName, Transform parent) where T : Component
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            return part.AddComponent<T>();
        }

        static Transform CreateSceneRoot(string rootName, Scene scene)
        {
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        /// <summary>The Surface and Nest scenes are made at runtime, since everything in them is generated.</summary>
        static Scene GetOrCreateScene(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded ? scene : SceneManager.CreateScene(sceneName);
        }

        static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) component = target.AddComponent<T>();
            return component;
        }

        static Camera FindOrCreateCamera()
        {
            if (Camera.main != null) return Camera.main;

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<AudioListener>();
            return cameraObject.AddComponent<Camera>();
        }

        /// <summary>The project uses the URP 2D renderer, where sprites are black without a light.</summary>
        static void EnsureGlobalLight()
        {
            if (FindAnyObjectByType<Light2D>() != null) return;

            Light2D globalLight = new GameObject("Global Light 2D").AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
        }
    }
}
