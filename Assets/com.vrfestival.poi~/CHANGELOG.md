# Changelog

## [1.0.4] - 2026-08-18

- Added Japanese custom Inspectors for paper physics, frame appearance, and Poi configuration
- Added beginner-friendly labels, tooltips, guidance, and fragile/standard/durable paper presets
- Documented how each setting affects drying, collision durability, wet strength, and water-movement resistance
- Kept serialized field names unchanged for compatibility with existing settings assets

## [1.0.3] - 2026-08-18

- Detected water-damage motion when XR Interaction Toolkit uses Kinematic or Instantaneous movement
- Accounted for both linear and angular Transform motion while the paper is submerged
- Added validation for `XRGrabInteractable` placement on `PoiRoot` instead of the child `Handle`
- Documented the supported XR Interaction Toolkit setup

## [1.0.2] - 2026-08-18

- Fixed Basic Sample controls in Input System-only projects without adding a hard Input System dependency
- Added conditional URP SubShaders for paper, water surface, ripple, frame and handle materials
- Kept Built-in Render Pipeline support and documented automatic and optional package requirements

## [1.0.1] - 2026-08-18

- Added noise-based automatic dissolve and cleanup for detached paper fragments
- Added configurable fragment lifetime and dissolve duration
- Added Grid Resolution performance warnings and Grid/Visual Mesh Inspector guidance
- Added area-based connected-component cleanup for tiny visual mesh islands

## [1.0.0] - 2026-08-18

- Dynamic paper damage and procedural tear
- Physics impact and continuous load damage
- Detached unsupported paper islands
- Cell-based wetness, diffusion and drying
- Wetness-based strength and paper rendering
- Water volume, depth and velocity API
- Water movement damage
- Lightweight water surface, ripple and splash
- Package-independent VR grab API
- Configurable paper and external Frame model settings
- Basic Sample
