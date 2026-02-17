# Stylized Hand Rendering Plan (VR-like Ghost Hands)

## Goal
Render tracked hand movement with a visual style similar to the reference image:
- soft/semi-transparent "ghost" hands,
- smooth silhouette,
- subtle glow/fog look,
- optional UI-space composition feel.

## Feasibility
**Yes, it is feasible with the current architecture.**

The app already has the key pieces:
- hand landmarks from `MediaPipeHandTracker`,
- per-frame composition in `FrameRenderer`,
- camera pipeline in `MainWindow` + `CameraCaptureService`.

This means the change is mostly in the rendering stage and does not require replacing camera or tracker services.

## Proposed implementation approach

### 1) Build a stable hand mesh from landmarks
Current renderer fills a generic convex polygon from all points. To get a hand-like shape, construct a palm + fingers mask using landmark groups:

- Palm contour from landmarks (wrist + MCP joints), e.g. indices:
  - `0 (wrist), 1, 2, 5, 9, 13, 17`
- Finger segments with variable thickness:
  - Thumb: `1-2-3-4`
  - Index: `5-6-7-8`
  - Middle: `9-10-11-12`
  - Ring: `13-14-15-16`
  - Pinky: `17-18-19-20`

Practical method:
- Draw thick anti-aliased polylines/capsules for each finger chain.
- Fill palm polygon.
- Union all of them into a binary mask (single channel `Mat`).

### 2) Convert hard mask to soft silhouette
To mimic the reference style:
- Apply `GaussianBlur` to the mask edges.
- Optionally run morphological close/open to reduce jitter holes.
- Create two layers:
  - Inner body (higher alpha, smoother fill)
  - Outer halo (bigger blur, low alpha)

### 3) Color grading and compositing
Use cool gray-blue tones (instead of bright cyan) and tune opacity:
- Inner hand tint: near `#8E9BAF`
- Halo tint: near `#C7D4E8`
- Alpha range: `0.25 - 0.55` depending on confidence or depth

Composite order per frame:
1. Background camera frame
2. Halo layer
3. Hand body layer
4. Landmark highlights (optional minimal white dots)

### 4) Temporal smoothing (important)
To avoid "nervous" shape movement:
- Smooth landmark positions with EMA per point:
  - `smoothed = alpha * current + (1 - alpha) * previous`
  - start with `alpha = 0.45`
- Keep one state per hand (left/right association via handedness + nearest-wrist matching).

### 5) Optional depth-like effect
If `Z` is available later from MediaPipe (or inferred from hand scale), modulate:
- blur radius,
- opacity,
- hand scale accent,
so nearer hands appear clearer/brighter.

### 6) Runtime settings
Add a small render settings model (for tuning without touching code repeatedly):
- `EnableGhostStyle`
- `BodyOpacity`
- `HaloOpacity`
- `BlurSigma`
- `LandmarkSize`
- `SmoothingAlpha`

## Integration points in this repository
- Main work: `src/HandTrackingWpfApp/Services/FrameRenderer.cs`
- Minor model extension (if needed for smoothing/depth metadata):
  - `src/HandTrackingWpfApp/Models/HandModels.cs`
- Optional UI controls for tuning:
  - `src/HandTrackingWpfApp/MainWindow.xaml`
  - `src/HandTrackingWpfApp/MainWindow.xaml.cs`

## Suggested delivery in phases
1. **Phase A:** Hand mesh + soft silhouette + new palette.
2. **Phase B:** Landmark EMA smoothing + stability tuning.
3. **Phase C:** Optional depth modulation and UI sliders.

## Risks and mitigations
- **Landmark jitter:** mitigate with EMA + contour smoothing.
- **CPU usage at high FPS:** reuse buffers and keep blur kernels moderate.
- **Occlusion artifacts:** clip outside frame and enforce mask validity checks.

## Acceptance criteria
- Two hands rendered simultaneously with stable silhouettes.
- Visual style clearly closer to ghost/VR hands than current flat overlay.
- No major frame drops on target machine at configured capture settings.
