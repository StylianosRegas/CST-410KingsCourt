using System.IO;
using KingsCourt.Core;
using KingsCourt.UnityLayer;
using UnityEditor;
using UnityEngine;

namespace KingsCourt.EditorTools
{
    /// <summary>Build 8: creates and checks the balance configuration asset. Menu: King's Court / Balance.</summary>
    public static class BalanceAssetTools
    {
        private const string AssetPath = "Assets/Resources/BalanceConfig.asset";

        /// <summary>Creates Assets/Resources/BalanceConfig.asset from the factory values if it does not exist yet.</summary>
        [MenuItem("King's Court/Balance/Create Balance Config (if missing)")]
        public static void CreateIfMissing()
        {
            if (File.Exists(AssetPath)) { Debug.Log("BalanceConfig already exists at " + AssetPath); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var asset = ScriptableObject.CreateInstance<BalanceAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + AssetPath + " from the factory balance values.");
        }

        /// <summary>Batch check: the asset loads, and prints every value that differs from the factory set
        /// (so the changelog entries can be compared with what is actually in the asset).</summary>
        [MenuItem("King's Court/Balance/Report differences from factory values")]
        public static void Report()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BalanceAsset>(AssetPath);
            if (asset == null) { Debug.LogError("No balance asset at " + AssetPath); EditorApplication.Exit(2); return; }
            var factory = JsonUtility.ToJson(BalanceConfig.Factory(), true).Split('\n');
            var actual = JsonUtility.ToJson(asset.Values, true).Split('\n');
            int diffs = 0;
            for (int i = 0; i < System.Math.Max(factory.Length, actual.Length); i++)
            {
                string f = i < factory.Length ? factory[i] : "", a = i < actual.Length ? actual[i] : "";
                if (f != a) { diffs++; Debug.Log("BALANCE DIFF  factory: " + f.Trim() + "   asset: " + a.Trim()); }
            }
            Debug.Log("BALANCE REPORT: asset loaded, " + actual.Length + " lines, " + diffs + " differences from factory values.");
        }

        public static void CreateAndReportBatch()
        {
            CreateIfMissing();
            Report();
        }
    }
}
