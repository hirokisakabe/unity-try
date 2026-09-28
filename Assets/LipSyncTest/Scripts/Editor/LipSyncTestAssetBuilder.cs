using System.Collections.Generic;
using System.IO;
using UniVRM10;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Presets;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using uLipSync;
using uLipSync.Timeline;

namespace UnityTry.LipSyncTest.Editor
{
    public static class LipSyncTestAssetBuilder
    {
        const string Root = "Assets/LipSyncTest";
        const string ModelPath = Root + "/Models/TestAvatar.vrm";
        const string AudioPath = Root + "/Audio/TestVoice.wav";
        const string ProfilePath = Root + "/Profiles/uLipSync-Profile-Test.asset";
        const string BakedDataPath = Root + "/BakedData/TestVoice.asset";
        const string PrefabPath = Root + "/Prefabs/BakedLipSyncAvatar.prefab";
        const string ScenePath = Root + "/Scenes/BakedLipSyncTest.unity";
        const string TimelinePath = Root + "/Timeline/TestVoiceSequence.playable";
        const string RecorderPresetPath = Root + "/Recorder/TestVoiceMovieRecorder.preset";
        const string RecorderScenePath = Root + "/Scenes/TimelineRecorderTest.unity";
        const string RecorderOutputPath = "Recordings/LipSyncTest/test_voice_sequence";
        const string ScenarioPath = Root + "/Scenarios/TestVoiceScenario.asset";
        const string ScenarioBakedDataPath = Root + "/BakedData/TestVoiceScenario.asset";
        const string ScenarioPrefabPath = Root + "/Prefabs/TestVoiceScenarioAvatar.prefab";
        const string ScenarioTimelinePath = Root + "/Timeline/TestVoiceScenarioSequence.playable";
        const string ScenarioRecorderPresetPath = Root + "/Recorder/TestVoiceScenarioMovieRecorder.preset";
        const string ScenarioRecorderScenePath = Root + "/Scenes/TestVoiceScenarioRecorder.unity";
        const string ScenarioRecorderOutputPath = "Recordings/LipSyncTest/test_voice_scenario";
        const string PackageProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset";
        const string UniversalRenderPipelineImporterName = "UniversalRenderPipeline";
        const float CameraFieldOfView = 30f;
        const float CameraNearClipPlane = 0.05f;
        static readonly Vector3 CameraPosition = new Vector3(0f, 1.4f, 0.72f);
        static readonly Vector3 CameraTarget = new Vector3(0f, 1.36f, 0f);

