using UnityEngine;
using UnityEngine.EventSystems;

namespace Riten.Native.Cursors.UI
{
    public class OnHoverCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private NTCursors _cursor;
        [Tooltip("Higher priority means this cursor will override other cursors with lower priority")]
        [SerializeField] private int _priority = -2;

        public NTCursors Cursor
        {
            get => _cursor;
            set
            {
                if (_cursor == value) return;

                _cursor = value;

                if (_isHovering)
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

                if (_isHovering)
                    CursorStack.Update(_pushedId, _cursor, _priority, SecondaryPriority);
            }
        }
        
        private bool _isHovering;

        private int _pushedId;

        private int SecondaryPriority => transform.GetSecondaryPriority(transform.CalculateTransformDepth());

        private void OnDisable()
        {
            ClearCursor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isHovering)
                return;

            _isHovering = true;
            _pushedId = CursorStack.Push(
                _cursor, 
                _priority, 
                SecondaryPriority
            );
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearCursor();
        }

        private void ClearCursor()
        {
            if (!_isHovering)
                return;

            _isHovering = false;
            CursorStack.Pop(_pushedId);
            _pushedId = 0;
        }
    }
}
