using UnityEngine;

namespace Riten.Native.Cursors.Virtual
{
    public class AutoSetCursorPack : MonoBehaviour
    {
        [SerializeField] CursorPack _cursorPack;
        [SerializeField] Camera _camera;
        [SerializeField] MaskCursorMode _maskCursorMode = MaskCursorMode.Stable;
        [SerializeField, Min(1)] int _liveMaskInversionUpdatesPerSecond = 30;

        private CursorPack _lastActivated;

        private void OnEnable()
        {
            if (_cursorPack == null)
                return;
            
            ApplyMaskSettings();
            NativeCursor.SetCursorPack(_cursorPack, _camera);
            NativeCursor.SetCursor(NTCursors.Arrow);
            
            _lastActivated = _cursorPack;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !enabled) return;

            ApplyMaskSettings();
            
            if (_lastActivated != null && _lastActivated != _cursorPack)
            {
                NativeCursor.SetCursorPack(_cursorPack, _camera);
                NativeCursor.SetCursor(NTCursors.Arrow);
                _lastActivated = _cursorPack;
            }
            else if (_lastActivated != null)
            {
                NativeCursor.SetCursor(NTCursors.Arrow);
            }
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
    }
}
