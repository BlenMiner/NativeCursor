using UnityEngine;

namespace Riten.Native.Cursors.Virtual
{
    public class AutoSetCursorPack : MonoBehaviour
    {
        [SerializeField] CursorPack _cursorPack;
        [SerializeField] Camera _camera;
        [SerializeField] MaskCursorMode _maskCursorMode = MaskCursorMode.LiveInverted;
        [SerializeField, Min(1)] int _liveMaskInversionUpdatesPerSecond = 30;

        private CursorPack _lastActivated;

        private void OnEnable()
        {
            ApplyCursorPack();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !enabled) return;

            ApplyCursorPack();
        }

        private void OnDisable()
        {
            if (_lastActivated)
            {
                NativeCursor.ClearCursorPack();
                _lastActivated = null;
            }
        }

        private void ApplyMaskSettings()
        {
            VirtualCursorService.maskCursorMode = _maskCursorMode;
            VirtualCursorService.liveMaskInversionUpdatesPerSecond = Mathf.Max(1, _liveMaskInversionUpdatesPerSecond);
        }

        private void ApplyCursorPack()
        {
            ApplyMaskSettings();

            if (!_cursorPack)
            {
                if (_lastActivated)
                {
                    NativeCursor.ClearCursorPack();
                    _lastActivated = null;
                }

                return;
            }

            if (_lastActivated != _cursorPack)
            {
                NativeCursor.SetCursorPack(_cursorPack, _camera);
                _lastActivated = _cursorPack;
            }
            else
            {
                NativeCursor.SetCursorPackCamera(_camera);
            }

            NativeCursor.SetCursor(NTCursors.Arrow);
        }
    }
}
