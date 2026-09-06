<img width="800" alt="2051" src="https://github.com/user-attachments/assets/355102fd-cd5a-4a63-a725-75af9c381540">

# What is Native Cursor?

Native Cursor is cross-platform package that allows you to change the cursor to any of the available cursors on the OS.
This is useful for games that want to use the OS's cursor instead of a custom one.

There are two modes, virtual and native cursors.
Virtual cursors allows you to use `.cur` and `.ani` files for your game.
Cursor stack helpers, UGUI components, UI Toolkit bindings, and an editor stack debugger are included for common interaction flows.

## Compatibility

WebGL, Windows, MacOS and Linux

All `NTCursors` values are mapped on every supported platform. Some operating systems do not expose a perfect visual match for every shape, so Native Cursor uses the closest native OS cursor rather than falling back to a software cursor.

## Installation

### Unity Package Manager (git URL)

In Unity open **Window > Package Manager**, click **+** and choose **Add package from git URL...**, then paste:

```
https://github.com/BlenMiner/NativeCursor.git?path=/Assets/NativeCursor
```

To pin a specific release or commit, append it after `#`:

```
https://github.com/BlenMiner/NativeCursor.git?path=/Assets/NativeCursor#v1.0.0
```

Or add it to `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.riten.nativecursor": "https://github.com/BlenMiner/NativeCursor.git?path=/Assets/NativeCursor"
  }
}
```

Requires Unity 2022.3 or newer. The package has no required dependencies. Two are optional and detected automatically:

- `com.unity.ugui` enables the `OnHoverCursor`, `OnPressCursor`, and `OnDragCursor` components and the example scene scripts.
- `com.unity.inputsystem` lets the virtual cursor's live inverted mask read the pointer position when the project uses the Input System as its active input handler. With the legacy Input Manager nothing extra is needed.

### Unity Package / Asset Store

Import the `.unitypackage` as usual; everything lives under `Assets/NativeCursor`.

## Docs

https://gameobject.xyz/nativecursor/
