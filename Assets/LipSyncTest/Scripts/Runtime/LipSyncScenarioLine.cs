using UnityEngine;

namespace UnityTry.LipSyncTest
{
    [CreateAssetMenu(fileName = "LipSyncScenarioLine", menuName = "LipSync Test/Scenario Line")]
    public sealed class LipSyncScenarioLine : ScriptableObject
    {
        [SerializeField] string speakerName;
        [SerializeField, TextArea] string lineText;
        [SerializeField] string wavAssetPath;

        public string SpeakerName => speakerName;
        public string LineText => lineText;
        public string WavAssetPath => wavAssetPath;
    }
}
