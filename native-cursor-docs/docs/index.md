# Getting Started

## Installation

Download from the [Asset Store](https://assetstore.unity.com/packages/slug/220347).

## NativeCursor API

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

## Example usage:

```c#
using Riten.Native.Cursors;

NativeCursor.SetCursor(NTCursors.IBeam);
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

## Convinience Components

`OnHoverCursor` and `OnClickCursor` are convinience components that allow you to change the cursor on hover or click.