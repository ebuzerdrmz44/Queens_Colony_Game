
      using System.IO;
      using UnityEditor;
      using UnityEditor.PackageManager;

      [InitializeOnLoad]
      public static class HubForceResolve
      {
          private const string k_ScriptPath = "Assets/Editor/HubForceResolve.cs";
          private const string k_EditorFolderPath = "Assets/Editor";
          private const string k_StateKey = "Hub.ForceResolve.State";

          static HubForceResolve()
          {
              EditorApplication.delayCall += Tick;
          }

          static void Tick()
          {
              // Wait until the Editor is idle so the resolve/self-delete don't fight the import.
              if (EditorApplication.isCompiling || EditorApplication.isUpdating)
              {
                  EditorApplication.delayCall += Tick;
                  return;
              }

              if (SessionState.GetString(k_StateKey, "") == "")
              {
                  // First idle pass: resolve. May reload, after which Tick runs again.
                  SessionState.SetString(k_StateKey, "resolved");
                  Client.Resolve();
                  EditorApplication.delayCall += Tick;
                  return;
              }

              // Resolved and idle again → remove the script.
              Cleanup();
          }

          static void Cleanup()
          {
              AssetDatabase.DeleteAsset(k_ScriptPath);
              CleanupEmptyEditorFolder();
          }

          static void CleanupEmptyEditorFolder()
          {
              string absolutePath = Path.GetFullPath(k_EditorFolderPath);

              if (!Directory.Exists(absolutePath)) return;

              string[] files = Directory.GetFiles(absolutePath);
              string[] dirs = Directory.GetDirectories(absolutePath);

              bool isEmpty = true;

              if (dirs.Length > 0)
              {
                  isEmpty = false;
              }

              foreach (string file in files)
              {
                  if (!file.EndsWith(".DS_Store"))
                  {
                      isEmpty = false;
                      break;
                  }
              }

              if (isEmpty)
              {
                  AssetDatabase.DeleteAsset(k_EditorFolderPath);
              }
          }
      }