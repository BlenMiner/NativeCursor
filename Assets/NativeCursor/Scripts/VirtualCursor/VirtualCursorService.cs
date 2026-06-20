using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Riten.Native.Cursors.Virtual
{
    public enum MaskCursorMode
    {
        Stable,
        LiveInverted
    }

    public class VirtualCursorService : MonoBehaviour, ICursorService
    {
        public static MaskCursorMode maskCursorMode = MaskCursorMode.LiveInverted;
        public static int liveMaskInversionUpdatesPerSecond = 30;
        private const byte InvertedMaskAlpha = 1;

        private CursorPack _cursorPack;
        
        private VirtualCursorBase _activeCursor;
        private Camera _camera;

        private int _lastFrame;
        private int _frame;
        private float _fps;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void OnEnable()
        {
            Camera.onPostRender += OnPostRenderCb;
        }
        
        private void OnDisable()
        {
            Camera.onPostRender -= OnPostRenderCb;
        }

        private void OnPostRenderCb(Camera cmr)
        {
            if (!Application.isPlaying) return;
            
            if (_camera && cmr != _camera) return;

            if (!_camera)
                _camera = cmr;

            if (!_activeCursor || !_activeCursor.isMask) return;

            if (maskCursorMode != MaskCursorMode.LiveInverted) return;

            DoLiveInvertedMaskCursorUpdate();
        }

        public void UpdatePack(CursorPack pack, Camera cmr)
        {
            if (cmr) _camera = cmr;
            _cursorPack = pack;
        }
        
        public bool SetCursor(NTCursors ntCursor)
        {
            if (!_cursorPack)
            {
                ClearUnityCursor();
                return false;
            }

            var requestedCursor = _cursorPack.GetCursor(ntCursor);
            var foundRequestedCursor = requestedCursor;

            if (!requestedCursor && ntCursor != NTCursors.Arrow && ntCursor != NTCursors.Default)
                requestedCursor = _cursorPack.GetCursor(NTCursors.Arrow);

            _activeCursor = requestedCursor;

            if (!_activeCursor)
            {
                ClearUnityCursor();
                return ntCursor == NTCursors.Default;
            }
            
            _frame = 0;
            _lastFrame = -1;
            _fps = Mathf.Abs(_activeCursor.framesPerSecond == 0 ? 0 : 1f / _activeCursor.framesPerSecond);

            DoCursorUpdate();
            return foundRequestedCursor;
        }

        public void ResetCursor()
        {
            if (!SetCursor(NTCursors.Arrow))
                ClearUnityCursor();
        }

        void Update()
        {
            var frames = _activeCursor ? _activeCursor.frames : null;

            if (!_activeCursor || !_activeCursor.isAnimated || frames == null || frames.Length == 0)
                return;
            
            _frame = Mathf.FloorToInt(_fps == 0f ? 0 : Time.time / _fps) % frames.Length;

            if (_lastFrame != _frame)
            {
                _lastFrame = _frame;
                DoCursorUpdate();
            }
        }

        private void DoCursorUpdate()
        {
            if (!_activeCursor) return;

            if (_activeCursor.isMask)
            {
                if (maskCursorMode == MaskCursorMode.LiveInverted)
                {
                    _nextLiveMaskUpdateTime = 0f;
                    SetStableMaskCursor();
                }
                else
                    SetStableMaskCursor();

                return;
            }

            var frames = _activeCursor.frames;

            if (_activeCursor.isAnimated && frames != null && frames.Length > 0)
            {
                var cursor = frames[_frame];
                var texture = cursor ? cursor.texture : null;

                if (!texture)
                {
                    ClearUnityCursor();
                    return;
                }
                
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

                if (!texture)
                {
                    ClearUnityCursor();
                    return;
                }
                
                Cursor.SetCursor(
                    _activeCursor.texture,
                    new Vector2(texture.width, texture.height) * _activeCursor.hotspot,
                    CursorMode.Auto
                );
            }
        }

        private Texture2D _screenTexture;
        private Texture2D _liveMaskTexture;
        private Texture2D _stableMaskTexture;
        private Texture2D _stableMaskSourceTexture;
        private Texture2D _liveMaskSourceTexture;
        private VirtualCursorBase _stableMaskCursor;
        private Color32[] _stableMaskPixels;
        private Color32[] _liveMaskSourcePixels;
        private Color32[] _liveMaskPixels;
        private bool _liveMaskHasExplicitInvertedPixels;
        private float _nextLiveMaskUpdateTime;

        private bool CaptureScreen()
        {
            _screenTexture ??= new Texture2D(_liveMaskTexture.width, _liveMaskTexture.height, TextureFormat.RGBA32, false);
            
            if (_screenTexture.width != _liveMaskTexture.width || _screenTexture.height != _liveMaskTexture.height)
                _screenTexture.Reinitialize(_liveMaskTexture.width, _liveMaskTexture.height, TextureFormat.RGBA32, false);

            if (!TryGetMousePosition(out var pos))
                return false;

            var hot = _activeCursor.hotspot * new Vector2(_liveMaskTexture.width, _liveMaskTexture.height);
            var region = new Rect(
                pos.x - hot.x,
                pos.y + hot.y - _screenTexture.height,
                _screenTexture.width,
                _screenTexture.height
            );
            
            if (region.x + region.width > Screen.width)
                region.x = Screen.width - region.width;
            
            if (region.y + region.height > Screen.height)
                region.y = Screen.height - region.height;
            
            if (region.x < 0)
                region.x = 0;
            
            if (region.y < 0)
                region.y = 0;
            
            _screenTexture.ReadPixels(region, 0, 0, false);
            _screenTexture.Apply();
            return true;
        }

        private void SetStableMaskCursor()
        {
            var texture = _activeCursor.texture;

            if (!texture)
            {
                ClearUnityCursor();
                return;
            }

            if (!_stableMaskTexture)
            {
                _stableMaskTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            else if (_stableMaskTexture.width != texture.width || _stableMaskTexture.height != texture.height)
            {
                _stableMaskTexture.Reinitialize(texture.width, texture.height, TextureFormat.RGBA32, false);
                _stableMaskCursor = null;
                _stableMaskSourceTexture = null;
            }

            if (_stableMaskCursor != _activeCursor || _stableMaskSourceTexture != texture)
            {
                BuildStableMaskTexture(texture);
                _stableMaskCursor = _activeCursor;
                _stableMaskSourceTexture = texture;
            }

            Cursor.SetCursor(
                _stableMaskTexture,
                new Vector2(
                    _activeCursor.hotspot.x * _stableMaskTexture.width,
                    _activeCursor.hotspot.y * _stableMaskTexture.height
                ),
                CursorMode.ForceSoftware
            );
        }

        private void BuildStableMaskTexture(Texture2D texture)
        {
            var source = texture.GetPixels32();
            var pixelCount = source.Length;

            if (_stableMaskPixels == null || _stableMaskPixels.Length != pixelCount)
                _stableMaskPixels = new Color32[pixelCount];
            else
                System.Array.Clear(_stableMaskPixels, 0, _stableMaskPixels.Length);

            var hasExplicitInvertedPixels = HasExplicitInvertedMaskPixels(source);
            var invertedFallback = EnsureOpaque(_activeCursor.backgroundColor, new Color32(0, 0, 0, 255));
            var foreground = EnsureOpaque(_activeCursor.foregroundColor, new Color32(255, 255, 255, 255));

            for (var i = 0; i < pixelCount; ++i)
            {
                var pixel = source[i];

                if (pixel.a == 0)
                    continue;

                if (IsInvertedMaskPixel(pixel))
                {
                    _stableMaskPixels[i] = invertedFallback;
                }
                else if (!hasExplicitInvertedPixels)
                {
                    _stableMaskPixels[i] = foreground;
                }
                else
                {
                    pixel.a = 255;
                    _stableMaskPixels[i] = pixel;
                }
            }

            _stableMaskTexture.SetPixels32(_stableMaskPixels);
            _stableMaskTexture.Apply();
        }

        private void DoLiveInvertedMaskCursorUpdate()
        {
            var updatesPerSecond = Mathf.Max(1, liveMaskInversionUpdatesPerSecond);

            if (Time.unscaledTime < _nextLiveMaskUpdateTime) return;

            _nextLiveMaskUpdateTime = Time.unscaledTime + 1f / updatesPerSecond;

            var texture = _activeCursor.texture;

            if (!texture)
            {
                ClearUnityCursor();
                return;
            }

            var pixelCount = texture.width * texture.height;

            if (!_liveMaskTexture)
            {
                _liveMaskTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            else if (_liveMaskTexture.width != texture.width || _liveMaskTexture.height != texture.height)
            {
                _liveMaskTexture.Reinitialize(texture.width, texture.height, TextureFormat.RGBA32, false);
            }

            if (_liveMaskSourceTexture != texture || _liveMaskSourcePixels == null || _liveMaskSourcePixels.Length != pixelCount)
            {
                _liveMaskSourceTexture = texture;
                _liveMaskSourcePixels = texture.GetPixels32();
                _liveMaskHasExplicitInvertedPixels = HasExplicitInvertedMaskPixels(_liveMaskSourcePixels);
            }

            if (_liveMaskPixels == null || _liveMaskPixels.Length != pixelCount)
                _liveMaskPixels = new Color32[pixelCount];

            if (!CaptureScreen())
            {
                SetStableMaskCursor();
                return;
            }
            
            var screen = _screenTexture.GetPixels32();

            for (var i = 0; i < pixelCount; i++)
            {
                var pixel = _liveMaskSourcePixels[i];

                if (IsInvertedMaskPixel(pixel) || (pixel.a != 0 && !_liveMaskHasExplicitInvertedPixels))
                {
                    var screenPixel = screen[i];
                    screenPixel.r = (byte)(255 - screenPixel.r);
                    screenPixel.g = (byte)(255 - screenPixel.g);
                    screenPixel.b = (byte)(255 - screenPixel.b);
                    screenPixel.a = 255;
                    _liveMaskPixels[i] = screenPixel;
                }
                else
                {
                    if (pixel.a != 0)
                        pixel.a = 255;

                    _liveMaskPixels[i] = pixel;
                }
            }
            
            _liveMaskTexture.SetPixels32(_liveMaskPixels);
            _liveMaskTexture.Apply();
            
            Cursor.SetCursor(
                _liveMaskTexture,
                new Vector2(
                    _activeCursor.hotspot.x * _liveMaskTexture.width,
                    _activeCursor.hotspot.y * _liveMaskTexture.height
                ),
                CursorMode.ForceSoftware
            );
        }

        private static Color32 EnsureOpaque(Color32 color, Color32 fallback)
        {
            if (color.a == 0)
                color = fallback;

            color.a = 255;
            return color;
        }

        private static bool HasExplicitInvertedMaskPixels(Color32[] pixels)
        {
            for (var i = 0; i < pixels.Length; ++i)
            {
                if (IsInvertedMaskPixel(pixels[i]))
                    return true;
            }

            return false;
        }

        private static bool IsInvertedMaskPixel(Color32 pixel)
        {
            return pixel.a == InvertedMaskAlpha;
        }

        private static bool TryGetMousePosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;

            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            position = UnityEngine.Input.mousePosition;
            return true;
#else
            position = default;
            return false;
#endif
        }

        public void SetCamera(Camera cmr)
        {
            _camera = cmr;
        }

        private void ClearUnityCursor()
        {
            _activeCursor = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
