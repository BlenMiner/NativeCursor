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

- Windows subclasses the Unity window and handles `WM_SETCURSOR`. It also checks the active OS cursor while the app is focused and the pointer is inside the client area, then reapplies only if another cursor replaced it.
- MacOS keeps the current `NSCursor` authoritative and redirects later AppKit or Unity cursor changes back to the active native cursor.
- Linux uses XFixes cursor notifications when available. If XFixes is not available, it falls back to a low-frequency reapply while focused.
- WebGL writes the matching CSS cursor value to the Unity canvas.

This keeps the visible cursor representative of what the game uses in builds, without relying on the virtual cursor fallback.

## Editor behavior

The native services are build-only. The Unity editor can still show editor-specific cursor behavior while inspecting UI, dragging windows, recompiling, or showing loading states. Treat editor cursor display as a convenience preview and validate final cursor behavior in a player build.

## Build workflows

The MacOS service depends on `Assets/NativeCursor/Scripts/Native/MacOS/Plugins/CursorWrapper.dylib`, built from `MacOS/CursorWrapper.m`.

The GitHub workflow at `.github/workflows/build-native-cursor-plugin.yml` runs on changes to the wrapper source, the MacOS cursor service, or the workflow file. It compiles the universal `x86_64`/`arm64` dylib and uploads it as a workflow artifact. On pushes, it also commits the rebuilt binary back to the branch when the output changed. On pull requests, it compiles and uploads the artifact as validation but does not push changes.

The docs workflow at `.github/workflows/build-docs.yml` runs on documentation changes, builds the MkDocs site, and uploads the generated `native-cursor-docs/site` folder as a workflow artifact.
