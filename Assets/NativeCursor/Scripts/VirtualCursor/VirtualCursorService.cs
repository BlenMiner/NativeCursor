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
        private readonly WaitForEndOfFrame frameEnd = new ();

        void Update()
        {
            /*if (_activeCursor && _activeCursor.isMask)
            {
                DoMaskedPostProcess();
                continue;
            }*/

            if (!_activeCursor || !_activeCursor.isAnimated || _activeCursor.frames.Length == 0) 
                return;
        
            if (_timer < _fps)
            {
                _timer += Time.deltaTime;
                return;
            }
        
            _frame = (_frame + 1) % _activeCursor.frames.Length;
            _timer = 0;

            DoCursorUpdate(true);
        }

        private void DoCursorUpdate(bool pp = false)
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
            
            /*if (pp && _activeCursor.isMask)
                DoMaskedPostProcess();*/
        }
        
        /*private void CaptureScreen()
        {
            _screenTexture ??= new Texture2D(_maskTexture.width, _maskTexture.height, TextureFormat.RGBA32, false);
            
            if (_screenTexture.width != _maskTexture.width || _screenTexture.height != _maskTexture.height)
                _screenTexture.Reinitialize(_maskTexture.width, _maskTexture.height, TextureFormat.RGBA32, false);

            var pos = Input.mousePosition;
            var hot = _activeCursor.hotspot * new Vector2(_maskTexture.width, _maskTexture.height);
            var region = new Rect(pos.x - hot.x, pos.y - hot.y, _screenTexture.width, _screenTexture.height);
            
            _screenTexture.ReadPixels(region, 0, 0, false);
            _screenTexture.Apply();
        }

        private void DoMaskedPostProcess()
        {
            var texture = _activeCursor.texture;
            var pixels = texture.GetPixels32();

            if (!_maskTexture)
            {
                _maskTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true
                };
            }
            else if (_maskTexture.width != texture.width || _maskTexture.height != texture.height)
            {
                _maskTexture.Reinitialize(texture.width, texture.height, TextureFormat.RGBA32, false);
                _maskTexture.alphaIsTransparency = true;
            }
            
            CaptureScreen();
            
            var screen = _screenTexture.GetPixels32();

            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];

                if (pixel.a == 255)
                {
                    var screenPixel = screen[i];
                    screenPixel.r = (byte)(255 - screenPixel.r);
                    screenPixel.g = (byte)(255 - screenPixel.g);
                    screenPixel.b = (byte)(255 - screenPixel.b);
                    pixels[i] = screenPixel;
                }
            }
            
            _maskTexture.SetPixels32(pixels);
            _maskTexture.Apply();
            
            Cursor.SetCursor(
                _maskTexture,
                new Vector2(
                    _activeCursor.hotspot.x * _maskTexture.width, 
                    _activeCursor.hotspot.y * _maskTexture.height
                ),
                CursorMode.ForceSoftware
            );
        }*/
    }
}