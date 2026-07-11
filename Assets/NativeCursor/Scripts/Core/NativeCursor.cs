using Riten.Native.Cursors.Virtual;
using UnityEngine;

namespace Riten.Native.Cursors
{
    public static class NativeCursor
    {
        private static readonly object PublicCursorPackOwner = new();

        static ICursorService _instance;

        private static ICursorService _defaultService;
        private static VirtualCursorService _vcs;
        private static NTCursors _requestedCursor = NTCursors.Default;
        private static object _cursorPackOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _defaultService = null;
            _vcs = null;
            _requestedCursor = NTCursors.Default;
            _cursorPackOwner = null;
        }

        public static string ServiceName => _instance == null ? "NULL" : _instance.GetType().Name;
        public static MaskCursorMode VirtualMaskCursorMode
        {
            get => VirtualCursorService.maskCursorMode;
            set => VirtualCursorService.maskCursorMode = value;
        }

        public static int LiveMaskInversionUpdatesPerSecond
        {
            get => VirtualCursorService.liveMaskInversionUpdatesPerSecond;
            set => VirtualCursorService.liveMaskInversionUpdatesPerSecond = Mathf.Max(1, value);
        }
        
        public static void SetFallbackService(ICursorService service)
        {
            _defaultService = service;
        }
        
        /// <summary>
        /// Set custom cursor service.
        /// You should not need to call this method.
        /// But you can!
        /// </summary>
        public static void SetService(ICursorService service)
        {
            if (_instance == service) 
                return;

            if (!ReferenceEquals(service, _vcs))
                _cursorPackOwner = null;
            
            _instance?.ResetCursor();

            if (_instance is ICursorServiceLifecycle previousLifecycle)
                previousLifecycle.OnDeactivated();

            _instance = service;

            if (_instance is ICursorServiceLifecycle currentLifecycle)
                currentLifecycle.OnActivated();

            if (_instance == null)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            else
                _instance.SetCursor(_requestedCursor);
        }
        
        public static bool SetCursor(NTCursors ntCursor)
        {
            _requestedCursor = ntCursor;
            return _instance != null && _instance.SetCursor(ntCursor);
        }
        
        /// <summary>
        /// This method uses a virtual cursor pack to set the cursor.
        /// </summary>
        public static void SetCursorPack(CursorPack cursorPack, Camera cmr)
        {
            if (cursorPack == null)
            {
                ClearCursorPackInternal();
                return;
            }

            SetCursorPack(cursorPack, cmr, PublicCursorPackOwner);
        }

        internal static void SetCursorPack(CursorPack cursorPack, Camera cmr, object owner)
        {
            if (cursorPack == null)
            {
                ClearCursorPack(owner);
                return;
            }

            if (owner == null)
                throw new System.ArgumentNullException(nameof(owner));
            
            if (!_vcs)
            {
                var go = new GameObject("VirtualCursorService")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                
                Object.DontDestroyOnLoad(go);
                
                _vcs = go.AddComponent<VirtualCursorService>();
            }
            
            _cursorPackOwner = owner;
            _vcs.UpdatePack(cursorPack, cmr);

            var wasAlreadyActive = ReferenceEquals(_instance, _vcs);
            SetService(_vcs);

            if (wasAlreadyActive)
                _vcs.SetCursor(_requestedCursor);
        }

        internal static object CursorPackOwner => _cursorPackOwner;

        internal static bool IsCursorPackOwner(object owner)
        {
            return owner != null && ReferenceEquals(_cursorPackOwner, owner);
        }

        internal static bool ClearCursorPack(object owner)
        {
            if (!IsCursorPackOwner(owner))
                return false;

            ClearCursorPackInternal();
            return true;
        }

        private static void ClearCursorPackInternal()
        {
            _cursorPackOwner = null;

            if (_vcs)
                _vcs.UpdatePack(null, null);

            SetService(_defaultService);
        }
        
        public static void SetCursorPackCamera(Camera cmr)
        {
            if (_vcs)
                _vcs.SetCamera(cmr);
        }

        public static void ClearCursorPack()
        {
            SetCursorPack(null, null);
        }
        
        /// <summary>
        /// Reset cursor to default.
        /// This is safer than setting the cursor to NTCursors.Arrow.
        /// </summary>
        public static void ResetCursor()
        {
            _requestedCursor = NTCursors.Default;
            _instance?.ResetCursor();
        }
    }
}
