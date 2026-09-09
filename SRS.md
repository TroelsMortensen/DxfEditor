## App Overview & Goal

- **Name:** Dxf Editor
- **Purpose:**  
  A lightweight, client-side web utility to ingest multiple individual DXF part files, arrange and nest them to minimize a bounding box, assign cut/edge layers and colors, and export a single master DXF ready for Lightburn.
- **Hosting:**  
  Static deployment via GitHub Pages (Blazor WASM). *(not yet configured)*
- **Status:** Phases 1–3 complete. Core editor works: import, canvas viewport, select/move/rotate/mirror/delete. Layers, export, and GitHub Pages remain.

## Tech Stack

- **Framework:** Blazor WebAssembly (.NET 10)
- **DXF Processing:** netDxf (client-side). Uploads are buffered into a seekable `MemoryStream` (no filesystem paths). Pre-2000 ASCII DXFs (e.g. Fusion 360 R14 / AC1014) are header-upgraded to AC1015; if netDxf still fails, a legacy ASCII fallback parses common entities including `LWPOLYLINE`.
- **UI / Styling:** Blazor Razor components + HTML5 `<canvas>` with a dedicated JS `requestAnimationFrame` handler (`wwwroot/js/dxfCanvas.js`). C# owns workspace state; JS owns smooth gesture redraw and commits transforms/selection back to Blazor.

## Architecture

- Prefer small components over large ones.
- Organize components into logical folders; match file names to component names.
- Prefer code-behind (`.razor.cs`) over inline `@code` blocks.
- Prefer local stylesheets (`.razor.css`) over global styles when it makes sense.
- Current layout: `Models/`, `Services/` (`WorkspaceState`, `DxfImportService`), `Components/Editor/` (`EditorShell`, `DxfCanvas`, `LeftToolsPanel`, `PartsListPanel`).

## UI layout

- **Canvas (center):** primary surface for DXF parts; drop target for `.dxf` files; pan/zoom/select/transform.
- **Left tools rail:** horizontal mirror, vertical mirror (same icon rotated 90°), delete. Icons live under `wwwroot/img/`.
- **Right sidebar:** parts list of imported DXF files; selecting a row selects/highlights the part on the canvas. *(Layer management UI not built yet — Phase 4.)*
- **Top bar:** app title only for now; export actions reserved for Phase 5.
- **Bottom bar:** reserved for layer color assignment (Phase 4); not shown yet.
- **Rotate handle:** selection chrome includes a rotate handle using `img/rotate.svg`.

## Features

| # | Feature | Status |
|---|---------|--------|
| 1 | Drag and drop a DXF onto the canvas | Done |
| 2 | Multi-file drop; spread parts evenly | Done |
| 3 | Drag a selected part (or multi-selection) to move | Done |
| 4 | Rotate handle on selection | Done |
| 5 | Click to select; clear selection chrome | Done |
| 6 | Shift+click multi-select | Done |
| 7 | Marquee (box) select | Done |
| 8 | Mirror selection horizontally (toolbar) | Done |
| 9 | Mirror selection vertically (toolbar) | Done |
| 10 | Delete selection (toolbar + Delete/Backspace, with confirm) | Done |
| 11 | Pan (middle-mouse / Space+drag) and wheel zoom | Done |
| 12 | Parts list selection sync with canvas | Done |
| 13 | Layer / color assignment | Not started (Phase 4) |
| 14 | Bounding-box / sheet size readout | Not started (Phase 4) |
| 15 | Master DXF export download | Not started (Phase 5) |
| 16 | GitHub Pages deploy workflow | Not started (Phase 5) |

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

**Phase 3: Transformations (Move, Rotate, Mirror, Delete)** — Done
- Click / shift-click / marquee select on canvas; parts list selection.
- Translate by drag; free rotate via handle; horizontal and vertical mirror from left toolbar.
- Delete selected parts via toolbar button or Delete/Backspace, with confirmation prompt.
- *(Precise numeric coordinate entry not implemented — deferred.)*

**Phase 4: Layer & Color Assignment** — Not started
- Layer configuration panel (e.g. Cut = Red, Edge = Blue) in the right sidebar.
- Assign selected parts to layers; entity colors ByLayer for Lightburn.
- Bottom toolbar of layer color buttons.
- Bounding box / nested sheet dimensions readout.

**Phase 5: Master DXF Export & GitHub Pages Deployment** — Not started
- Combine transformed parts into one master `DxfDocument`.
- Client-side `.dxf` download.
- GitHub Actions → GitHub Pages for the Blazor WASM publish output.
