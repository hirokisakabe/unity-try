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
        const string PackageProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset";
        // UniVRM importer serialized enum: ImporterRenderPipelineTypes.UniversalRenderPipeline.
        const int UniversalRenderPipelineImporterValue = 2;
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

        public static void BuildFromBatch()
        {
            Build();
        }

        public static void BuildIssue5FromBatch()
        {
            BuildIssue5();
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

            if (Mathf.Abs(camera.fieldOfView - 30f) > 0.1f)
            {
                throw new InvalidDataException("Recorder scene camera does not use the close-up framing field of view.");
            }

            var importer = AssetImporter.GetAtPath(ModelPath) ??
                throw new InvalidDataException("VRM importer is missing.");
            var renderPipeline = new SerializedObject(importer).FindProperty("RenderPipeline") ??
                throw new InvalidDataException("VRM importer has no RenderPipeline property.");
            if (renderPipeline.intValue != UniversalRenderPipelineImporterValue)
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

            var serializedImporter = new SerializedObject(importer);
            var renderPipeline = serializedImporter.FindProperty("RenderPipeline");
            if (renderPipeline == null || renderPipeline.intValue == UniversalRenderPipelineImporterValue) return;

            renderPipeline.intValue = UniversalRenderPipelineImporterValue;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
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
            var bakedData = AssetDatabase.LoadAssetAtPath<BakedData>(BakedDataPath);
            if (!bakedData)
            {
                bakedData = ScriptableObject.CreateInstance<BakedData>();
                AssetDatabase.CreateAsset(bakedData, BakedDataPath);
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
            var modelPrefab = LoadRequired<GameObject>(ModelPath);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            avatar.name = "BakedLipSyncAvatar";
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

            var prefab = PrefabUtility.SaveAsPrefabAsset(avatar, PrefabPath);
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

        static void BuildRecorderPreset()
        {
            if (File.Exists(RecorderPresetPath))
            {
                AssetDatabase.DeleteAsset(RecorderPresetPath);
            }

            var recorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            try
            {
                TimelineRecorderBatchRunner.ConfigureMovieRecorderSettings(
                    recorder,
                    "TestVoice Movie Recorder",
                    RecorderOutputPath);

                var preset = new Preset(recorder);
                AssetDatabase.CreateAsset(preset, RecorderPresetPath);
            }
            finally
            {
                Object.DestroyImmediate(recorder);
            }
        }

        static void BuildRecorderScene(GameObject avatarPrefab, TimelineAsset timeline, float audioLength)
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
            }

            var runner = directorObject.AddComponent<TimelineRecorderBatchRunner>();
            runner.Configure(Mathf.Min(audioLength, 5f), RecorderOutputPath, RecorderPresetPath);

            EditorSceneManager.SaveScene(scene, RecorderScenePath);
        }

        static void ConfigureCamera(Camera camera)
        {
            camera.transform.position = CameraPosition;
            camera.transform.rotation = Quaternion.LookRotation(CameraTarget - camera.transform.position);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
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
