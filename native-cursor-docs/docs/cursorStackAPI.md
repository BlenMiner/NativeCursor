# Cursor Stack API

This is the cursor stack API. It allows you to change the cursor to any of the available cursors.
The difference between this and the native cursor API is that this API allows you to push and pop cursors to a stack.
This is useful for when you want to change the cursor to a specific cursor for a specific part of your code, and then revert it back to the previous cursor.

This is used internally for the preset components, but you can and should use it for your own components as well.

```c#
namespace Riten.Native.Cursors;

public static class CursorStack
{
    public static event Action Changed;

    public static int Count { get; }
    public static bool IsEmpty { get; }
    public static bool IsRenderingPaused { get; }

    /// <summary>
    /// Pushes a cursor to the stack.
    /// Higher priority means this cursor will override other cursors with lower priority.
    /// </summary>
    /// <returns>The id of the pushed cursor. Use this to remove the cursor later.</returns>
    public static int Push(NTCursors cursor, int priority = 0, int secondaryPriority = 0);

    /// <summary>
    /// Pushes a cursor and returns a disposable handle that pops it when disposed.
    /// </summary>
    public static CursorStackScope PushScoped(NTCursors cursor, int priority = 0, int secondaryPriority = 0);

    /// <summary>
    /// Replace the cursor with the given id with the given cursor.
    /// This will not change the priority of the cursor.
    /// Use this if you want to change a cursor that is already in the stack.
    /// For example, if you want to change the cursor during a drag operation to indicate that the drag is invalid.
    /// </summary>
    public static bool Replace(int id, NTCursors cursor);

    /// <summary>
    /// Updates a cursor stack item in place.
    /// Pass null for priority values that should stay unchanged.
    /// </summary>
    public static bool Update(int id, NTCursors cursor, int? priority = null, int? secondaryPriority = null);

    /// <summary>
    /// Pops the cursor with the given id.
    /// You can get the id from the return of Push().
    /// </summary>
    /// <returns></returns>
    public static bool Pop(int id);

    /// <summary>
    /// Pops the cursor that is being rendered.
    /// Always prefer using Pop(int id) if you have the id.
    /// </summary>
    /// <returns>True if a cursor was removed, false otherwise.</returns>
    public static bool Pop();

    /// <summary>
    /// Removes all cursors from the stack.
    /// Resets the cursor to default.
    /// </summary>
    public static void Clear();

    /// <summary>
    /// Returns the cursor at the top of the stack.
    /// It will be the cursor with the highest priority.
    /// </summary>
    /// <returns>Default if the stack is empty. Otherwise, the cursor with highest priority.</returns>
    public static CursorStackItem Peek();

    /// <summary>
    /// Gets the cursor at the top of the stack.
    /// </summary>
    /// <returns>True if the stack has an active item, false otherwise.</returns>
    public static bool TryPeek(out CursorStackItem item);

    /// <summary>
    /// Returns true if an item with the given id is still in the stack.
    /// </summary>
    public static bool Contains(int id);

    public static void CopyItemsTo(List<CursorStackItem> items);

    /// <summary>
    /// A debug GUI to see the current stack.
    /// Uses GUILayout, so it's not very customizable.
    /// Just dumps the stack from top to bottom.
    /// </summary>
    public static void OnDebugGUI();

    /// <summary>
    /// If true, the cursor will not change until this is set to false.
    /// The stack will still be updated, but won't affect the cursor.
    /// Once unpaused, the cursor will be updated automatically.
    /// </summary>
    public static void PauseRendering(bool isPaused);

    /// <summary>
    /// Call this if you used NativeCursor.SetCursor() directly and want to reapply the stack's cursor.
    /// </summary>
    public static void ReApply();
}
```

I recommend you to look at the source code for the [components](components.md) implementations to see how they use this API if it's not clear enough.

## Example

```c#

// Push a cursor to the stack.
var cursorId = CursorStack.Push(NTCursors.Link);

// ... do stuff / wait for an event / whatever ...

// Pop the cursor from the stack.
CursorStack.Pop(cursorId);

// Scoped push.
using (CursorStack.PushScoped(NTCursors.Busy))
{
    // The Busy cursor is active until the scope exits.
}

// Update a pushed cursor without changing its id.
var dragCursorId = CursorStack.Push(NTCursors.ClosedHand, priority: 10);
CursorStack.Update(dragCursorId, NTCursors.Invalid, priority: 20);
CursorStack.Pop(dragCursorId);

```

## Debugger

You can inspect the current stack in **Window > Native Cursor > Cursor Stack Debugger**.
The debugger shows the active cursor, stack entries, priorities, and includes controls to pause rendering, reapply, pop, or clear the stack.
