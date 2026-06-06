using System.Collections.Generic;
using System.IO;
using UniVRM10;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using uLipSync;

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
        const string PackageProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset";

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

        public static void BuildFromBatch()
        {
            Build();
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
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceSynchronousImport);
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
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.25f, -2.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.15f, 0f) - camera.transform.position);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.2f, 0.22f);

            EditorSceneManager.SaveScene(scene, ScenePath);
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
