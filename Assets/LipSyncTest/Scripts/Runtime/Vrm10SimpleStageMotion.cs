using UniVRM10;
using UnityEngine;

namespace UnityTry.LipSyncTest
{
    [DisallowMultipleComponent]
    public sealed class Vrm10SimpleStageMotion : MonoBehaviour
    {
        [SerializeField] Vrm10Instance vrm;
        [SerializeField] Animator animator;
        [SerializeField, Min(0f)] float rootSway = 0.015f;
        [SerializeField, Min(0f)] float rootYaw = 2f;
        [SerializeField, Min(0f)] float headYaw = 5f;
        [SerializeField, Min(0f)] float headPitch = 2f;

        Transform root;
        Transform head;
        Transform chest;
        Vector3 initialRootPosition;
        Quaternion initialRootRotation;
        Quaternion initialHeadRotation;
        Quaternion initialChestRotation;
        bool hasPose;

        void Reset()
        {
            vrm = GetComponent<Vrm10Instance>();
            animator = GetComponentInChildren<Animator>();
        }

        void Awake()
        {
            CapturePose();
        }

        void OnEnable()
        {
            CapturePose();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!hasPose) CapturePose();
            if (!hasPose) return;

            var t = Time.time;
            root.localPosition = initialRootPosition + new Vector3(
                Mathf.Sin(t * 1.1f) * rootSway,
                Mathf.Sin(t * 1.7f) * rootSway * 0.5f,
                0f);
            root.localRotation = initialRootRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.9f) * rootYaw, 0f);

            if (chest)
            {
                chest.localRotation = initialChestRotation * Quaternion.Euler(
                    Mathf.Sin(t * 1.2f) * 0.8f,
                    Mathf.Sin(t * 0.8f) * 1.2f,
                    0f);
            }

            if (head)
            {
                head.localRotation = initialHeadRotation * Quaternion.Euler(
                    Mathf.Sin(t * 1.5f) * headPitch,
                    Mathf.Sin(t * 1.05f) * headYaw,
                    Mathf.Sin(t * 1.3f) * 1.2f);
            }
        }

        void OnDisable()
        {
            RestorePose();
        }

        void CapturePose()
        {
            if (!vrm) vrm = GetComponent<Vrm10Instance>();
            if (!animator) animator = GetComponentInChildren<Animator>();

            root = vrm ? vrm.transform : transform;
            head = animator ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            chest = animator ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;

            if (!root) return;

            initialRootPosition = root.localPosition;
            initialRootRotation = root.localRotation;
            initialHeadRotation = head ? head.localRotation : Quaternion.identity;
            initialChestRotation = chest ? chest.localRotation : Quaternion.identity;
            hasPose = true;
        }

        void RestorePose()
        {
            if (!hasPose || !Application.isPlaying) return;

            root.localPosition = initialRootPosition;
            root.localRotation = initialRootRotation;
            if (head) head.localRotation = initialHeadRotation;
            if (chest) chest.localRotation = initialChestRotation;
        }
    }
}
