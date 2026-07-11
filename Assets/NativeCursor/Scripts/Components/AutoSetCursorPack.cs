using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Riten.Native.Cursors.Virtual
{
    public class AutoSetCursorPack : MonoBehaviour
    {
        private static readonly List<AutoSetCursorPack> Providers = new();

        [Tooltip("When enabled, this component switches NativeCursor to the virtual cursor pack service. Disable it when player builds should keep using their platform-native service.")]
        [SerializeField] bool _useVirtualCursorPack = true;
        [SerializeField] CursorPack _cursorPack;
        [SerializeField] Camera _camera;
        [SerializeField] MaskCursorMode _maskCursorMode = MaskCursorMode.LiveInverted;
        [SerializeField, Min(1)] int _liveMaskInversionUpdatesPerSecond = 30;

#if UNITY_EDITOR
        private bool _applyCursorPackQueued;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Providers.Clear();
        }

        private void OnEnable()
        {
            RegisterProvider(this);

            if (WantsVirtualCursorPack)
                ActivateAsOwner();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _liveMaskInversionUpdatesPerSecond = Mathf.Max(1, _liveMaskInversionUpdatesPerSecond);

            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            QueueApplyCursorPack();
        }
#endif

        private void OnDisable()
        {
#if UNITY_EDITOR
            ClearQueuedApplyCursorPack();
#endif

            UnregisterProvider(this);

            if (NativeCursor.IsCursorPackOwner(this))
                ActivateFallbackOrClear(this);
        }

        private bool WantsVirtualCursorPack => isActiveAndEnabled && _useVirtualCursorPack && _cursorPack;

        private void ApplyMaskSettings()
        {
            VirtualCursorService.maskCursorMode = _maskCursorMode;
            VirtualCursorService.liveMaskInversionUpdatesPerSecond = Mathf.Max(1, _liveMaskInversionUpdatesPerSecond);
        }

        private void ApplyCursorPack()
        {
            RegisterProvider(this);

            if (!WantsVirtualCursorPack)
            {
                if (NativeCursor.IsCursorPackOwner(this))
                    ActivateFallbackOrClear(this);

                return;
            }

            var preferredProvider = FindLastEligibleProvider();
            var owner = NativeCursor.CursorPackOwner;

            if (NativeCursor.IsCursorPackOwner(this) ||
                (preferredProvider == this && (owner == null || owner is AutoSetCursorPack)))
            {
                ActivateAsOwner();
            }
        }

        private void ActivateAsOwner()
        {
            if (!WantsVirtualCursorPack)
                return;

            ApplyMaskSettings();
            NativeCursor.SetCursorPack(_cursorPack, _camera, this);
        }

        private static void ActivateFallbackOrClear(AutoSetCursorPack owner)
        {
            var fallback = FindLastEligibleProvider();

            if (fallback)
                fallback.ActivateAsOwner();
            else
                NativeCursor.ClearCursorPack(owner);
        }

        private static AutoSetCursorPack FindLastEligibleProvider()
        {
            RemoveDestroyedProviders();

            for (var i = Providers.Count - 1; i >= 0; --i)
            {
                if (Providers[i].WantsVirtualCursorPack)
                    return Providers[i];
            }

            return null;
        }

        private static void RegisterProvider(AutoSetCursorPack provider)
        {
            RemoveDestroyedProviders();

            if (!Providers.Contains(provider))
                Providers.Add(provider);
        }

        private static void UnregisterProvider(AutoSetCursorPack provider)
        {
            for (var i = Providers.Count - 1; i >= 0; --i)
            {
                if (!Providers[i] || Providers[i] == provider)
                    Providers.RemoveAt(i);
            }
        }

        private static void RemoveDestroyedProviders()
        {
            for (var i = Providers.Count - 1; i >= 0; --i)
            {
                if (!Providers[i])
                    Providers.RemoveAt(i);
            }
        }

#if UNITY_EDITOR
        private void QueueApplyCursorPack()
        {
            if (_applyCursorPackQueued)
                return;

            _applyCursorPackQueued = true;
            EditorApplication.delayCall += ApplyCursorPackFromEditorDelay;
        }

        private void ClearQueuedApplyCursorPack()
        {
            if (!_applyCursorPackQueued)
                return;

            EditorApplication.delayCall -= ApplyCursorPackFromEditorDelay;
            _applyCursorPackQueued = false;
        }

        private void ApplyCursorPackFromEditorDelay()
        {
            ClearQueuedApplyCursorPack();

            if (!this || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ApplyCursorPack();
        }
#endif
    }
}
