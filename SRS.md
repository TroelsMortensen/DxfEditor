## App Overview & Goal

- **Name:** Dxf Editor
- **Purpose:**  
  A lightweight, client-side web utility to ingest multiple individual DXF part files, arrange and nest them to minimize a bounding box, assign cut/edge layers and colors, and export a single master DXF (one BLOCK+INSERT per part) ready for LightBurn and for re-import as separate repositionable parts.
- **Hosting:**  
  Static deployment via GitHub Pages (Blazor WASM).
- **Status:** Phases 1–5 complete (layers/colors, master DXF export, GitHub Pages), including workspace bounding-box overlay (Phase 4.5). Remaining: Phase 6 backlog (export origin, undo/redo, rename, SVG import).

## Tech Stack

- **Framework:** Blazor WebAssembly (.NET 10)
- **DXF Processing:** netDxf (client-side). Uploads are buffered into a seekable `MemoryStream` (no filesystem paths). Pre-2000 ASCII DXFs (e.g. Fusion 360 R14 / AC1014) are header-upgraded to AC1015; if netDxf still fails, a legacy ASCII fallback parses common entities including `LWPOLYLINE`.
- **UI / Styling:** Blazor Razor components + HTML5 `<canvas>` with a dedicated JS `requestAnimationFrame` handler (`wwwroot/js/dxfCanvas.js`). C# owns workspace state; JS owns smooth gesture redraw and commits transforms/selection back to Blazor.

## Architecture

- Prefer small components over large ones.
- Organize components into logical folders; match file names to component names.
- Prefer code-behind (`.razor.cs`) over inline `@code` blocks.
- Prefer local stylesheets (`.razor.css`) over global styles when it makes sense.
- Current layout: `Models/` (`PlacedPart`, `PartEntity`, layers/scene DTOs), `Services/` (`WorkspaceState`, `DxfImportService`, `DxfExportService`), `Components/Editor/` (`EditorShell`, `DxfCanvas`, `LeftToolsPanel`, `PartsListPanel`, `LayerBarPanel`, `LayerEditPanel`, `InfoHelpModal`).

## UI layout

- **Canvas (center):** primary surface for DXF parts; drop target for `.dxf` files; pan/zoom/select/transform.
- **Info button:** 50×50 overlay at canvas top-left (`img/info.svg`); opens a scrollable help modal describing toolbar, layers, parts list, layer bar, and canvas gestures.
- **Left tools rail:** horizontal mirror, vertical mirror (same icon rotated 90°), duplicate, delete; import and export at the bottom. Icons live under `wwwroot/img/`.
- **Right sidebar:** layer editor (list + add from palette) above parts list; selecting a part row selects/highlights it on the canvas.
- **Top bar:** app title only for now.
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
| 12 | Parts list selection sync with canvas (Ctrl/Cmd+click toggle; Shift+click range) | Done |
| 13 | Layer / color assignment (per block or per entity) | Done |
| 14 | Workspace bounding box: thin grey outline around all parts, with width×height in mm | Done |
| 15 | Master DXF export (BLOCK+INSERT per part) with round-trip re-import | Done |
| 16 | GitHub Pages deploy workflow | Done |
| 17 | In-app help modal (info button on canvas) | Done |
| 18 | Duplicate selection (toolbar offsets ~50px; Ctrl/Cmd+D places at cursor; unique `(n)` names in Parts list) | Done |
| 19 | Export origin normalization (investigate LightBurn placement; shift/normalize if needed) | Not started (Phase 6) |
| 20 | Undo / redo | Not started (Phase 6) |
| 21 | Rename parts in the Parts list | Not started (Phase 6) |
| 22 | SVG import (convert to DXF entities on import or at export) | Not started (Phase 6) |

## Functional Requirements & Tasks (Phased Breakdown)

**Phase 1: Project Initialization & File Ingestion** — Done
- Blazor WASM project + netDxf package.
- Canvas dropzone for multiple `.dxf` files (JS `File` → base64 → C# `MemoryStream`).
- Parse into geometry; top-level `INSERT`s become separate parts (nested inserts exploded); flat files (no inserts) remain one part; normalize part origin to local bbox center.
- R14 / pre-2000 ASCII support via header upgrade + legacy entity fallback.

**Phase 2: The 2D Canvas Viewport & State Management** — Done
- `WorkspaceState` tracks placed parts (position, rotation, mirror) and selection.
- JS-owned rAF canvas loop; Blazor pushes scene snapshots and receives commit callbacks.
- Pan and zoom (wheel toward cursor).

**Phase 3: Transformations (Move, Rotate, Mirror, Duplicate, Delete)** — Done
- Click selects a DXF block; Shift+click / marquee multi-select blocks; parts list: Ctrl/Cmd+click toggles, Shift+click selects a contiguous range.
- Ctrl+click selects individual entities (line/arc/circle/…) for layer assignment; Escape clears selection.
- Move / rotate / mirror / duplicate / delete always apply to whole parent block(s), keeping each import cohesive.
- Duplicate deep-copies selected blocks and adds uniquely named `(n)` entries to the Parts list. Toolbar offsets copies slightly; Ctrl/Cmd+D places the selection’s world bounding-box center at the mouse cursor.
- *(Precise numeric coordinate entry not implemented — deferred.)*

**Phase 4: Layer & Color Assignment** — Done
- Layer configuration panel in the right sidebar (list + add from 10 distinct palette colors).
- Bottom color bar assigns selected blocks (all entities) or selected entities to a layer; strokes use per-entity layer color.
- Import preserves each entity’s source color into the workspace palette and assigns per-entity layers.

**Phase 4.5: Workspace Bounding Box Overlay** — Done
- Draw a thin grey axis-aligned bounding box around all parts currently on the canvas (union of world-space part bounds).
- Show dimensions in mm (width × height) on or near the box; update live as parts move, rotate, mirror, duplicate, delete, or import.
- No fixed “sheet stock” frame for now—this is a readout of the nested layout extent, not a material template.

**Phase 5: Master DXF Export & GitHub Pages Deployment** — Done
- Export each placed part as a named `BLOCK` (local geometry) plus `INSERT` (offset, rotation, mirror via negative X scale) in one master `DxfDocument`.
- Re-importing that master restores separate repositionable parts and layer colors; already-flattened DXFs (no inserts) still import as one part per file.
- Client-side `.dxf` Save As (File System Access API) with download fallback.
- GitHub Actions → GitHub Pages for the Blazor WASM publish output.
- *LightBurn acceptance of block-structured masters is unverified on this branch; flatten export can be restored if needed.*

**Phase 6: Backlog** — Not started
- **Export origin normalization:** Investigate how LightBurn places a DXF whose geometry sits far from the origin (e.g. all parts off to one side). If LightBurn does not auto-center usefully, normalize on export (e.g. shift so the workspace bbox min or center maps to a predictable origin). Document the chosen behavior.
- **Undo / redo:** Essential for iterative nesting. Expect this to grow complicated—prefer a clear command/snapshot model early (what is undoable: move, rotate, mirror, duplicate, delete, layer assign, rename, import) and keep history bounded. Keyboard shortcuts Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z (or Ctrl/Cmd+Y).
- **Rename parts:** Allow renaming entries in the Parts list (inline edit or dialog); keep names unique or disambiguate; selection sync unchanged.
- **SVG import:** Accept `.svg` drops/picks alongside `.dxf`. Convert paths/shapes to DXF-compatible entities either at import (into workspace geometry) or deferred at master export—decide based on edit fidelity vs. complexity. Closed paths should become polylines/lwpolylines suitable for cutting.
