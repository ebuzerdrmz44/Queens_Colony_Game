using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AntColony.EditorTools
{
    /// <summary>
    /// Adds "Ant Colony > Create Game Scene" to the menu bar. It creates a ready-to-play scene with a camera,
    /// a global 2D light and the Colony Game object (with the prefabs from <see cref="PrefabGenerator"/> plugged in),
    /// and adds the scene to Build Settings.
    /// </summary>
    public static class ColonySceneBuilder
    {
        const string SceneFolder = "Assets/_Project/Scenes";
        const string ScenePath = SceneFolder + "/AntColony.unity";

        [MenuItem("Ant Colony/Create Game Scene")]
        static void CreateGameScene()
        {
            bool exists = File.Exists(ScenePath);
            if (exists && !EditorUtility.DisplayDialog("Ant Colony",
                    $"{ScenePath} already exists. Rebuild it?\n\nThe Colony Game settings in it (effects, prefabs, waves, audio, UI skin, art...) are kept.",
                    "Rebuild", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Keep the team's settings: read them from the existing scene before it is replaced.
            string keptSettings = null;
            if (exists)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ColonyGame existing = Object.FindFirstObjectByType<ColonyGame>();
                if (existing != null) keptSettings = EditorJsonUtility.ToJson(existing);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateGlobalLight();

            ColonyGame game = new GameObject("Colony Game").AddComponent<ColonyGame>();
            if (keptSettings != null) EditorJsonUtility.FromJsonOverwrite(keptSettings, game);
            // Keeps prefabs that already exist, so a new scene picks up the team's edited ones.
            PrefabGenerator.Generate(game, replace: false);

            EnsureFolderExists(SceneFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            Selection.activeGameObject = game.gameObject;
            Debug.Log($"Ant Colony scene created at {ScenePath}. Press Play!");
        }

        static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 3f, -10f);
            cameraObject.AddComponent<AudioListener>();

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = AutumnPalette.SkyTop;
        }

        static void CreateGlobalLight()
        {
            Light2D globalLight = new GameObject("Global Light 2D").AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
        }

        static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>Puts the scene first in Build Settings so builds start with it.</summary>
        static void AddToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
