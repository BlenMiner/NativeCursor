using UnityEngine;
using UnityEngine.EventSystems;

namespace Riten.Native.Cursors.UI
{
    public class OnPressCursor : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private NTCursors _cursor;
        [Tooltip("Higher priority means this cursor will override other cursors with lower priority")]
        [SerializeField] private int _priority = -1;

        public NTCursors Cursor
        {
            get => _cursor;
            set
            {
                if (_cursor == value) return;

                _cursor = value;

                if (_isPressing)
                    CursorStack.Update(_pushedId, _cursor, _priority, SecondaryPriority);
            }
        }

        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority == value) return;

                _priority = value;

                if (_isPressing)
                    CursorStack.Update(_pushedId, _cursor, _priority, SecondaryPriority);
            }
        }

        private bool _isPressing;
        
        private int _pushedId;

        private int SecondaryPriority => transform.GetSecondaryPriority(transform.CalculateTransformDepth());

        private void OnDisable()
        {
            ClearCursor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isPressing)
                return;

            _isPressing = true;
            _pushedId = CursorStack.Push(_cursor, _priority, SecondaryPriority);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ClearCursor();
        }

        private void ClearCursor()
        {
            if (!_isPressing)
                return;

            _isPressing = false;
            CursorStack.Pop(_pushedId);
            _pushedId = 0;
        }
    }
}
