using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riten.Native.Cursors
{
    public struct CursorStackItem
    {
        public NTCursors cursor;
        public int id;
        public int priority;
        public int secondaryPriority;
    }

    public sealed class CursorStackScope : IDisposable
    {
        public int Id { get; private set; }

        internal CursorStackScope(int id)
        {
            Id = id;
        }

        public void Dispose()
        {
            if (Id == 0)
                return;

            var id = Id;
            Id = 0;
            CursorStack.Pop(id);
        }
    }
    
    public static class CursorStack
    {
        static int _nextUid = 1;
        static bool _paused;
        
        static readonly List<CursorStackItem> _stack = new ();

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
        {
            _stack.Clear();
            _nextUid = 1;
            _paused = false;
            NotifyChanged();
        }

        public static int Count => _stack.Count;

        public static bool IsRenderingPaused => _paused;

        public static void CopyItemsTo(List<CursorStackItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            items.Clear();
            items.AddRange(_stack);
        }

        static void NotifyChanged()
        {
            Changed?.Invoke();
        }
        
        static void OnStackChanged(bool force = false)
        {
            if (!force && _paused) return;
            
            if (_stack.Count == 0)
                 NativeCursor.ResetCursor();
            else NativeCursor.SetCursor(Peek().cursor);
        }

        static bool HasActiveCursorChanged(CursorStackItem previous, CursorStackItem current)
        {
            return previous.id != current.id || previous.cursor != current.cursor;
        }

        /// <summary>
        /// A debug GUI to see the current stack.
        /// Uses GUILayout, so it's not very customizable.
        /// Just dumps the stack from top to bottom.
        /// </summary>
        public static void OnDebugGUI()
        {
            GUILayout.Label($"Stack count: {_stack.Count}");
            GUILayout.Label($"Paused: {_paused}");
            
            var peek = Peek();
            
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (peek.id == _stack[i].id)
                    GUI.color = Color.green;
                GUILayout.Label($"{_stack[i].id}: Cursor {_stack[i].cursor} with priority {_stack[i].priority} and secondary priority {_stack[i].secondaryPriority}");
                GUI.color = Color.white;
            }
        }
        
        /// <summary>
        /// If true, the cursor will not change until this is set to false.
        /// The stack will still be updated, but won't affect the cursor.
        /// Once unpaused, the cursor will be updated automatically.
        /// </summary>
        public static void PauseRendering(bool isPaused)
        {
            if (_paused == isPaused)
                return;

            _paused = isPaused;
            OnStackChanged();
            NotifyChanged();
        }
        
        /// <summary>
        /// Call this if you used NativeCursor.SetCursor() directly and want to reapply the stack's cursor.
        /// </summary>
        public static void ReApply()
        {
            OnStackChanged(true);
            NotifyChanged();
        }

        /// <summary>
        /// Pushes a cursor to the stack.
        /// Higher priority means this cursor will override other cursors with lower priority.
        /// </summary>
        /// <returns>The id of the pushed cursor. Use this to remove the cursor later.</returns>
        public static int Push(NTCursors cursor, int priority = 0, int secondaryPriority = 0)
        {
            var previousActive = Peek();
            var uid = _nextUid++;
            
            _stack.Add(new CursorStackItem
            {
                cursor = cursor,
                id = uid,
                priority = priority,
                secondaryPriority = secondaryPriority
            });

            if (HasActiveCursorChanged(previousActive, Peek()))
                OnStackChanged();

            NotifyChanged();

            return uid;
        }

        /// <summary>
        /// Pushes a cursor and returns a disposable handle that pops it when disposed.
        /// </summary>
        public static CursorStackScope PushScoped(NTCursors cursor, int priority = 0, int secondaryPriority = 0)
        {
            return new CursorStackScope(Push(cursor, priority, secondaryPriority));
        }
        
        /// <summary>
        /// Pops the cursor that is being rendered.
        /// Always prefer using Pop(int id) if you have the id.
        /// </summary>
        /// <returns>True if a cursor was removed, false otherwise.</returns>
        public static bool Pop()
        {
            if (_stack.Count > 0)
            {
                var top = Peek();
                return Pop(top.id);
            }

            return false;
        }
        
        /// <summary>
        /// Pops the cursor with the given id.
        /// You can get the id from the return of Push().
        /// </summary>
        /// <returns></returns>
        public static bool Pop(int id)
        {
            if (id == 0) return false;

            var previousActive = Peek();
            
            bool removed = false;
            
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].id == id)
                {
                    _stack.RemoveAt(i);
                    removed = true;
                    break;
                }
            }

            if (removed)
            {
                if (HasActiveCursorChanged(previousActive, Peek()))
                    OnStackChanged();

                NotifyChanged();
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Removes all cursors from the stack.
        /// Resets the cursor to default.
        /// </summary>
        public static void Clear()
        {
            if (_stack.Count == 0)
                return;

            var previousActive = Peek();
            
            _stack.Clear();

            if (HasActiveCursorChanged(previousActive, Peek()))
                OnStackChanged();

            NotifyChanged();
        }
        
        /// <summary>
        /// Returns true if the stack is empty.
        /// </summary>
        public static bool IsEmpty => _stack.Count == 0;
        
        /// <summary>
        /// Returns the cursor at the top of the stack.
        /// It will be the cursor with the highest priority.
        /// </summary>
        /// <returns>Default if the stack is empty. Otherwise, the cursor with highest priority.</returns>
        public static CursorStackItem Peek()
        {
            TryPeek(out var item);
            return item;
        }

        /// <summary>
        /// Gets the cursor at the top of the stack.
        /// </summary>
        /// <returns>True if the stack has an active item, false otherwise.</returns>
        public static bool TryPeek(out CursorStackItem item)
        {
            item = default;

            if (_stack.Count == 0)
                return false;

            int highestPriority = _stack[_stack.Count - 1].priority;
            int highestSecondaryPriority = _stack[_stack.Count - 1].secondaryPriority;
            int idx = _stack.Count - 1;
                
            for (int i = _stack.Count - 2; i >= 0; i--)
            {
                if (_stack[i].priority >= highestPriority)
                {
                    if (highestPriority != _stack[i].priority)
                    {
                        highestPriority = _stack[i].priority;
                        highestSecondaryPriority = _stack[i].secondaryPriority;
                        idx = i;
                        continue;
                    }
                    
                    if (_stack[i].secondaryPriority > highestSecondaryPriority)
                    {
                        highestSecondaryPriority = _stack[i].secondaryPriority;
                        idx = i;
                    }
                }
            }
            
            item = _stack[idx];
            return true;
        }

        /// <summary>
        /// Returns true if an item with the given id is still in the stack.
        /// </summary>
        public static bool Contains(int id)
        {
            if (id == 0)
                return false;

            for (int i = _stack.Count - 1; i >= 0; --i)
            {
                if (_stack[i].id == id)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Replace the cursor with the given id with the given cursor.
        /// This will not change the priority of the cursor.
        /// </summary>
        public static bool Replace(int id, NTCursors cursor)
        {
            return Update(id, cursor);
        }

        /// <summary>
        /// Updates a cursor stack item in place.
        /// Pass null for priority values that should stay unchanged.
        /// </summary>
        public static bool Update(int id, NTCursors cursor, int? priority = null, int? secondaryPriority = null)
        {
            if (id == 0)
                return false;

            var previousActive = Peek();

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].id == id)
                {
                    var item = _stack[i];
                    var newPriority = priority ?? item.priority;
                    var newSecondaryPriority = secondaryPriority ?? item.secondaryPriority;

                    if (item.cursor == cursor &&
                        item.priority == newPriority &&
                        item.secondaryPriority == newSecondaryPriority)
                    {
                        return true;
                    }
                    
                    _stack[i] = new CursorStackItem
                    {
                        cursor = cursor,
                        id = id,
                        priority = newPriority,
                        secondaryPriority = newSecondaryPriority
                    };
                    
                    if (HasActiveCursorChanged(previousActive, Peek()))
                        OnStackChanged();

                    NotifyChanged();
                    return true;
                }
            }

            return false;
        }
    }
}
