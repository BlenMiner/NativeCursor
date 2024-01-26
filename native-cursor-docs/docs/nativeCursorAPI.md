# NativeCursor API

This is the native cursor API. It allows you to change the cursor to any of the available cursors.
We recommend you use the [`CursorStack`](cursorStackAPI.md) API instead for most use cases, it's more flexible, easier and serves as a wrapper for this raw API.

```c#
namespace Riten.Native.Cursors;

public static class NativeCursor
{
    /// <summary>
    /// Changes the OS's cursor to the specified cursor.
    /// Implementation varies based on environment.
    /// </summary>
    public static bool SetCursor(NTCursors cursor);

    /// <summary>
    /// Resets cursor to default state.
    /// Certain platforms may include extra cleanup.
    /// Prefer this over SetCursor(NTCursors.Arrow);
    /// </summary>
    public static void ResetCursor();
}
```

## Cursor Types

> ⚠️ Editor doesn't reflect the actual cursor, it should be considered as a placeholder for testing purposes.
> It's best to test in a build.

> The `Busy` and `Invalid` cursors are known to be unreliable on MacOS.

```c#
namespace Riten.Native.Cursors;

public enum NTCursors
{
    Default,
    Arrow,
    IBeam,
    Crosshair,
    Link,
    Busy,
    Invalid,
    ResizeVertical,
    ResizeHorizontal,
    ResizeDiagonalLeft,
    ResizeDiagonalRight,
    ResizeAll,
    OpenHand,
    ClosedHand
}
```