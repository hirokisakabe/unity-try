using System;
using System.Collections.Generic;
using UniVRM10;
using UnityEngine;
using uLipSync;

namespace UnityTry.LipSyncTest
{
    [DisallowMultipleComponent]
    public sealed class Vrm10BakedLipSyncDriver : MonoBehaviour
    {
        static readonly IReadOnlyDictionary<string, ExpressionKey> PhonemeMap =
            new Dictionary<string, ExpressionKey>(StringComparer.OrdinalIgnoreCase)
            {
                { "A", ExpressionKey.Aa },
                { "I", ExpressionKey.Ih },
                { "U", ExpressionKey.Ou },
                { "E", ExpressionKey.Ee },
                { "O", ExpressionKey.Oh },
            };

        [SerializeField] Vrm10Instance vrm;
        [SerializeField, Range(0f, 1f)] float maxWeight = 1f;

        void Reset()
        {
            vrm = GetComponent<Vrm10Instance>();
        }

        void Awake()
        {
            if (!vrm) vrm = GetComponent<Vrm10Instance>();
        }

        public void OnLipSyncUpdate(LipSyncInfo info)
        {
            if (!vrm && !TryGetComponent(out vrm)) return;
            var expression = vrm.Runtime?.Expression;
            if (expression == null) return;

            ResetMouth(expression);

            if (info.phonemeRatios == null) return;

            var volume = Mathf.Clamp01(info.volume);
            foreach (var pair in info.phonemeRatios)
            {
                if (!PhonemeMap.TryGetValue(pair.Key, out var key)) continue;

                var weight = Mathf.Clamp01(pair.Value) * volume * maxWeight;
                expression.SetWeight(key, weight);
            }
        }

        static void ResetMouth(Vrm10RuntimeExpression expression)
        {
            expression.SetWeight(ExpressionKey.Aa, 0f);
            expression.SetWeight(ExpressionKey.Ih, 0f);
            expression.SetWeight(ExpressionKey.Ou, 0f);
            expression.SetWeight(ExpressionKey.Ee, 0f);
            expression.SetWeight(ExpressionKey.Oh, 0f);
        }
    }
}
