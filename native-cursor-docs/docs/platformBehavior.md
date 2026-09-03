# Platform Behavior

Native Cursor uses the operating system cursor in player builds. It does not fall back to a software cursor unless you explicitly switch to a virtual cursor pack.

## Supported cursors

Every value in `NTCursors` has a native mapping on WebGL, Windows, MacOS, and Linux:

| Cursor | Windows | MacOS | Linux | WebGL |
| --- | --- | --- | --- | --- |
| `Default` | Arrow | Arrow | Arrow | `default` |
| `Arrow` | Arrow | Arrow | Arrow | `default` |
| `IBeam` | I-beam | I-beam | XTerm | `text` |
| `Crosshair` | Crosshair | Crosshair | Crosshair | `crosshair` |
| `Link` | Hand | Pointing hand | Hand | `pointer` |
| `Busy` | Wait | Busy cursor when available, otherwise Arrow | Watch | `wait` |
| `Invalid` | No | Operation not allowed | X cursor | `not-allowed` |
| `ResizeVertical` | North/south resize | Up/down resize | Vertical double arrow | `ns-resize` |
| `ResizeHorizontal` | East/west resize | Left/right resize | Horizontal double arrow | `ew-resize` |
| `ResizeDiagonalLeft` | Northwest/southeast resize | Top-left corner resize on macOS 15+, otherwise Up resize | Bottom-right corner (northwest/southeast) | `nwse-resize` |
| `ResizeDiagonalRight` | Northeast/southwest resize | Top-right corner resize on macOS 15+, otherwise Down resize | Bottom-left corner (northeast/southwest) | `nesw-resize` |
| `ResizeAll` | Move | Arrow | Move | `move` |
| `OpenHand` | Hand | Open hand | Hand | `grab` |
| `ClosedHand` | Hand | Closed hand | Hand | `grabbing` |

Some operating systems do not expose a perfect visual match for every cursor shape. In those cases the package uses the closest native system cursor and keeps the hardware/OS cursor path active.

## Player hardening

Unity and the OS can replace the active cursor after your code sets it. Native Cursor protects against that in player builds:

- Windows subclasses every Unity player window on the main thread (matched by window class, so native dialogs and message boxes are never hooked) to handle client-area `WM_SETCURSOR`, then reapplies after Unity processes mouse movement. Windows created later, such as secondary displays, are picked up automatically. Unity hides the cursor through the system display counter, so `Cursor.visible` and `CursorLockMode.Locked` keep working. The subclass is removed when the service is disabled, deactivated, or destroyed.
- MacOS keeps the current `NSCursor` authoritative and redirects later AppKit or Unity cursor changes back to the active native cursor, but only while the pointer is inside the content area of one of the app's windows. The title bar, window edges, and other applications keep their own cursors. The `Busy` cursor uses a private AppKit selector when it exists and falls back to the arrow otherwise; if you submit to the Mac App Store, map `Busy` to a virtual cursor instead.
- Linux defines the cursor on every X11 window owned by the process, found through the window manager's `_NET_CLIENT_LIST` and `_NET_WM_PID`, so it never touches another application's window. XFixes cursor notifications reapply the cursor when Unity or SDL replace it; without XFixes it falls back to a low-frequency reapply while focused. X errors from its own requests are swallowed rather than terminating the player. Under native Wayland there is no X11 window to target and the service stays idle with a single warning.
- WebGL writes the matching CSS cursor value to Unity's active canvas, with fallbacks for custom WebGL templates. Unity implements `Cursor.visible` through the same property, so the service tracks visibility: cursor changes while hidden stay hidden, and showing the cursor restores the requested shape.

This keeps the visible cursor representative of what the game uses in builds, without relying on the virtual cursor fallback.

## Editor behavior

The platform P/Invoke services are build-only. In particular, the Windows service does not subclass the Unity Editor window: a docked Game view has no independent native window, so an Editor hook would also affect Inspector, Console, and other panes.

Instead, an Editor-only service (`EditorCursorService`) maps each `NTCursors` value to the closest Editor `MouseCursor` and registers a cursor rect over the game area of every open Game view, in both edit mode and play mode. It uses an overlay that ignores input, so game interaction is unaffected. `Default` is left to the Game view so Unity's own custom cursor textures still show. Shapes the Editor cannot draw use a stand-in: `Crosshair` shows an arrow with a plus, `Busy` a rotate arrow, `Invalid` an arrow with a minus, and the hand cursors the pan hand. Treat Editor cursor display as a convenience preview and validate final cursor behavior in a player build.

## Build workflows

The MacOS service depends on `Assets/NativeCursor/Scripts/Native/MacOS/Plugins/CursorWrapper.dylib`, built from `MacOS/CursorWrapper.m`.

The GitHub workflow at `.github/workflows/build-native-cursor-plugin.yml` runs on changes to the wrapper source, the MacOS cursor service, or the workflow file. It compiles the universal `x86_64`/`arm64` dylib and uploads it as a workflow artifact. On pushes, it also commits the rebuilt binary back to the branch when the output changed. On pull requests, it compiles and uploads the artifact as validation but does not push changes.

The docs workflow at `.github/workflows/build-docs.yml` runs on documentation changes, builds the MkDocs site, and uploads the generated `native-cursor-docs/site` folder as a workflow artifact.
