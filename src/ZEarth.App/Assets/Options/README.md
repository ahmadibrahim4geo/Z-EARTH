# Options illustrative thumbnail assets

These images are original, provider-neutral UI illustrations generated for the Z EARTH
Options window on 2026-10-28 with the built-in OpenAI image generation tool. They are
not cached map-provider tiles, not evidence that a hosted source is online, and not a
substitute for required runtime attribution.

## Files and prompt set

- `basemap-thumbnail-atlas.png` — a 3×3 atlas whose first seven cells depict, in
  reading order, satellite, hybrid satellite/roads, street map, topographic map,
  light-gray reference map, dark reference map, and pastel physical-world map.
- `terrain-thumbnail-atlas.png` — a 3×3 atlas whose first seven cells depict, in
  reading order, alpine DEM, canyon terrain, blue bathymetry, scientific rainbow
  bathymetry, smooth WGS84 globe, grayscale local hillshade, and rugged custom terrain.
- `map-grid-preview.png` — a clean wide equirectangular world-map background with
  no geographic, coordinate or UTM lines baked into the bitmap. The Options dialog
  draws all preview lines and labels as live WPF overlays bound to the current
  visibility switches and color pickers.

All three generation prompts required: no text, numbers, logos, trademarks,
watermarks, provider branding, UI controls, or map pins. The cleaned map-background
edit additionally required removing every colored line and marker while reconstructing
the underlying land, ocean and ice. The application crops atlas
cells with WPF `ImageBrush.Viewbox`; provider names, status, licensing truth, and
attribution remain ordinary accessible UI text outside the images.
