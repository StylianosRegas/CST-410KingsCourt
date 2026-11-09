using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Build 8: the one configuration asset that holds every balance number in the game
    /// (Assets/Resources/BalanceConfig.asset). Open it in the Inspector, change a value, press Play:
    /// the next game uses it, with no code edited. The values live in BalanceConfig (Core) so the
    /// rules engine and the RulesSim tool read the very same fields.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "King's Court/Balance Config")]
    public sealed class BalanceAsset : ScriptableObject
    {
        public const string ResourcePath = "BalanceConfig";

        public BalanceConfig Values = BalanceConfig.Factory();

        /// <summary>Before the first scene loads, point the engine at the asset's values. The engine holds
        /// the asset's own object (not a copy), so a value edited in the Inspector is used by the next
        /// game that starts, even while playing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var asset = Resources.Load<BalanceAsset>(ResourcePath);
            if (asset == null || asset.Values == null)
            {
                Debug.LogWarning("BalanceConfig asset not found at Resources/" + ResourcePath + "; using the factory balance values.");
                return;
            }
            BalanceConfig.Current = asset.Values;
        }
    }
}
