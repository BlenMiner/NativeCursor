using UnityEngine;

namespace Riten.Native.Cursors.Virtual
{
    public class VirtualCursorService : MonoBehaviour, ICursorService
    {
        private CursorPack _cursorPack;
        
        private VirtualCursorBase _activeCursor;

        private int _frame;
        private float _fps;
        
        public void UpdatePack(CursorPack pack)
        {
            _cursorPack = pack;
        }
        
        public bool SetCursor(NTCursors ntCursor)
        {
            _activeCursor = _cursorPack.GetCursor(ntCursor);

            if (_activeCursor == null)
                return false;
            
            _frame = 0;
            _timer = 0;
            _fps = _activeCursor.framesPerSecond == 0 ? 0 : 1f / _activeCursor.framesPerSecond;

            DoCursorUpdate();
            return true;
        }

        public void ResetCursor()
        {
            _activeCursor = _cursorPack.GetCursor(NTCursors.Arrow);
            _frame = 0;
            _timer = 0;
            _fps = !_activeCursor || _activeCursor.framesPerSecond == 0 ? 1f / _activeCursor.framesPerSecond : 0;
            DoCursorUpdate();
        }

        private float _timer;

        private void Update()
        {
            if (!_activeCursor || !_activeCursor.isAnimated || _activeCursor.frames.Length == 0) return;
            
            if (_timer < _fps)
            {
                _timer += Time.deltaTime;
                return;
            }
            
            _frame = (_frame + 1) % _activeCursor.frames.Length;
            _timer = 0;

            DoCursorUpdate();
        }

        private void DoCursorUpdate()
        {
            if (!_activeCursor) return;

            if (_activeCursor.isAnimated && _activeCursor.frames.Length > 0)
            {
                var cursor = _activeCursor.frames[_frame];
                var texture = cursor.texture;
                
                Cursor.SetCursor(
                    texture,
                    new Vector2(
                        cursor.hotspot.x * texture.width, 
                        cursor.hotspot.y * texture.height
                    ),
                    CursorMode.Auto
                );
            }
            else
            {
                var texture = _activeCursor.texture;
                
                Cursor.SetCursor(
                    _activeCursor.texture,
                    new Vector2(texture.width, texture.height) * _activeCursor.hotspot,
                    CursorMode.Auto
                );
            }
        }
    }
}