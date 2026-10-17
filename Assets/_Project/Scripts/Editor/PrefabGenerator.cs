using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AntColony.EditorTools
{
    /// <summary>
    /// Adds "Ant Colony > Generate Prefabs" to the menu bar. It saves the game's generated pixel art as PNG sprites
    /// (Assets/_Project/Art/Sprites), builds a prefab for every ant, the queen, every enemy type, walls, towers and food
    /// and every piece of overworld scenery (Assets/_Project/Prefabs) and plugs them into the "Colony Game" object of the open scene.
    /// After that the prefabs are ordinary assets: change their sprites, scale, colour or add components, and the game
    /// spawns them as they are. Sprite overrides set on the Colony Game are baked into the prefabs.
    /// </summary>
    public static class PrefabGenerator
    {
        const string PrefabFolder = "Assets/_Project/Prefabs";
        const string SpriteFolder = "Assets/_Project/Art/Sprites";

        static readonly Dictionary<Sprite, Sprite> exported = new Dictionary<Sprite, Sprite>();
        /// <summary>PNGs written in this run; only these get the pixel-art import settings, so settings the team changed stay.</summary>
        static readonly HashSet<Sprite> written = new HashSet<Sprite>();
        static bool replaceExisting;

        [MenuItem("Ant Colony/Generate Prefabs")]
        static void GenerateFromMenu()
        {
            int choice = EditorUtility.DisplayDialogComplex("Ant Colony",
                "Create prefabs and sprites in Assets/_Project?\n\n" +
                "\"Missing only\" keeps prefabs and sprites that already exist, so your edits are safe.\n" +
                "\"Replace all\" rebuilds every one of them from the generated art.",
                "Missing only", "Cancel", "Replace all");
            if (choice == 1) return;

            ColonyGame game = Object.FindAnyObjectByType<ColonyGame>();
            if (game == null)
            {
                EditorUtility.DisplayDialog("Ant Colony",
                    "No Colony Game object in the open scene, so the prefabs will be created but not plugged in.\n" +
                    "Open Assets/_Project/Scenes/AntColony.unity and run this again to plug them in.", "OK");
            }

            Generate(game, choice == 2);
        }

        /// <summary>Creates (or, with <paramref name="replace"/>, rebuilds) every prefab and assigns them to <paramref name="game"/> if given.</summary>
        public static void Generate(ColonyGame game, bool replace)
        {
            replaceExisting = replace;
            exported.Clear();
            written.Clear();
            try
            {
                var art = new GameArt(game != null ? game.ArtOverrides : new ArtOverrides());
                List<EnemyStats> enemyTypes = game != null ? game.Settings.enemyTypes : new GameSettings().enemyTypes;

                EnsureFolderExists(SpriteFolder);
                AssetDatabase.StartAssetEditing();
                try
                {
                    ExportAll(art, enemyTypes);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                ConfigureSpriteImports();

                WorkerAnt worker = AntPrefab<WorkerAnt>("Worker", art);
                SoldierAnt soldier = AntPrefab<SoldierAnt>("Soldier", art);
                BuilderAnt builder = AntPrefab<BuilderAnt>("Builder", art);
                GameObject queen = QueenPrefab(art);
                Structure wall = StructurePrefab(StructureKind.Wall, art.Wall);
                Structure tower = StructurePrefab(StructureKind.Tower, art.Tower);
                FoodSource leaf = FoodPrefab("Leaf", art.Leaf, FoodSpawner.LeafWidth);
                FoodSource berries = FoodPrefab("Berries", art.Berry, FoodSpawner.BerriesWidth);
                FoodSource acorn = FoodPrefab("Acorn", art.Acorn, FoodSpawner.AcornWidth);

                var enemies = new List<Enemy>();
                foreach (EnemyStats stats in enemyTypes)
                    enemies.Add(EnemyPrefab(stats, art));

                GameObject[] trees = SceneryPrefabs("Tree", SceneryType.Tree, art.Trees, art);
                GameObject[] bushes = SceneryPrefabs("Bush", SceneryType.Bush, art.Bushes, art);
                GameObject[] boulders = SceneryPrefabs("Boulder", SceneryType.Boulder, art.Boulders, art);
                GameObject[] stumps = SceneryPrefabs("Stump", SceneryType.Stump, new[] { art.Stump }, art);
                GameObject[] logs = SceneryPrefabs("Fallen Log", SceneryType.Log, new[] { art.Log }, art);
                string[] flowerNames = { "Cream", "Gold", "Plum", "Orange" };
                var flowers = new GameObject[Surface.FlowerColors.Length];
                for (int i = 0; i < flowers.Length; i++)
                    flowers[i] = SceneryPrefab($"Flower {flowerNames[i % flowerNames.Length]}", SceneryType.Flower, art.Flower, art, Surface.FlowerColors[i]);

                if (game != null)
                {
                    var serialized = new SerializedObject(game);
                    serialized.FindProperty("prefabs.worker").objectReferenceValue = worker;
                    serialized.FindProperty("prefabs.soldier").objectReferenceValue = soldier;
                    serialized.FindProperty("prefabs.builder").objectReferenceValue = builder;
                    serialized.FindProperty("prefabs.queen").objectReferenceValue = queen;
                    serialized.FindProperty("prefabs.wall").objectReferenceValue = wall;
                    serialized.FindProperty("prefabs.tower").objectReferenceValue = tower;
                    serialized.FindProperty("prefabs.leaf").objectReferenceValue = leaf;
                    serialized.FindProperty("prefabs.berries").objectReferenceValue = berries;
                    serialized.FindProperty("prefabs.acorn").objectReferenceValue = acorn;

                    SetObjects(serialized.FindProperty("prefabs.trees"), trees);
                    SetObjects(serialized.FindProperty("prefabs.bushes"), bushes);
                    SetObjects(serialized.FindProperty("prefabs.boulders"), boulders);
                    SetObjects(serialized.FindProperty("prefabs.stumps"), stumps);
                    SetObjects(serialized.FindProperty("prefabs.logs"), logs);
                    SetObjects(serialized.FindProperty("prefabs.flowers"), flowers);

                    SerializedProperty types = serialized.FindProperty("settings.enemyTypes");
                    for (int i = 0; i < types.arraySize && i < enemies.Count; i++)
                        types.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue = enemies[i];

                    serialized.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Ant Colony prefabs are in {PrefabFolder}" +
                          (game != null ? " and plugged into the Colony Game object. Save the scene to keep them." : "."));
            }
            finally
            {
                exported.Clear();
                written.Clear();
            }
        }

        // ---------- Sprites ----------

        /// <summary>Writes a PNG for every generated sprite the prefabs use.</summary>
        static void ExportAll(GameArt art, List<EnemyStats> enemyTypes)
        {
            var sprites = new List<Sprite>();
            void AddFrames(CreatureFrames frames)
            {
                sprites.AddRange(frames.Walk);
                sprites.AddRange(frames.Attack);
            }

            AddFrames(art.AntSide);
            AddFrames(art.AntTop);
            foreach (EnemyStats stats in enemyTypes) AddFrames(art.EnemyFrames(stats.kind));
            sprites.AddRange(new[] { art.Crumb, art.SelectionRing, art.Wall, art.Tower, art.Leaf, art.Berry, art.Acorn });
            sprites.AddRange(art.Trees);
            sprites.AddRange(art.Bushes);
            sprites.AddRange(art.Boulders);
            sprites.AddRange(new[] { art.Stump, art.Log, art.Flower, art.Shadow });

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null || AssetDatabase.Contains(sprite)) continue;

                string path = SpritePath(sprite);
                if (replaceExisting || !File.Exists(path))
                {
                    File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
                    written.Add(sprite);
                }
                exported[sprite] = null;
            }
        }

        /// <summary>Imports the PNGs as crisp pixel-art sprites matching the generated ones (16 pixels per unit, centred pivot).</summary>
        static void ConfigureSpriteImports()
        {
            foreach (Sprite sprite in new List<Sprite>(exported.Keys))
            {
                string path = SpritePath(sprite);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                // Unity imports a new PNG as a sprite at 100 pixels per unit, so "already a sprite" says nothing:
                // set up every PNG written in this run.
                if (written.Contains(sprite))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }

                exported[sprite] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        /// <summary>The saved sprite asset for a generated sprite (sprites that already are assets come back as they are).</summary>
        static Sprite Asset(Sprite sprite)
        {
            if (sprite == null) return null;
            return exported.TryGetValue(sprite, out Sprite asset) && asset != null ? asset : sprite;
        }

        static Sprite[] Assets(Sprite[] sprites) => System.Array.ConvertAll(sprites, Asset);

        static string SpritePath(Sprite sprite)
        {
            string name = sprite.name;
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return $"{SpriteFolder}/{name}.png";
        }

        // ---------- Prefabs ----------

        static T AntPrefab<T>(string name, GameArt art) where T : Ant
        {
            string path = $"{PrefabFolder}/Ants/{name}.prefab";
            if (TryKeep(path, out T existing)) return existing;

            var root = new GameObject(name);
            T ant = root.AddComponent<T>();
            Sprite[] sideWalk = Assets(art.AntSide.Walk);
            UnitBody.Create(art, root.transform, sideWalk[0], ant.BodyWidth, SortingOrder.Ants, ant.Tint);

            var serialized = new SerializedObject(ant);
            SetSprites(serialized.FindProperty("sideView.walk"), sideWalk);
            SetSprites(serialized.FindProperty("sideView.attack"), Assets(art.AntSide.Attack));
            SetSprites(serialized.FindProperty("topView.walk"), Assets(art.AntTop.Walk));
            SetSprites(serialized.FindProperty("topView.attack"), Assets(art.AntTop.Attack));
            serialized.FindProperty("sideViewFacesLeft").boolValue = art.AntSideFacesLeft;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Children the ant would otherwise make for itself, so they can be restyled in the prefab too.
            if (ant is WorkerAnt)
                art.CreateSprite("Carried Food", root.transform, Asset(art.Crumb), SortingOrder.CarriedFood).enabled = false;
            if (ant is SoldierAnt)
            {
                SpriteRenderer ring = art.CreateSprite("Selection Ring", root.transform, Asset(art.SelectionRing), SortingOrder.SelectionRings);
                ring.color = AutumnPalette.Selection;
                ring.enabled = false;
            }

            return Save<T>(root, path);
        }

        static GameObject QueenPrefab(GameArt art)
        {
            string path = $"{PrefabFolder}/Ants/Queen.prefab";
            if (TryKeep(path, out Transform existing)) return existing.gameObject;

            var root = new GameObject("Queen");
            UnitBody.Create(art, root.transform, Asset(art.Ant), Colony.QueenWidth, SortingOrder.Queen, Colony.QueenTint);
            return Save<Transform>(root, path).gameObject;
        }

        static Enemy EnemyPrefab(EnemyStats stats, GameArt art)
        {
            string path = $"{PrefabFolder}/Enemies/{FileName(stats.name)}.prefab";
            if (TryKeep(path, out Enemy existing)) return existing;

            var root = new GameObject(stats.name);
            Enemy enemy = root.AddComponent<Enemy>();
            CreatureFrames frames = art.EnemyFrames(stats.kind);
            Sprite[] walk = Assets(frames.Walk);
            UnitBody.Create(art, root.transform, walk[0], stats.bodyWidth, SortingOrder.Enemies, Color.white);

            var serialized = new SerializedObject(enemy);
            SetSprites(serialized.FindProperty("sprites.walk"), walk);
            SetSprites(serialized.FindProperty("sprites.attack"), Assets(frames.Attack));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return Save<Enemy>(root, path);
        }

        static Structure StructurePrefab(StructureKind kind, Sprite sprite)
        {
            string path = $"{PrefabFolder}/Defenses/{kind}.prefab";
            if (TryKeep(path, out Structure existing)) return existing;

            var root = new GameObject(kind.ToString());
            Structure structure = root.AddComponent<Structure>();
            SpriteRenderer visual = CreateSprite("Visual", root.transform, Asset(sprite), SortingOrder.Structures);
            GameArt.ScaleToWidth(visual, Defenses.RadiusOf(kind) * 2f);
            return Save<Structure>(root, path);
        }

        static FoodSource FoodPrefab(string name, Sprite sprite, float width)
        {
            string path = $"{PrefabFolder}/Food/{name}.prefab";
            if (TryKeep(path, out FoodSource existing)) return existing;

            var root = new GameObject(name);
            root.AddComponent<FoodSource>();
            SpriteRenderer visual = CreateSprite("Visual", root.transform, Asset(sprite), SortingOrder.Food);
            GameArt.ScaleToWidth(visual, width);
            return Save<FoodSource>(root, path);
        }

        /// <summary>One prefab per sprite variant, named "Tree 1", "Tree 2"... (no number if there is only one).</summary>
        static GameObject[] SceneryPrefabs(string name, SceneryType type, Sprite[] sprites, GameArt art)
        {
            var prefabs = new GameObject[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                prefabs[i] = SceneryPrefab(sprites.Length > 1 ? $"{name} {i + 1}" : name, type, sprites[i], art, null);
            return prefabs;
        }

        static GameObject SceneryPrefab(string name, SceneryType type, Sprite sprite, GameArt art, Color? color)
        {
            string path = $"{PrefabFolder}/Scenery/{name}.prefab";
            if (TryKeep(path, out Transform existing)) return existing.gameObject;

            var root = new GameObject(name);
            SpriteRenderer visual = Surface.BuildScenery(art, root.transform, type, Asset(sprite), Asset(art.Shadow));
            if (color.HasValue) visual.color = color.Value;
            return Save<Transform>(root, path).gameObject;
        }

        // ---------- Helpers ----------

        /// <summary>True (with the prefab's component) if the prefab exists and should be kept as it is.</summary>
        static bool TryKeep<T>(string path, out T component) where T : Component
        {
            component = null;
            if (replaceExisting) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null && prefab.TryGetComponent(out component);
        }

        static T Save<T>(GameObject root, string path) where T : Component
        {
            EnsureFolderExists(Path.GetDirectoryName(path).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        static void SetObjects(SerializedProperty array, Object[] objects)
        {
            array.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        static void SetSprites(SerializedProperty array, Sprite[] sprites)
        {
            array.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        static string FileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Enemy";
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }

        static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
