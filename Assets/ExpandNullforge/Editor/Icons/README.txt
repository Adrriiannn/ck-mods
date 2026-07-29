Section icons — drop-in overrides
=================================

The dashboard's left "SECTIONS" nav draws smooth, code-generated icons by default.
To replace any of them with a real high-quality image, drop a PNG in THIS folder named
after the section id. It is picked up automatically (full colour, no tint) and overrides
the generated one.

File names (exact):
  overview.png      dimension.png     portals.png       tilesets.png
  layout.png        biomes.png        terrain.png       generation.png
  scenes.png        resources.png     spawns.png        export.png
  diagnostics.png

Tips:
- Square images work best (they're fit into a ~20px slot). 64x64 or 128x128 is plenty.
- Set the texture's Alpha Is Transparency = on in the import settings for clean edges.
- White/monochrome icons will show in full white; for the blue tint the nav uses, author
  them white-on-transparent (the generated ones already do this).

Great free source that has every icon this dashboard uses (binoculars, portal, trees,
mountain, monster face, sword & shield, etc.): game-icons.net (CC-BY). Download the PNG,
rename it to the id above, drop it here.
