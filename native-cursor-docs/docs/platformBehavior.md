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
| `ResizeDiagonalLeft` | Northwest/southeast resize | Up resize | Bottom-left corner | `nwse-resize` |
| `ResizeDiagonalRight` | Northeast/southwest resize | Down resize | Bottom-right corner | `nesw-resize` |
| `ResizeAll` | Move | Arrow | Move | `move` |
| `OpenHand` | Hand | Open hand | Hand | `grab` |
| `ClosedHand` | Hand | Closed hand | Hand | `grabbing` |

Some operating systems do not expose a perfect visual match for every cursor shape. In those cases the package uses the closest native system cursor and keeps the hardware/OS cursor path active.

## Player hardening

Unity and the OS can replace the active cursor after your code sets it. Native Cursor protects against that in player builds:

- Windows subclasses every Unity player window on the main thread (matched by window class, so native dialogs and message boxes are never hooked) to handle client-area `WM_SETCURSOR`, then reapplies after Unity processes mouse movement. Windows created later, such as secondary displays, are picked up automatically. Unity hides the cursor through the system display counter, so `Cursor.visible` and `CursorLockMode.Locked` keep working. The subclass is removed when the service is disabled, deactivated, or destroyed.
- MacOS keeps the current `NSCursor` authoritative and redirects later AppKit or Unity cursor changes back to the active native cursor.
- Linux uses XFixes cursor notifications when available. If XFixes is not available, it falls back to a low-frequency reapply while focused. It never applies a cursor to the X root window during focus transitions.
- WebGL writes the matching CSS cursor value to Unity's active canvas, with fallbacks for custom WebGL templates.

This keeps the visible cursor representative of what the game uses in builds, without relying on the virtual cursor fallback.

## Editor behavior

The platform P/Invoke services are build-only. In particular, the Windows service does not subclass the Unity Editor window: a docked Game view has no independent native window, so an Editor hook would also affect Inspector, Console, and other panes. Treat Editor cursor display as a convenience preview and validate final cursor behavior in a player build.

## Build workflows

The MacOS service depends on `Assets/NativeCursor/Scripts/Native/MacOS/Plugins/CursorWrapper.dylib`, built from `MacOS/CursorWrapper.m`.

The GitHub workflow at `.github/workflows/build-native-cursor-plugin.yml` runs on changes to the wrapper source, the MacOS cursor service, or the workflow file. It compiles the universal `x86_64`/`arm64` dylib and uploads it as a workflow artifact. On pushes, it also commits the rebuilt binary back to the branch when the output changed. On pull requests, it compiles and uploads the artifact as validation but does not push changes.

The docs workflow at `.github/workflows/build-docs.yml` runs on documentation changes, builds the MkDocs site, and uploads the generated `native-cursor-docs/site` folder as a workflow artifact.
