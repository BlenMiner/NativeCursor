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

    /// <summary>Switches to a custom cursor service.</summary>
    public static void SetService(ICursorService service);
}
```

## Custom cursor services

Custom services implement `ICursorService`. If a service installs a native hook, polls for cursor changes, or animates cursor frames, it should also implement `ICursorServiceLifecycle` and stop that work while inactive.

When services change, `NativeCursor` resets and deactivates the old service, activates the new service, then reapplies the last requested `NTCursors` value. This prevents inactive native services from fighting a virtual pack or another custom service.

```c#
public sealed class CustomCursorService : ICursorService, ICursorServiceLifecycle
{
    public bool SetCursor(NTCursors cursor) { /* apply it */ return true; }
    public void ResetCursor() { /* restore default */ }
    public void OnActivated() { /* start enforcement or animation */ }
    public void OnDeactivated() { /* stop enforcement or animation */ }
}
```

## Cursor Types

> Native platform services run only in player builds. The Unity Editor cannot safely expose a platform-native cursor hook only to a docked Game view, so treat Editor cursor behavior as a preview and validate final behavior in a build.

> Some platforms use the closest available native cursor when the OS does not expose an exact visual match.

See [Platform Behavior](platformBehavior.md) for the exact native mappings and the build-only cursor hardening behavior.

When a virtual cursor pack is active, `Default` and `Arrow` both use the pack's default cursor entry. Use `NativeCursor.ClearCursorPack()` to return to the platform-native service.

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
