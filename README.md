# ViewSystem UI Framework

A view/UI management framework for Unity (uGUI). It provides a stack-based view
manager with focus and input control, layered canvases, back-button handling,
and Addressables-based loading.

> Package name: `com.dakgg.ui` · Namespace: `ViewSystem` · Assembly: `Dakgg.UI`

## Features

- **Stack-based views** — `ViewManager` keeps views in ordered layers
  (`Default`, `Special`), each with its own canvas sort-order band.
- **View types** — `ViewBase` and its subclasses `ModalView`, `PageView`, and
  `CameraPageView<T>` (a page that spawns a companion camera). `OverlayView` is a
  separate, non-stacked static view type (e.g. tooltips).
- **Awaitable open/close** — `ViewRequest.Open<T>()` returns a request that you
  can `await` or `yield return`. Close or return a result with the `Complete()`,
  `Success()`, and `Success(result)` extension methods; typed results via
  `ViewRequest<T>`.
- **Lifecycle via interfaces** — opt into events by implementing marker
  interfaces (`IViewAddedListener`, `IViewInitialFocusListener`,
  `IViewFocusListener`, `IViewBackButtonListener`, `IViewUpdateListener`,
  `IViewVisibleListener`, …) — no base-method overrides needed.
- **Focus, visibility & input control** — a single refresh pass recomputes the
  focused view, hides views behind a `HideBehindViews` page, and toggles raycast
  blocking per view. Per-view input can be locked with `SetInputEnable`.
- **Back button & orientation** — the focused view receives the Escape/back
  button (auto-closes if unhandled); `ViewManager.OnOrientationChange` fires on
  screen rotation.
- **Addressables** — views load from Addressables using the key
  `{TypeName}@View` (override the path with `[ViewLoad("…")]`). `BundleUtility`
  wraps load/instantiate/release with automatic handle cleanup.
- **UI animation** — `UIAnimation` samples legacy `AnimationClip`s manually
  (timescale-independent) and dispatches animation events by reflection;
  `AnimationSet` pairs fold/unfold clips for open/close.

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
- `com.unity.addressables` (1.19.19)

## Usage

A `ViewManager` and a `ViewRootCanvas` must exist in the scene (the root canvas
prefab is loaded from `Resources/ViewRootCanvas`). Then:

```csharp
using ViewSystem;

// Define a view. The Addressables key defaults to "SettingsView@View".
public class SettingsView : PageView, IViewInitialFocusListener
{
    public void OnViewInitialFocus() { /* Start() replacement */ }
}

// Open it (awaitable or yieldable).
await ViewRequest.Open<SettingsView>(view => { /* configure before show */ });

// A view that returns a result.
public class ConfirmView : ModalView, IViewResult<bool>
{
    public void OnYes() => this.Success(true);
    public void OnNo()  => this.Complete();   // cancelled
}

var req = ViewRequest<bool>.Open<ConfirmView>();
await req;
if (req.TryGetResult(out var ok) && ok) { /* confirmed */ }
```

## Layout

```
Runtime/Core/    Dakgg.UI         — ViewManager, ViewBase, ModalView, PageView,
                                    ViewLayer, ViewRequest, OverlayView, …
Runtime/Utils/                    — BundleUtility, UIAnimation, UIVisiblity, …
Editor/          Dakgg.UI.Editor  — editor-only assembly
```

## License

[MIT](LICENSE.md)
