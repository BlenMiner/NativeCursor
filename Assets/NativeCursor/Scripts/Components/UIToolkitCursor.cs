using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riten.Native.Cursors.UI
{
    public enum UIToolkitCursorTrigger
    {
        Hover,
        Press,
        Drag
    }

    public sealed class CursorManipulator : PointerManipulator, IDisposable
    {
        private NTCursors _cursor;
        private UIToolkitCursorTrigger _trigger;
        private int _priority;
        private int _secondaryPriority;
        private float _dragThreshold = 4f;

        private int _pushedId;
        private bool _isPressed;
        private bool _isDragging;
        private Vector2 _pressPosition;

        public CursorManipulator(
            NTCursors cursor,
            UIToolkitCursorTrigger trigger = UIToolkitCursorTrigger.Hover,
            int priority = -2,
            int secondaryPriority = 0)
        {
            _cursor = cursor;
            _trigger = trigger;
            _priority = priority;
            _secondaryPriority = secondaryPriority;
        }

        public NTCursors Cursor
        {
            get => _cursor;
            set
            {
                if (_cursor == value)
                    return;

                _cursor = value;

                if (_pushedId != 0)
                    CursorStack.Replace(_pushedId, _cursor);
            }
        }

        public UIToolkitCursorTrigger Trigger
        {
            get => _trigger;
            set
            {
                if (_trigger == value)
                    return;

                ClearCursor();
                _isPressed = false;
                _isDragging = false;
                _trigger = value;
            }
        }

        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority == value)
                    return;

                _priority = value;
                RePushActiveCursor();
            }
        }

        public int SecondaryPriority
        {
            get => _secondaryPriority;
            set
            {
                if (_secondaryPriority == value)
                    return;

                _secondaryPriority = value;
                RePushActiveCursor();
            }
        }

        public float DragThreshold
        {
            get => _dragThreshold;
            set => _dragThreshold = Mathf.Max(0f, value);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            ClearCursor();
            _isPressed = false;
            _isDragging = false;

            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Dispose()
        {
            var currentTarget = target;

            if (currentTarget != null)
                currentTarget.RemoveManipulator(this);
            else
                ClearCursor();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_trigger == UIToolkitCursorTrigger.Hover)
                PushCursor();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_trigger == UIToolkitCursorTrigger.Hover ||
                _trigger == UIToolkitCursorTrigger.Press ||
                _trigger == UIToolkitCursorTrigger.Drag)
            {
                ClearCursor();
            }

            _isPressed = false;
            _isDragging = false;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_trigger == UIToolkitCursorTrigger.Press)
            {
                PushCursor();
                return;
            }

            if (_trigger != UIToolkitCursorTrigger.Drag)
                return;

            _isPressed = true;
            _isDragging = false;
            _pressPosition = GetPointerPosition(evt);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_trigger != UIToolkitCursorTrigger.Drag || !_isPressed || _isDragging)
                return;

            var delta = GetPointerPosition(evt) - _pressPosition;

            if (delta.sqrMagnitude < _dragThreshold * _dragThreshold)
                return;

            _isDragging = true;
            PushCursor();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_trigger == UIToolkitCursorTrigger.Press || _trigger == UIToolkitCursorTrigger.Drag)
                ClearCursor();

            _isPressed = false;
            _isDragging = false;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ClearCursor();
            _isPressed = false;
            _isDragging = false;
        }

        private void PushCursor()
        {
            if (_pushedId != 0)
                return;

            _pushedId = CursorStack.Push(_cursor, _priority, _secondaryPriority);
        }

        private void ClearCursor()
        {
            if (_pushedId == 0)
                return;

            CursorStack.Pop(_pushedId);
            _pushedId = 0;
        }

        private void RePushActiveCursor()
        {
            if (_pushedId == 0)
                return;

            CursorStack.Pop(_pushedId);
            _pushedId = CursorStack.Push(_cursor, _priority, _secondaryPriority);
        }

        private static Vector2 GetPointerPosition(IPointerEvent evt)
        {
            var position = evt.position;
            return new Vector2(position.x, position.y);
        }
    }

    public static class UIToolkitCursorExtensions
    {
        public static CursorManipulator AddNativeCursor(
            this VisualElement element,
            NTCursors cursor,
            UIToolkitCursorTrigger trigger = UIToolkitCursorTrigger.Hover,
            int priority = -2,
            int secondaryPriority = 0)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var manipulator = new CursorManipulator(cursor, trigger, priority, secondaryPriority);
            element.AddManipulator(manipulator);
            return manipulator;
        }
    }

    [DisallowMultipleComponent]
    public class UIToolkitCursorBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private List<CursorBinding> _bindings = new();

        private readonly List<CursorManipulator> _manipulators = new();

        public IReadOnlyList<CursorBinding> Bindings => _bindings;

        private void Reset()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            ApplyBindings();
        }

        private void OnDisable()
        {
            ClearBindings();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            ApplyBindings();
        }

        public void ApplyBindings()
        {
            ClearBindings();

            var document = _document ? _document : GetComponent<UIDocument>();
            var root = document ? document.rootVisualElement : null;

            if (root == null)
                return;

            foreach (var binding in _bindings)
                binding.Apply(root, _manipulators);
        }

        public void ClearBindings()
        {
            for (var i = _manipulators.Count - 1; i >= 0; --i)
                _manipulators[i]?.Dispose();

            _manipulators.Clear();
        }

        [Serializable]
        public class CursorBinding
        {
            [SerializeField] private string _elementName;
            [SerializeField] private string _className;
            [SerializeField] private UIToolkitCursorTrigger _trigger = UIToolkitCursorTrigger.Hover;
            [SerializeField] private NTCursors _cursor = NTCursors.Link;
            [SerializeField] private int _priority = -2;
            [SerializeField] private int _secondaryPriority;
            [SerializeField, Min(0f)] private float _dragThreshold = 4f;

            internal void Apply(VisualElement root, List<CursorManipulator> manipulators)
            {
                var elementName = string.IsNullOrEmpty(_elementName) ? null : _elementName;
                var className = string.IsNullOrEmpty(_className) ? null : _className;

                if (elementName == null && className == null)
                {
                    AddManipulator(root, manipulators);
                    return;
                }

                root.Query<VisualElement>(elementName, className)
                    .ForEach(element => AddManipulator(element, manipulators));
            }

            private void AddManipulator(VisualElement element, List<CursorManipulator> manipulators)
            {
                if (element == null)
                    return;

                var manipulator = element.AddNativeCursor(_cursor, _trigger, _priority, _secondaryPriority);
                manipulator.DragThreshold = _dragThreshold;
                manipulators.Add(manipulator);
            }
        }
    }
}
