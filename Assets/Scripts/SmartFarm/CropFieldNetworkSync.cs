using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Syncs all CropGrowthController states (stage + health) from the owner to every client.
    ///
    /// Uses NetworkVariableWritePermission.Owner:
    ///   LocalOnly → host owns scene-placed NetworkObjects (IsOwner = true)
    ///   DA mode   → session creator is assigned ownership of scene-placed NetworkObjects
    ///
    /// Packed format (FixedString512Bytes):
    ///   "stage,health,stage,health,..."   — 2 tokens per crop, worst case 6 chars → 85 crops max.
    ///
    /// Attach to FarmSimulationHub (same GameObject as the NetworkObject).
    /// GrowthManager calls SetCropData() after each authority tick.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CropFieldNetworkSync : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString512Bytes> _cropData =
            new NetworkVariable<FixedString512Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        private readonly StringBuilder _sb = new StringBuilder(256);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _cropData.OnValueChanged += OnCropDataChanged;

            // Client: apply whatever the owner has already synced (handles late join)
            if (!IsOwner)
                ApplyCropData(_cropData.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            _cropData.OnValueChanged -= OnCropDataChanged;
            base.OnNetworkDespawn();
        }

        // ── Owner (authority) API ─────────────────────────────────────────────

        /// <summary>Called by GrowthManager after each tick to broadcast crop states.</summary>
        public void SetCropData(System.Collections.Generic.IReadOnlyList<CropGrowthController> crops)
        {
            if (!IsOwner || crops == null || crops.Count == 0) return;

            _sb.Clear();
            int limit = Mathf.Min(crops.Count, 80); // stay inside 512-byte budget
            for (int i = 0; i < limit; i++)
            {
                var crop = crops[i];
                if (crop == null) continue;

                if (_sb.Length > 0) _sb.Append(',');
                _sb.Append((int)crop.CurrentStage);
                _sb.Append(',');
                _sb.Append(Mathf.RoundToInt(crop.Health));
            }

            var packed  = _sb.ToString();
            var current = _cropData.Value.ToString();
            if (packed == current) return; // skip write if unchanged

            if (packed.Length < 511)
                _cropData.Value = new FixedString512Bytes(packed);
        }

        // ── Client side ───────────────────────────────────────────────────────

        private void OnCropDataChanged(FixedString512Bytes _, FixedString512Bytes newValue)
        {
            if (IsOwner) return; // owner already has the ground truth
            ApplyCropData(newValue.ToString());
        }

        private static void ApplyCropData(string packed)
        {
            if (string.IsNullOrEmpty(packed)) return;

            var gm = GrowthManager.Instance;
            if (gm == null) return;

            var crops = gm.GetAllCrops();
            if (crops == null || crops.Count == 0) return;

            var tokens = packed.Split(',');
            int cropIndex = 0;

            for (int t = 0; t + 1 < tokens.Length && cropIndex < crops.Count; t += 2, cropIndex++)
            {
                if (!int.TryParse(tokens[t],     out int stageInt))  continue;
                if (!int.TryParse(tokens[t + 1], out int healthInt)) continue;

                var crop = crops[cropIndex];
                if (crop == null) continue;

                crop.ApplyNetworkState(
                    (CropStage)Mathf.Clamp(stageInt, 0, 4),
                    Mathf.Clamp(healthInt, 0, 100));
            }
        }
    }
}
