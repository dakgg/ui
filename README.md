# ViewSystem UI Framework

A view/UI management framework for Unity (uGUI). It provides a stack-based view
manager with focus and input control, layered canvases, back-button handling,
and Addressables-based loading.

> Package name: `com.dakgg.ui` · Namespace: `ViewSystem`

## Features

- **View lifecycle** — add/remove, active/inactive, and focus callbacks via
  listener interfaces (`IViewAddedListener`, `IViewFocusListener`, …).
- **Input & visibility control** — per-view input locking (`SetInputEnable`),
  canvas visibility, and raycast blocking managed centrally.
- **View types** — `ModalView` and `PageView` with configurable layer, global
  (don't-destroy) flag, and "hide behind views" behavior.
- **Animations** — `AnimationSet` open/close animations driven by `UIAnimation`.
- **Addressables** — views are loaded and released through `UnityEngine.AddressableAssets`.

## Installation (UPM)

Add the package to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dakgg.ui": "https://github.com/dakgg/ui.git"
  }
}
```

Or in the Unity Editor: **Window → Package Manager → + → Add package from git URL…**
and enter `https://github.com/dakgg/ui.git`.

To pin a version, append a tag: `...ui.git#0.1.0`.

### Requirements

- Unity **2021.3** or newer
- `com.unity.addressables`

## Layout

```
Runtime/   Dakgg.UI         — runtime assembly (UIBase, ModalView, PageView, …)
Editor/    Dakgg.UI.Editor  — editor-only assembly
```

## Note on `.meta` files

This repository was scaffolded outside the Unity Editor, so per-asset `.meta`
files are not yet committed. Open the package once in Unity to generate them,
then commit the generated `.meta` files — Unity packages require them.

## License

[MIT](LICENSE.md)
