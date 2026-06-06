using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
#endif

namespace UnityTry.LipSyncTest
{
    [DisallowMultipleComponent]
    public sealed class TimelineRecorderBatchRunner : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float duration = 5f;
        [SerializeField] string outputFile = "Recordings/LipSyncTest/test_voice_sequence";

        public void Configure(float sequenceDuration, string recorderOutputFile)
        {
            duration = Mathf.Max(0.1f, sequenceDuration);
            outputFile = recorderOutputFile;
        }

#if UNITY_EDITOR
        RecorderController recorderController;

        public static void ConfigureMovieRecorderSettings(
            MovieRecorderSettings movieRecorder,
            string recorderName,
            string recorderOutputFile)
        {
            movieRecorder.name = recorderName;
            movieRecorder.Enabled = true;
            movieRecorder.CaptureAudio = true;
            movieRecorder.CaptureAlpha = false;
            movieRecorder.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movieRecorder.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1280,
                OutputHeight = 720,
            };
            movieRecorder.OutputFile = recorderOutputFile;
            movieRecorder.FrameRate = 30f;
        }

        void OnEnable()
        {
            if (!Application.isBatchMode) return;

            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputFile));
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movieRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            ConfigureMovieRecorderSettings(movieRecorder, "Batch TestVoice Movie Recorder", outputFile);

            settings.AddRecorderSettings(movieRecorder);
            settings.FrameRate = 30f;
            settings.SetRecordModeToTimeInterval(0f, duration);

            recorderController = new RecorderController(settings);
            recorderController.PrepareRecording();
            recorderController.StartRecording();

            var director = FindAnyObjectByType<PlayableDirector>();
            if (director)
            {
                director.time = 0d;
                director.Play();
            }
        }

        void Update()
        {
            if (!Application.isBatchMode || recorderController == null) return;
            if (recorderController.IsRecording()) return;

            recorderController.StopRecording();
            recorderController = null;
            EditorApplication.Exit(HasExpectedOutput() ? 0 : 1);
        }

        void OnDisable()
        {
            if (recorderController == null) return;

            recorderController.StopRecording();
            recorderController = null;
        }

        bool HasExpectedOutput()
        {
            var expectedOutputPath = Path.GetFullPath(outputFile + ".mp4");
            var fileInfo = new FileInfo(expectedOutputPath);
            return fileInfo.Exists && fileInfo.Length > 0;
        }
#endif
    }
}
