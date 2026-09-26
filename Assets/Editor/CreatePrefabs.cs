using System.IO;
using KingsCourt.Core;
using KingsCourt.UnityLayer;
using UnityEditor;
using UnityEngine;

namespace KingsCourt.EditorTools
{
    /// <summary>Writes Assets/Prefabs/CardView.prefab, the card UI prefab. Run from the King's Court menu.</summary>
    public static class CreatePrefabs
    {
        [MenuItem("King's Court/Create Card Prefab")]
        public static void CreateCardPrefab()
        {
            Directory.CreateDirectory("Assets/Prefabs");
            var canvas = new GameObject("PrefabCanvas", typeof(RectTransform), typeof(Canvas));
            var cv = CardView.Create(canvas.transform, "CardView");
            cv.Set(new ActionCard(0, ActionEffect.ExecuteAdvisor, "Execute an advisor"));
            PrefabUtility.SaveAsPrefabAsset(cv.gameObject, "Assets/Prefabs/CardView.prefab");
            Object.DestroyImmediate(canvas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created Assets/Prefabs/CardView.prefab");
        }
    }
}
