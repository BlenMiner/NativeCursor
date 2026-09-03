using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riten.Native.Cursors.Editor
{
    /// <summary>
    /// Editor preview of native cursors.
    ///
    /// The platform services are compiled out of the Editor, so this service maps <see cref="NTCursors"/>
    /// to the Editor's own <see cref="MouseCursor"/> set and registers a cursor rect over the game area
    /// of every open Game view through an overlay <see cref="IMGUIContainer"/>. The overlay ignores
    /// picking, so game input is unaffected. Cursors the Editor cannot show use the closest available
    /// shape; treat this as a preview and validate final behaviour in a player build.
    /// </summary>
    public sealed class EditorCursorService : ICursorService, ICursorServiceLifecycle
    {
        private const string OverlayName = "NativeCursor#EditorCursorOverlay";
        private const double WindowScanInterval = 0.5;

        private static readonly Type GameViewType = Type.GetType("UnityEditor.GameView, UnityEditor.CoreModule")
                                                    ?? Type.GetType("UnityEditor.GameView, UnityEditor");

        // Rect of the rendered game area inside the window, when the Editor exposes it. Internal API, optional.
        private static readonly PropertyInfo ViewInWindowProperty = GameViewType?.GetProperty(
            "viewInWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static EditorCursorService _instance;

        private readonly List<Overlay> _overlays = new();
        private MouseCursor? _cursor;
        private bool _serviceActive;
        private double _nextWindowScanTime;

        private sealed class Overlay
        {
            public EditorWindow window;
            public IMGUIContainer container;
        }

        [InitializeOnLoadMethod]
        private static void InstallInEditMode()
        {
            Install();
        }

        // Runs after NativeCursor resets its static state when entering play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallInPlayMode()
        {
            Install();
        }

        private static void Install()
        {
            if (GameViewType == null)
                return;

            _instance ??= new EditorCursorService();

            NativeCursor.SetFallbackService(_instance);
            NativeCursor.SetService(_instance);
        }

        private void OnEditorUpdate()
        {
            if (!_serviceActive)
                return;

            if (EditorApplication.timeSinceStartup >= _nextWindowScanTime)
            {
                _nextWindowScanTime = EditorApplication.timeSinceStartup + WindowScanInterval;
                RefreshOverlays();
            }

            if (_cursor == null)
                return;

            // Cursor rects only live until the view's next repaint, and a Game view repaint does not
            // re-run our container on its own. Keep the overlay repainting while a cursor is active.
            foreach (var overlay in _overlays)
                overlay.container.MarkDirtyRepaint();
        }

        private void RefreshOverlays()
        {
            for (var i = _overlays.Count - 1; i >= 0; i--)
            {
                var overlay = _overlays[i];

                if (overlay.window != null && overlay.container.panel != null && AttachOverlay(overlay))
                    continue;

                overlay.container.RemoveFromHierarchy();
                _overlays.RemoveAt(i);
            }

            foreach (var obj in Resources.FindObjectsOfTypeAll(GameViewType))
            {
                if (obj is not EditorWindow window || HasOverlay(window))
                    continue;

                if (window.rootVisualElement == null)
                    continue;

                var container = new IMGUIContainer
                {
                    name = OverlayName,
                    pickingMode = PickingMode.Ignore,
                    focusable = false,
                    style =
                    {
                        position = Position.Absolute,
                        left = 0,
                        top = 0,
                        right = 0,
                        bottom = 0
                    }
                };

                var overlay = new Overlay { window = window, container = container };
                container.onGUIHandler = () => OnOverlayGUI(overlay);

                if (AttachOverlay(overlay))
                    _overlays.Add(overlay);
            }
        }

        /// <summary>
        /// Places the overlay so its cursor rect is registered before the Game view's own.
        /// The Editor honours the first cursor rect added under the pointer, and the window's IMGUI
        /// (which registers the play-mode cursor rect) runs from the first child of the panel root, so the
        /// overlay must sit in the panel root ahead of it. Returns false when the window has no panel yet.
        /// </summary>
        private static bool AttachOverlay(Overlay overlay)
        {
            var root = overlay.window.rootVisualElement;

            if (root == null)
                return false;

            var host = root.parent ?? root;

            if (host.panel == null)
                return false;

            if (overlay.container.parent == host && host.IndexOf(overlay.container) == 0)
                return true;

            overlay.container.RemoveFromHierarchy();
            host.Insert(0, overlay.container);
            return true;
        }

        private bool HasOverlay(EditorWindow window)
        {
            foreach (var overlay in _overlays)
            {
                if (overlay.window == window)
                    return true;
            }

            return false;
        }

        private void RemoveOverlays()
        {
            foreach (var overlay in _overlays)
                overlay.container.RemoveFromHierarchy();

            _overlays.Clear();
        }

        private void OnOverlayGUI(Overlay overlay)
        {
            if (!_serviceActive || _cursor == null || Event.current.type != EventType.Repaint)
                return;

            EditorGUIUtility.AddCursorRect(GetGameAreaRect(overlay), _cursor.Value);
        }

        /// <summary>
        /// The game area in the overlay's local coordinates. The window's rootVisualElement excludes the
        /// tab strip, and viewInWindow is relative to it, so both are re-based against the overlay.
        /// </summary>
        private static Rect GetGameAreaRect(Overlay overlay)
        {
            var root = overlay.window.rootVisualElement;
            var overlayOrigin = overlay.container.worldBound.position;

            var windowRect = root != null ? root.worldBound : overlay.container.worldBound;
            windowRect.position -= overlayOrigin;

            if (ViewInWindowProperty == null)
                return windowRect;

            try
            {
                if (ViewInWindowProperty.GetValue(overlay.window) is Rect view && view.width > 0 && view.height > 0)
                {
                    view.position += windowRect.position;
                    return view;
                }
            }
            catch (Exception)
            {
                // Internal API changed shape; fall back to the whole window content.
            }

            return windowRect;
        }

        private void RepaintOverlays()
        {
            foreach (var overlay in _overlays)
            {
                overlay.container.MarkDirtyRepaint();
                overlay.window.Repaint();
            }
        }

        private static MouseCursor? ToEditorCursor(NTCursors cursor)
        {
            return cursor switch
            {
                // Let the Game view handle the default so Unity's own custom cursor textures still show.
                NTCursors.Default => null,
                NTCursors.Arrow => MouseCursor.Arrow,
                NTCursors.IBeam => MouseCursor.Text,
                NTCursors.Crosshair => MouseCursor.ArrowPlus,
                NTCursors.Link => MouseCursor.Link,
                NTCursors.Busy => MouseCursor.RotateArrow,
                NTCursors.Invalid => MouseCursor.ArrowMinus,
                NTCursors.ResizeVertical => MouseCursor.ResizeVertical,
                NTCursors.ResizeHorizontal => MouseCursor.ResizeHorizontal,
                NTCursors.ResizeDiagonalLeft => MouseCursor.ResizeUpLeft,
                NTCursors.ResizeDiagonalRight => MouseCursor.ResizeUpRight,
                NTCursors.ResizeAll => MouseCursor.MoveArrow,
                NTCursors.OpenHand => MouseCursor.Pan,
                NTCursors.ClosedHand => MouseCursor.Pan,
                _ => null
            };
        }

        public bool SetCursor(NTCursors ntCursor)
        {
            _cursor = ToEditorCursor(ntCursor);

            if (!_serviceActive)
                return true;

            if (_overlays.Count == 0)
                RefreshOverlays();

            RepaintOverlays();
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }

        public void OnActivated()
        {
            if (_serviceActive)
                return;

            _serviceActive = true;
            _nextWindowScanTime = 0;
            EditorApplication.update += OnEditorUpdate;
            RefreshOverlays();
        }

        public void OnDeactivated()
        {
            if (!_serviceActive)
                return;

            _serviceActive = false;
            EditorApplication.update -= OnEditorUpdate;
            _cursor = null;
            RepaintOverlays();
            RemoveOverlays();
        }
    }
}
