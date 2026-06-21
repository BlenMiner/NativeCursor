using UnityEngine;
using UnityEngine.EventSystems;

namespace Riten.Native.Cursors.UI
{
    public class OnDragCursor : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private NTCursors _cursor;
        [Tooltip("Higher priority means this cursor will override other cursors with lower priority")]
        [SerializeField] private int _priority = -1;

        private bool _isDragging;

        private int _pushedId;

        public NTCursors Cursor
        {
            get => _cursor;
            set
            {
                if (_cursor == value) return;

                _cursor = value;

                if (_isDragging)
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

                if (_isDragging)
                    CursorStack.Update(_pushedId, _cursor, _priority, SecondaryPriority);
            }
        }

        private int SecondaryPriority => transform.GetSecondaryPriority(transform.CalculateTransformDepth());

        private void OnDisable()
        {
            ClearCursor();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isDragging)
                return;

            _isDragging = true;
            _pushedId = CursorStack.Push(_cursor, _priority, SecondaryPriority);
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            ClearCursor();
        }

        private void ClearCursor()
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            CursorStack.Pop(_pushedId);
            _pushedId = 0;
        }
    }
}
