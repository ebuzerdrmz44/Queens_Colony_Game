using UnityEditor;
using UnityEngine;

namespace AntColony
{
    /// <summary>Editor shortcuts for testing a running game.</summary>
    public static class TestMenu
    {
        [MenuItem("Ant Colony/Test/Boss Fight (Play mode, F8)")]
        static void BossFight()
        {
            ColonyGame game = Object.FindFirstObjectByType<ColonyGame>();
            if (!Application.isPlaying || game == null)
            {
                EditorUtility.DisplayDialog("Boss test", "Press Play first, then choose this again (or press F8 in the game).", "OK");
                return;
            }
            game.SetUpBossTest();
        }
    }
}
