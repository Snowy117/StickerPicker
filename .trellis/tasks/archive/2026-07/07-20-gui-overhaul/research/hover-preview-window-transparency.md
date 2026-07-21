# Hover-preview window transparency (Avalonia 12.1 / Win32)

## Scope and source baseline

- StickerPicker references Avalonia `12.1.0` in
  `src/StickerPicker/StickerPicker.csproj`.
- Avalonia was shallow-cloned to `/tmp/avalonia-hover-transparency`, then
  pinned to tag `12.1.0`, commit
  `a21b9f573172f705a944dcc8aad7f036b9986f39`.
- This is source research only. No production code was changed and no Windows
  runtime smoke test was possible from this Linux session.

## Findings

### High — the preview never requests a transparent top-level

`src/StickerPicker/Views/MainWindow.HoverPreview.cs:225` creates a second
`Window` with `Background=Transparent`, but does not set
`TransparencyLevelHint`.

Those properties are not interchangeable:

- `Background=Transparent` only tells Avalonia not to paint the Window
  template's background brush.
- `TransparencyLevelHint=[WindowTransparencyLevel.Transparent]` asks the
  platform backend for an alpha-composited top-level surface.

This distinction is explicit in Avalonia source:

- `src/Avalonia.Controls/WindowTransparencyLevel.cs:18` documents
  `Transparent` as making undrawn window background transparent.
- `src/Avalonia.Controls/TopLevel.cs:241` forwards the hint to the platform.
- `src/Avalonia.Controls/TopLevel.cs:725` maps the achieved platform level to
  the composition target and removes the theme fallback only when the level
  is not `None`.
- `src/Avalonia.Themes.Fluent/Controls/Window.xaml:14` contains the
  `PART_TransparencyFallback` border; `Window.xaml:15` separately paints the
  `Window.Background`. A transparent background therefore does not defeat the
  fallback used when the actual top-level level is `None`.

With StickerPicker's default Windows setting, `Program.cs:41` selects only
`Win32RenderingMode.Software`. Avalonia then takes this path:

1. `src/Windows/Avalonia.Win32/Win32GlManager.cs:44` returns no platform GPU
   graphics object for `Software`.
2. `src/Windows/Avalonia.Win32/WindowImpl.cs:147` consequently sets
   `UseRedirectionBitmap=true`.
3. `WindowImpl.cs:182` makes the default transparency level `None` for that
   redirection path.
4. If `Transparent` is explicitly requested, `WindowImpl.cs:424` supports it
   on Windows 8+, and `WindowImpl.cs:440` routes it to legacy transparency.
5. `WindowImpl.cs:480` enables a whole-window DWM blur-behind region with
   `DwmEnableBlurBehindWindow`; Avalonia describes this as its Win8+ fallback
   for true transparency when WinUI/DirectComposition is unavailable.

Therefore software rendering does support Avalonia transparent top-levels on
Windows 8+, but it is opt-in. A transparent brush alone leaves the preview at
`ActualTransparencyLevel=None` and the theme fallback occludes other apps.

### Medium — opacity is currently applied at the wrong semantic level

`MainWindow.HoverPreview.cs:57` and `MainWindow.HoverPreview.cs:208` apply the
setting to the child `Image.Opacity`. For a "preview window opacity" setting,
apply it to `Window.Opacity` instead and leave the image at its natural opacity
of `1`.

Avalonia's opacity is a visual-composition operation:

- `src/Avalonia.Base/Visual.cs:64` defines `Opacity` on every `Visual`.
- `src/Avalonia.Base/Visual.Composition.cs:140` transfers it to the
  composition visual.
- `src/Avalonia.Base/Rendering/Composition/Server/ServerCompositionVisual/ServerCompositionVisual.Render.cs:95`
  pushes that opacity over the visual subtree.

The placement determines which subtree is faded:

- `Image.Opacity`: fades only bitmap pixels. The border remains fully opaque,
  and any Window-template fallback remains behind the image.
- `Border.Opacity`: fades image plus preview border, but not the Window
  template/fallback outside that content subtree.
- `Window.Opacity`: fades the complete preview top-level visual subtree,
  including image and border. This best matches the persisted setting's name
  and avoids partially opaque chrome.

