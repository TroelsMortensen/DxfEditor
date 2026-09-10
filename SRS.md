## App Overview & Goal

- **Name:** Dxf Editor
- **Purpose:**  
  A lightweight, client-side web utility to ingest multiple individual DXF part files, arrange and nest them to minimize a bounding box, assign cut/edge layers and colors, and export a single master DXF ready for Lightburn.
- **Hosting:**  
  Static deployment via GitHub Pages (Blazor WASM). *(not yet configured)*
- **Status:** Phases 1–4 (layers/colors) complete except bounding-box readout. Export and GitHub Pages remain.

## Tech Stack

- **Framework:** Blazor WebAssembly (.NET 10)
- **DXF Processing:** netDxf (client-side). Uploads are buffered into a seekable `MemoryStream` (no filesystem paths). Pre-2000 ASCII DXFs (e.g. Fusion 360 R14 / AC1014) are header-upgraded to AC1015; if netDxf still fails, a legacy ASCII fallback parses common entities including `LWPOLYLINE`.
- **UI / Styling:** Blazor Razor components + HTML5 `<canvas>` with a dedicated JS `requestAnimationFrame` handler (`wwwroot/js/dxfCanvas.js`). C# owns workspace state; JS owns smooth gesture redraw and commits transforms/selection back to Blazor.

## Architecture

- Prefer small components over large ones.
- Organize components into logical folders; match file names to component names.
- Prefer code-behind (`.razor.cs`) over inline `@code` blocks.
- Prefer local stylesheets (`.razor.css`) over global styles when it makes sense.
- Current layout: `Models/` (`PlacedPart`, `PartEntity`, layers/scene DTOs), `Services/` (`WorkspaceState`, `DxfImportService`), `Components/Editor/` (`EditorShell`, `DxfCanvas`, `LeftToolsPanel`, `PartsListPanel`, `LayerBarPanel`, `LayerEditPanel`, `InfoHelpModal`).

## UI layout

- **Canvas (center):** primary surface for DXF parts; drop target for `.dxf` files; pan/zoom/select/transform.
- **Info button:** 50×50 overlay at canvas top-left (`img/info.svg`); opens a scrollable help modal describing toolbar, layers, parts list, layer bar, and canvas gestures.
- **Left tools rail:** horizontal mirror, vertical mirror (same icon rotated 90°), duplicate, delete; import at the bottom. Icons live under `wwwroot/img/`.
- **Right sidebar:** layer editor (list + add from palette) above parts list; selecting a part row selects/highlights it on the canvas.
- **Top bar:** app title only for now; export actions reserved for Phase 5.
- **Bottom bar:** layer color buttons; click assigns the current block or entity selection to that layer.
- **Rotate handle:** selection chrome includes a rotate handle using `img/rotate.svg`.

## Features

| # | Feature | Status |
|---|---------|--------|
| 1 | Drag and drop a DXF onto the canvas | Done |
| 2 | Multi-file drop; spread parts evenly | Done |
| 3 | Drag a selected part (or multi-selection) to move | Done |
| 4 | Rotate handle on selection | Done |
| 5 | Click to select block; empty click / Escape clear | Done |
| 6 | Shift+click multi-select blocks; Ctrl+click multi-select entities | Done |
| 7 | Marquee (box) select blocks | Done |
| 8 | Mirror selection horizontally (toolbar) | Done |
| 9 | Mirror selection vertically (toolbar) | Done |
| 10 | Delete selection (toolbar + Delete/Backspace, with confirm) | Done |
| 11 | Pan (middle-mouse / Space+drag, grabbing cursor) and wheel zoom | Done |
| 12 | Parts list selection sync with canvas | Done |
| 13 | Layer / color assignment (per block or per entity) | Done |
| 14 | Bounding-box / sheet size readout | Not started (Phase 4) |
| 15 | Master DXF export download | Not started (Phase 5) |
| 16 | GitHub Pages deploy workflow | Not started (Phase 5) |
| 17 | In-app help modal (info button on canvas) | Done |
| 18 | Duplicate selection (toolbar offsets ~50px; Ctrl/Cmd+D places at cursor; unique `(n)` names in Parts list) | Done |

## Functional Requirements & Tasks (Phased Breakdown)

**Phase 1: Project Initialization & File Ingestion** — Done
- Blazor WASM project + netDxf package.
- Canvas dropzone for multiple `.dxf` files (JS `File` → base64 → C# `MemoryStream`).
- Parse into geometry; explode inserts/blocks; normalize part origin to local bbox center.
- R14 / pre-2000 ASCII support via header upgrade + legacy entity fallback.

**Phase 2: The 2D Canvas Viewport & State Management** — Done
- `WorkspaceState` tracks placed parts (position, rotation, mirror) and selection.
- JS-owned rAF canvas loop; Blazor pushes scene snapshots and receives commit callbacks.
- Pan and zoom (wheel toward cursor).

**Phase 3: Transformations (Move, Rotate, Mirror, Duplicate, Delete)** — Done
- Click selects a DXF block; Shift+click / marquee multi-select blocks; parts list selection.
- Ctrl+click selects individual entities (line/arc/circle/…) for layer assignment; Escape clears selection.
- Move / rotate / mirror / duplicate / delete always apply to whole parent block(s), keeping each import cohesive.
- Duplicate deep-copies selected blocks and adds uniquely named `(n)` entries to the Parts list. Toolbar offsets copies slightly; Ctrl/Cmd+D places the selection’s world bounding-box center at the mouse cursor.
- *(Precise numeric coordinate entry not implemented — deferred.)*

**Phase 4: Layer & Color Assignment** — Mostly done
- Layer configuration panel in the right sidebar (list + add from 10 distinct palette colors).
- Bottom color bar assigns selected blocks (all entities) or selected entities to a layer; strokes use per-entity layer color.
- Import preserves each entity’s source color into the workspace palette and assigns per-entity layers.
- Bounding box / nested sheet dimensions readout — not started.

**Phase 5: Master DXF Export & GitHub Pages Deployment** — Not started
- Combine transformed parts into one master `DxfDocument`.
- Client-side `.dxf` download.
- GitHub Actions → GitHub Pages for the Blazor WASM publish output.