        [MenuItem("Tools/LipSync Test/Rebuild Issue 4 Assets")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureDirectories();
            ImportInputs();

            var profile = EnsureProfile();
            var audioClip = LoadRequired<AudioClip>(AudioPath);
            var bakedData = EnsureBakedData(profile, audioClip);
            var avatarPrefab = BuildAvatarPrefab(bakedData, audioClip);
            BuildScene(avatarPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/LipSync Test/Rebuild Issue 5 Timeline Recorder Assets")]
        public static void BuildIssue5()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureDirectories();
            ImportInputs();

            var profile = EnsureProfile();
            var audioClip = LoadRequired<AudioClip>(AudioPath);
            var bakedData = EnsureBakedData(profile, audioClip);
            var avatarPrefab = BuildAvatarPrefab(bakedData, audioClip);
            var timeline = BuildTimeline(audioClip, bakedData);
            BuildRecorderPreset();
            BuildRecorderScene(avatarPrefab, timeline, audioClip.length);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/LipSync Test/Rebuild Issue 19 Scenario Assets")]
        public static void BuildIssue19()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureDirectories();
            ImportInputs();

            var scenario = LoadRequired<LipSyncScenarioLine>(ScenarioPath);
            ValidateScenarioFields(scenario);
            AssetDatabase.ImportAsset(scenario.WavAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var profile = EnsureProfile();
            var audioClip = LoadRequired<AudioClip>(scenario.WavAssetPath);
            var bakedData = EnsureBakedData(profile, audioClip, ScenarioBakedDataPath);
            var avatarPrefab = BuildAvatarPrefab(
                bakedData,
                audioClip,
                ScenarioPrefabPath,
                scenario.name + "Avatar");
            var timeline = BuildScenarioTimeline(scenario, audioClip, bakedData);
            BuildRecorderPreset(
                ScenarioRecorderPresetPath,
                scenario.name + " Movie Recorder",
                ScenarioRecorderOutputPath);
            BuildRecorderScene(
                avatarPrefab,
                timeline,
                audioClip.length,
                ScenarioRecorderScenePath,
                ScenarioRecorderOutputPath,
                ScenarioRecorderPresetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildFromBatch()
        {
            Build();
        }

        public static void BuildIssue5FromBatch()
        {
            BuildIssue5();
        }

        public static void BuildIssue19FromBatch()
        {
            BuildIssue19();
        }

        public static void ValidateFromBatch()
        {
            var avatarPrefab = LoadRequired<GameObject>(PrefabPath);
            var bakedData = LoadRequired<BakedData>(BakedDataPath);
            LoadRequired<SceneAsset>(ScenePath);
            LoadRequired<AudioClip>(AudioPath);
            LoadRequired<GameObject>(ModelPath);

            if (!bakedData.isValid)
            {
                throw new InvalidDataException("Baked data is invalid.");
            }

            var hasMouthFrame = false;
            foreach (var frame in bakedData.frames)
            {
                if (frame.volume <= 0f || frame.phonemes == null) continue;

                foreach (var phoneme in frame.phonemes)
                {
                    if (phoneme.ratio > 0.01f)
                    {
                        hasMouthFrame = true;
                        break;
                    }
                }

                if (hasMouthFrame) break;
            }

            if (!hasMouthFrame)
            {
                throw new InvalidDataException("Baked data does not contain non-zero phoneme frames.");
            }

            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
            try
            {
                if (!avatar.GetComponent<Vrm10Instance>())
                {
                    throw new MissingComponentException("Prefab does not have Vrm10Instance.");
                }

                var player = avatar.GetComponent<uLipSyncBakedDataPlayer>();
                if (!player || player.bakedData != bakedData)
                {
                    throw new MissingComponentException("Prefab does not have a configured uLipSyncBakedDataPlayer.");
                }

                var driver = avatar.GetComponent<Vrm10BakedLipSyncDriver>();
                if (!driver)
                {
                    throw new MissingComponentException("Prefab does not have Vrm10BakedLipSyncDriver.");
                }

                if (!avatar.GetComponent<Vrm10SimpleStageMotion>())
                {
                    throw new MissingComponentException("Prefab does not have Vrm10SimpleStageMotion.");
                }

                driver.OnLipSyncUpdate(new LipSyncInfo
                {
                    phoneme = "A",
                    volume = 1f,
                    rawVolume = 1f,
                    phonemeRatios = new Dictionary<string, float> { { "A", 1f } },
                });

                if (player.onLipSyncUpdate.GetPersistentEventCount() == 0)
                {
                    throw new MissingReferenceException("Baked data player has no persistent lip-sync listener.");
                }
            }
            finally
            {
                Object.DestroyImmediate(avatar);
            }
        }

        public static void ValidateIssue5FromBatch()
        {
            var timeline = LoadRequired<TimelineAsset>(TimelinePath);
            var recorderPreset = LoadRequired<Preset>(RecorderPresetPath);
            var audioClip = LoadRequired<AudioClip>(AudioPath);
            var bakedData = LoadRequired<BakedData>(BakedDataPath);
            LoadRequired<SceneAsset>(RecorderScenePath);

            var hasAudioTrack = false;
            var hasLipSyncTrack = false;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is AudioTrack)
                {
                    hasAudioTrack |= HasClipWithAsset<AudioPlayableAsset>(track);
                }
                else if (track is uLipSyncTrack)
                {
                    hasLipSyncTrack |= HasLipSyncClip(track, bakedData);
                }
            }

            if (!hasAudioTrack)
            {
                throw new InvalidDataException("Timeline does not contain an AudioTrack clip.");
            }

            if (!hasLipSyncTrack)
            {
                throw new InvalidDataException("Timeline does not contain a uLipSync Track clip bound to baked data.");
            }

            if (timeline.duration < Mathf.Min(audioClip.length, 5f) - 0.1f)
            {
                throw new InvalidDataException("Timeline duration is shorter than the configured test sequence.");
            }

            var recorderTargetType = recorderPreset.GetTargetTypeName();
            if (recorderTargetType != nameof(MovieRecorderSettings) &&
                recorderTargetType != typeof(MovieRecorderSettings).FullName)
            {
                throw new InvalidDataException("Recorder preset does not target MovieRecorderSettings.");
            }

            var scene = EditorSceneManager.OpenScene(RecorderScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<PlayableDirector>();
            if (!director || director.playableAsset != timeline)
            {
                throw new MissingReferenceException("Recorder scene does not have a PlayableDirector bound to the test timeline.");
            }

            var camera = Camera.main;
            if (!camera)
            {
                throw new MissingReferenceException("Recorder scene does not have a MainCamera.");
            }

            if (Vector3.Distance(camera.transform.position, CameraPosition) > 0.05f)
            {
                throw new InvalidDataException("Recorder scene camera is not in the expected avatar framing position.");
            }

            if (Mathf.Abs(camera.fieldOfView - CameraFieldOfView) > 0.1f)
            {
                throw new InvalidDataException("Recorder scene camera does not use the close-up framing field of view.");
            }

            var importer = AssetImporter.GetAtPath(ModelPath) ??
                throw new InvalidDataException("VRM importer is missing.");
            using var serializedImporter = new SerializedObject(importer);
            var renderPipeline = FindRenderPipelineProperty(serializedImporter);
            if (renderPipeline.enumValueIndex != GetUniversalRenderPipelineImporterIndex(renderPipeline))
            {
                throw new InvalidDataException("VRM importer is not configured for URP materials.");
            }

            var timelineEvent = Object.FindAnyObjectByType<uLipSyncTimelineEvent>();
            if (!timelineEvent || timelineEvent.onLipSyncUpdate.GetPersistentEventCount() == 0)
            {
                throw new MissingReferenceException("Recorder scene does not bind Timeline lip-sync output to the VRM driver.");
            }

            foreach (var track in timeline.GetOutputTracks())
            {
                var binding = director.GetGenericBinding(track);
                if (track is AudioTrack && binding is not AudioSource)
                {
                    throw new MissingReferenceException("AudioTrack is not bound to the avatar AudioSource.");
                }

                if (track is uLipSyncTrack && binding is not uLipSyncTimelineEvent)
                {
                    throw new MissingReferenceException("uLipSync Track is not bound to uLipSyncTimelineEvent.");
                }
            }

            if (!scene.IsValid())
            {
                throw new InvalidDataException("Recorder scene could not be opened for validation.");
            }
        }

        public static void ValidateIssue19FromBatch()
        {
            var scenario = LoadRequired<LipSyncScenarioLine>(ScenarioPath);
            ValidateScenarioFields(scenario);

            var audioClip = LoadRequired<AudioClip>(scenario.WavAssetPath);
            var bakedData = LoadRequired<BakedData>(ScenarioBakedDataPath);
            var timeline = LoadRequired<TimelineAsset>(ScenarioTimelinePath);
            var recorderPreset = LoadRequired<Preset>(ScenarioRecorderPresetPath);
            LoadRequired<GameObject>(ScenarioPrefabPath);
            LoadRequired<SceneAsset>(ScenarioRecorderScenePath);

            if (!bakedData.isValid || bakedData.audioClip != audioClip)
            {
                throw new InvalidDataException("Scenario baked data is invalid or references a different WAV asset.");
            }

            var expectedAudioTrackName = scenario.name + " Audio";
            var expectedLipSyncTrackName = scenario.name + " LipSync";
            var expectedCameraTrackName = scenario.name + " Camera";
            AudioTrack audioTrack = null;
            uLipSyncTrack lipSyncTrack = null;
            ActivationTrack cameraTrack = null;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is AudioTrack candidateAudioTrack && track.name == expectedAudioTrackName)
                {
                    audioTrack = candidateAudioTrack;
                }
                else if (track is uLipSyncTrack candidateLipSyncTrack && track.name == expectedLipSyncTrackName)
                {
                    lipSyncTrack = candidateLipSyncTrack;
                }
                else if (track is ActivationTrack candidateCameraTrack && track.name == expectedCameraTrackName)
                {
                    cameraTrack = candidateCameraTrack;
                }
            }

            if (audioTrack == null || !HasAudioClip(audioTrack, audioClip))
            {
                throw new InvalidDataException("Scenario Timeline does not contain its named AudioTrack and WAV clip.");
            }

            if (lipSyncTrack == null || !HasLipSyncClip(lipSyncTrack, bakedData))
            {
                throw new InvalidDataException("Scenario Timeline does not contain its named lip-sync track and baked data.");
            }

            if (cameraTrack == null || !HasAnyClip(cameraTrack))
            {
                throw new InvalidDataException("Scenario Timeline does not contain its named camera ActivationTrack.");
            }

            if (timeline.name != scenario.name + "Sequence" ||
                timeline.duration < Mathf.Min(audioClip.length, 5f) - 0.1f)
            {
                throw new InvalidDataException("Scenario Timeline identity or duration is inconsistent with the scenario WAV.");
            }

            var recorderTargetType = recorderPreset.GetTargetTypeName();
            if (recorderTargetType != nameof(MovieRecorderSettings) &&
                recorderTargetType != typeof(MovieRecorderSettings).FullName)
            {
                throw new InvalidDataException("Scenario Recorder preset does not target MovieRecorderSettings.");
            }

            var scene = EditorSceneManager.OpenScene(ScenarioRecorderScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<PlayableDirector>();
            if (!scene.IsValid() || !director || director.playableAsset != timeline)
            {
                throw new MissingReferenceException("Scenario Recorder scene is invalid or does not reference its Timeline.");
            }

            var camera = Camera.main;
            if (!camera || director.GetGenericBinding(cameraTrack) != camera.gameObject)
            {
                throw new MissingReferenceException("Scenario camera ActivationTrack is not bound to the MainCamera.");
            }

            if (director.GetGenericBinding(audioTrack) is not AudioSource)
            {
                throw new MissingReferenceException("Scenario AudioTrack is not bound to an AudioSource.");
            }

            if (director.GetGenericBinding(lipSyncTrack) is not uLipSyncTimelineEvent)
            {
                throw new MissingReferenceException("Scenario lip-sync track is not bound to uLipSyncTimelineEvent.");
            }

            var runner = Object.FindAnyObjectByType<TimelineRecorderBatchRunner>();
            if (!runner)
            {
                throw new MissingReferenceException("Scenario Recorder scene does not contain a batch Recorder runner.");
            }

            using var serializedRunner = new SerializedObject(runner);
            if (serializedRunner.FindProperty("outputFile").stringValue != ScenarioRecorderOutputPath ||
                serializedRunner.FindProperty("recorderPresetPath").stringValue != ScenarioRecorderPresetPath)
            {
                throw new InvalidDataException("Scenario Recorder runner output or preset is inconsistent.");
            }
        }

        public static void ExportIssue5MovieFromBatch()
        {
            BuildIssue5();

            var outputPath = Path.GetFullPath(RecorderOutputPath + ".mp4");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            EditorSceneManager.OpenScene(RecorderScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void ExportIssue19MovieFromBatch()
        {
            BuildIssue19();

            var outputPath = Path.GetFullPath(ScenarioRecorderOutputPath + ".mp4");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            EditorSceneManager.OpenScene(ScenarioRecorderScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void EnsureDirectories()
        {
            foreach (var path in new[]
            {
                Root + "/Models",
                Root + "/Audio",
                Root + "/Profiles",
                Root + "/BakedData",
                Root + "/Prefabs",
                Root + "/Scenes",
                Root + "/Timeline",
                Root + "/Recorder",
                Root + "/Scenarios",
            })
            {
                EnsureFolder(path);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidDataException($"Invalid asset folder path: {path}");
            }

            EnsureFolder(parent);

            var folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName)))
            {
                throw new IOException($"Failed to create Unity asset folder: {path}");
            }
        }

        static void ImportInputs()
        {
            EnsureVrmImporterSettings();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static void EnsureVrmImporterSettings()
        {
            var importer = AssetImporter.GetAtPath(ModelPath);
            if (!importer) return;

            using var serializedImporter = new SerializedObject(importer);
            var renderPipeline = FindRenderPipelineProperty(serializedImporter);
            var urpIndex = GetUniversalRenderPipelineImporterIndex(renderPipeline);
            if (renderPipeline.enumValueIndex == urpIndex) return;

            renderPipeline.enumValueIndex = urpIndex;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }

        static SerializedProperty FindRenderPipelineProperty(SerializedObject importer)
        {
            return importer.FindProperty("RenderPipeline") ??
                throw new InvalidDataException("VRM importer has no RenderPipeline property.");
        }

        static int GetUniversalRenderPipelineImporterIndex(SerializedProperty renderPipeline)
        {
            for (var i = 0; i < renderPipeline.enumNames.Length; i++)
            {
                if (renderPipeline.enumNames[i] == UniversalRenderPipelineImporterName)
                {
                    return i;
                }
            }

            throw new InvalidDataException("VRM importer does not expose a URP render pipeline option.");
        }

        static Profile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<Profile>(ProfilePath);
            if (profile) return profile;

            if (!AssetDatabase.CopyAsset(PackageProfilePath, ProfilePath))
            {
                throw new FileNotFoundException("Failed to copy uLipSync sample profile.", PackageProfilePath);
            }

            AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceSynchronousImport);
            return LoadRequired<Profile>(ProfilePath);
        }

        static BakedData EnsureBakedData(Profile profile, AudioClip audioClip)
        {
            return EnsureBakedData(profile, audioClip, BakedDataPath);
        }

        static BakedData EnsureBakedData(Profile profile, AudioClip audioClip, string bakedDataPath)
        {
            var bakedData = AssetDatabase.LoadAssetAtPath<BakedData>(bakedDataPath);
            if (!bakedData)
            {
                bakedData = ScriptableObject.CreateInstance<BakedData>();
                AssetDatabase.CreateAsset(bakedData, bakedDataPath);
            }

            bakedData.profile = profile;
            bakedData.audioClip = audioClip;

            var editor = UnityEditor.Editor.CreateEditor(bakedData, typeof(BakedDataEditor));
            try
            {
                ((BakedDataEditor)editor).Bake();
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }

            if (!bakedData.isValid)
            {
                throw new InvalidDataException("uLipSync baked data was generated but is invalid.");
            }

            EditorUtility.SetDirty(bakedData);
            return bakedData;
        }

        static GameObject BuildAvatarPrefab(BakedData bakedData, AudioClip audioClip)
        {
            return BuildAvatarPrefab(bakedData, audioClip, PrefabPath, "BakedLipSyncAvatar");
        }

        static GameObject BuildAvatarPrefab(
            BakedData bakedData,
            AudioClip audioClip,
            string prefabPath,
            string avatarName)
        {
            var modelPrefab = LoadRequired<GameObject>(ModelPath);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            avatar.name = avatarName;
            avatar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var vrm = avatar.GetComponent<Vrm10Instance>();
            if (!vrm)
            {
                Object.DestroyImmediate(avatar);
                throw new MissingComponentException("Imported model does not have Vrm10Instance.");
            }

            var audioSource = avatar.GetComponent<AudioSource>();
            if (!audioSource) audioSource = avatar.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.playOnAwake = false;

            var player = avatar.GetComponent<uLipSyncBakedDataPlayer>();
            if (!player) player = avatar.AddComponent<uLipSyncBakedDataPlayer>();
            player.audioSource = audioSource;
            player.bakedData = bakedData;
            player.playOnAwake = true;
            player.playAudioSource = true;
            player.timeOffset = 0.05f;
            player.volume = 1f;

            var driver = avatar.GetComponent<Vrm10BakedLipSyncDriver>();
            if (!driver) driver = avatar.AddComponent<Vrm10BakedLipSyncDriver>();

            if (!avatar.GetComponent<Vrm10SimpleStageMotion>())
            {
                avatar.AddComponent<Vrm10SimpleStageMotion>();
            }

            player.onLipSyncUpdate.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(player.onLipSyncUpdate, driver.OnLipSyncUpdate);
            EditorUtility.SetDirty(avatar);

            var prefab = PrefabUtility.SaveAsPrefabAsset(avatar, prefabPath);
            Object.DestroyImmediate(avatar);
            return prefab;
        }

        static void BuildScene(GameObject avatarPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab, scene);
            avatar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var lightObject = new GameObject("Key Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            ConfigureCamera(camera);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.2f, 0.22f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static TimelineAsset BuildTimeline(AudioClip audioClip, BakedData bakedData)
        {
            if (File.Exists(TimelinePath))
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TestVoiceSequence";
            timeline.editorSettings.frameRate = 30d;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Mathf.Min(audioClip.length, 5f);
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var audioTrack = timeline.CreateTrack<AudioTrack>("TestVoice Audio");
            var audioTimelineClip = audioTrack.CreateClip(audioClip);
            audioTimelineClip.start = 0d;
            audioTimelineClip.duration = timeline.fixedDuration;
            audioTimelineClip.displayName = audioClip.name;

            var lipSyncTrack = timeline.CreateTrack<uLipSyncTrack>("TestVoice LipSync");
            var lipSyncTimelineClip = lipSyncTrack.CreateClip<uLipSyncClip>();
            lipSyncTimelineClip.start = 0d;
            lipSyncTimelineClip.duration = timeline.fixedDuration;
            lipSyncTimelineClip.displayName = "Baked LipSync";

            var lipSyncClip = (uLipSyncClip)lipSyncTimelineClip.asset;
            lipSyncClip.bakedData = bakedData;
            lipSyncClip.volume = 1f;
            lipSyncClip.timeOffset = 0.05f;

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        static TimelineAsset BuildScenarioTimeline(
            LipSyncScenarioLine scenario,
            AudioClip audioClip,
            BakedData bakedData)
        {
            if (File.Exists(ScenarioTimelinePath))
            {
                AssetDatabase.DeleteAsset(ScenarioTimelinePath);
            }

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = scenario.name + "Sequence";
            timeline.editorSettings.frameRate = 30d;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Mathf.Min(audioClip.length, 5f);
            AssetDatabase.CreateAsset(timeline, ScenarioTimelinePath);

            var audioTrack = timeline.CreateTrack<AudioTrack>(scenario.name + " Audio");
            var audioTimelineClip = audioTrack.CreateClip(audioClip);
            audioTimelineClip.start = 0d;
            audioTimelineClip.duration = timeline.fixedDuration;
            audioTimelineClip.displayName = $"{scenario.SpeakerName}: {scenario.LineText}";

            var lipSyncTrack = timeline.CreateTrack<uLipSyncTrack>(scenario.name + " LipSync");
            var lipSyncTimelineClip = lipSyncTrack.CreateClip<uLipSyncClip>();
            lipSyncTimelineClip.start = 0d;
            lipSyncTimelineClip.duration = timeline.fixedDuration;
            lipSyncTimelineClip.displayName = scenario.name + " Baked LipSync";

            var lipSyncClip = (uLipSyncClip)lipSyncTimelineClip.asset;
            lipSyncClip.bakedData = bakedData;
            lipSyncClip.volume = 1f;
            lipSyncClip.timeOffset = 0.05f;

            var cameraTrack = timeline.CreateTrack<ActivationTrack>(scenario.name + " Camera");
            var cameraClip = cameraTrack.CreateDefaultClip();
            cameraClip.start = 0d;
            cameraClip.duration = timeline.fixedDuration;
            cameraClip.displayName = scenario.name + " Camera";

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        static void BuildRecorderPreset()
        {
            BuildRecorderPreset(RecorderPresetPath, "TestVoice Movie Recorder", RecorderOutputPath);
        }

        static void BuildRecorderPreset(string presetPath, string recorderName, string outputPath)
        {
            if (File.Exists(presetPath))
            {
                AssetDatabase.DeleteAsset(presetPath);
            }

            var recorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            try
            {
                TimelineRecorderBatchRunner.ConfigureMovieRecorderSettings(
                    recorder,
                    recorderName,
                    outputPath);

                var preset = new Preset(recorder);
                AssetDatabase.CreateAsset(preset, presetPath);
            }
            finally
            {
                Object.DestroyImmediate(recorder);
            }
        }

        static void BuildRecorderScene(GameObject avatarPrefab, TimelineAsset timeline, float audioLength)
        {
            BuildRecorderScene(
                avatarPrefab,
                timeline,
                audioLength,
                RecorderScenePath,
                RecorderOutputPath,
                RecorderPresetPath);
        }

        static void BuildRecorderScene(
            GameObject avatarPrefab,
            TimelineAsset timeline,
            float audioLength,
            string recorderScenePath,
            string recorderOutputPath,
            string recorderPresetPath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab, scene);
            avatar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var audioSource = avatar.GetComponent<AudioSource>();
            if (!audioSource) audioSource = avatar.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var bakedPlayer = avatar.GetComponent<uLipSyncBakedDataPlayer>();
            if (bakedPlayer)
            {
                bakedPlayer.playOnAwake = false;
                bakedPlayer.playAudioSource = false;
            }

            var driver = avatar.GetComponent<Vrm10BakedLipSyncDriver>();
            if (!driver) driver = avatar.AddComponent<Vrm10BakedLipSyncDriver>();

            var timelineEvent = avatar.GetComponent<uLipSyncTimelineEvent>();
            if (!timelineEvent) timelineEvent = avatar.AddComponent<uLipSyncTimelineEvent>();
            timelineEvent.onLipSyncUpdate.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(timelineEvent.onLipSyncUpdate, driver.OnLipSyncUpdate);

            var lightObject = new GameObject("Key Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            ConfigureCamera(camera);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.2f, 0.22f);

            var directorObject = new GameObject("Timeline Director");
            SceneManager.MoveGameObjectToScene(directorObject, scene);
            var director = directorObject.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.initialTime = 0d;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is AudioTrack)
                {
                    director.SetGenericBinding(track, audioSource);
                }
                else if (track is uLipSyncTrack)
                {
                    director.SetGenericBinding(track, timelineEvent);
                }
                else if (track is ActivationTrack)
                {
                    director.SetGenericBinding(track, cameraObject);
                }
            }

            var runner = directorObject.AddComponent<TimelineRecorderBatchRunner>();
            runner.Configure(Mathf.Min(audioLength, 5f), recorderOutputPath, recorderPresetPath);

            EditorSceneManager.SaveScene(scene, recorderScenePath);
        }

        static void ConfigureCamera(Camera camera)
        {
            camera.transform.position = CameraPosition;
            camera.transform.rotation = Quaternion.LookRotation(CameraTarget - camera.transform.position);
            camera.fieldOfView = CameraFieldOfView;
            camera.nearClipPlane = CameraNearClipPlane;
        }

        static bool HasClipWithAsset<T>(TrackAsset track) where T : Object
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is T) return true;
            }

            return false;
        }

        static bool HasLipSyncClip(TrackAsset track, BakedData bakedData)
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is uLipSyncClip lipSyncClip && lipSyncClip.bakedData == bakedData)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasAudioClip(AudioTrack track, AudioClip audioClip)
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is AudioPlayableAsset playableAsset && playableAsset.clip == audioClip)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasAnyClip(TrackAsset track)
        {
            foreach (var unused in track.GetClips())
            {
                return true;
            }

            return false;
        }

        static void ValidateScenarioFields(LipSyncScenarioLine scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario.SpeakerName))
            {
                throw new InvalidDataException("Scenario speaker name is empty.");
            }

            if (string.IsNullOrWhiteSpace(scenario.LineText))
            {
                throw new InvalidDataException("Scenario line text is empty.");
            }

            if (string.IsNullOrWhiteSpace(scenario.WavAssetPath) ||
                !scenario.WavAssetPath.StartsWith("Assets/") ||
                !string.Equals(Path.GetExtension(scenario.WavAssetPath), ".wav", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Scenario WAV asset path must point to a .wav file below Assets/.");
            }
        }

        static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset)
            {
                throw new FileNotFoundException($"Required asset was not found or not imported as {typeof(T).Name}.", path);
            }
            return asset;
        }
    }
}