`Window.Opacity` is not a substitute for `TransparencyLevelHint`: it changes
Avalonia-rendered alpha, while the transparency level determines whether the
OS top-level surface can composite that alpha against windows behind it.

### Why child opacity looks washed out

Without an achieved transparent top-level, the image is blended against
pixels already inside the preview HWND, not against another application's
pixels. Conceptually the result is:

`shownColor = imageOpacity * imageColor + (1 - imageOpacity) * fallbackColor`

The fallback comes from the Window theme when
`ActualTransparencyLevel=None`. Lowering only `Image.Opacity` therefore mixes
the sticker with that light/dark fallback (often perceived as faded,
washed-out, or grey) while the rectangular HWND continues to cover the app
behind it. Transparent child backgrounds merely expose the next ancestor
surface; they do not punch through an opaque native top-level.

### Informational — decorations and native implementation

- `WindowDecorations=None` does not itself enable alpha transparency. It is
  still correct for the hover preview because it removes native titlebar and
  frame pixels that are not part of the desired sticker surface.
- Avalonia 12.1's normal Win32 top-level transparency path does not use
  `WS_EX_LAYERED` / `UpdateLayeredWindow`. The top-level implementation uses
  DWM transparency for the software/redirection case. StickerPicker should
  use the public Avalonia API rather than add Win32 P/Invoke.
- The software framebuffer is BGRA with premultiplied alpha
  (`src/Windows/Avalonia.Win32/FramebufferManager.cs:50`) and is copied to the
  window DC (`FramebufferManager.cs:127`). This is compatible with the DWM
  path selected by the transparency hint.

## Upstream executable evidence

Avalonia's own transparent-window sample uses the exact relevant trio in
`samples/IntegrationTestApp/Pages/WindowPage.axaml.cs:86`:

```csharp
WindowDecorations = WindowDecorations.None,
Background = Brushes.Transparent,
TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
```

It draws a red circular child over a green owner window. The Appium assertion
in `tests/Avalonia.IntegrationTests.Appium/WindowTests.cs:214` verifies that a
corner of the transparent child window shows the owner's green pixels while
the center remains red. This test is evidence for the public property
combination; the test app uses Avalonia's default rendering preference, so it
is not an explicit software-only runtime test.

## Recommended StickerPicker implementation

Keep the preview as a separate owned, topmost, non-activating Window, but use
this rendering contract:

```csharp
_hoverPreviewImage = new Image
{
    Stretch = Stretch.Uniform,
};

_hoverPreviewWindow = new Window
{
    WindowDecorations = WindowDecorations.None,
    TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
    Background = Brushes.Transparent,
    Opacity = previewOpacity,
    // existing ShowActivated/ShowInTaskbar/Topmost/etc. remain unchanged
};
```

Update live settings through `_hoverPreviewWindow.Opacity = opacity`, not
`_hoverPreviewImage.Opacity`. Keep the content `Border.Background` transparent
unless an intentional preview-card fill is desired; its border brush can stay
visible and will be faded together with the Window.

Do not rely on `TransparencyBackgroundFallback=Transparent` as a replacement
for the hint. If the platform reports `ActualTransparencyLevel=None`, it has
not supplied the required OS-level contract; a transparent fallback brush
cannot make an opaque HWND reveal another process.

## Residual risks and validation

- **Medium:** Avalonia's software implementation comments that the Win8+
  `DwmEnableBlurBehindWindow` fallback is not guaranteed to retain the same
  semantics in a future Windows version (it explicitly calls out possible
  Win12 behavior). Re-test when upgrading Avalonia or Windows.
- **Medium:** The upstream pixel assertion is not forced through
  `Win32RenderingMode.Software`. StickerPicker needs a manual Windows smoke
  with `UseGpuRendering=false`: place the preview over a high-contrast window,
  test opacity `1.0`, `0.5`, and the minimum, and confirm underlying pixels are
  visible rather than a tinted rectangle.
- **Low:** Inspect/log `ActualTransparencyLevel` during that smoke. It should
  become `Transparent`; `None` means the backend declined the request and the
  theme fallback will correctly remain opaque.
- **Low:** `Window.Opacity` fades the border as well as the sticker. If product
  intent is specifically "sticker only" opacity, child opacity is acceptable
  only after the transparent top-level hint is present; the border behavior
  should then be an explicit UX decision rather than an accidental side
  effect.
