# Portal item icons — size & import reference

Core Keeper items use **two** sprites. Both are tiny pixel-art PNGs with a transparent
background. Verified against the game's own items (e.g. ConveyorTunnelMod) and CoreLib's
authoring tooltips.

| Sprite | Canvas | Used for |
|--------|--------|----------|
| **Inventory icon** (`iconSprite`) | **16 × 16 px** | The item in inventory / chest / tooltip |
| **Small icon** (`smallIconSprite`) | **10 × 10 px** | In the player's hand while held, and on the cursor while dragging |

The held/cursor sprite is deliberately smaller than the inventory icon — that is why Core
Keeper ships a separate ~10×10 "in-hand" version rather than scaling the 16×16 down.

## Texture import settings (all icons)

Select the PNG in Unity and set the importer to:

- **Texture Type:** Sprite (2D and UI)
- **Sprite Mode:** Single
- **Pixels Per Unit:** 16
- **Filter Mode:** Point (no filter)  ← keeps pixels crisp
- **Compression:** None
- **Alpha Is Transparency:** on
- Pivot: Center (default)

(Equivalent raw values in the `.meta`: `filterMode: 0`, `spriteMode: 1`,
`spritePixelsToUnits: 16`, `textureType: 8`, `alphaIsTransparency: 1`.)

## Making these the framework default

Drop the finished art in **this folder** with these exact names and every newly
auto-created portal item will start with them (you can still override per item):

- `PortalItemIcon.png`         — 16 × 16 inventory icon
- `PortalItemIcon_inHand.png`  — 10 × 10 small / in-hand icon

If a file is absent the portal item is simply created without that icon.
