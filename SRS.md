## App Overview & Goal

- **Name Suggestion:** DxfLaserNester (or similar)
- **Purpose:**  
    A lightweight, client-side web utility to ingest multiple individual DXF part files, arrange and nest them to minimize a bounding box, assign cut/edge layers and colors, and export a single master DXF ready for Lightburn.
- **Hosting:**  
    Static deployment via GitHub Pages (Blazor WASM).

## Tech Stack

 - **Framework:** Blazor WebAssembly (.NET)
 - **DXF Processing:** netDxf NuGet package (runs entirely client-side in browser memory)
 - **UI / Styling:**  
   Standard Blazor Razor components + a responsive HTML5 Canvas (`<canvas>`) wrapper for 2D rendering and manipulation

## Architecture
- Prefer small components over large ones, if possible.
- Organize the components into logical folders, and use the same naming convention for the files as the components.
- Prefer code-behind files instead of code blocks at the bottom of components.
- Prefer local style sheets instead of global styles, when it makes sense.

## UI layout
- The UI is primarily the canvas, where the dxf files are displayed
- There is a slim vertical sidebar on the left, which contain tools, initialy just a mirror tool. This tool just has the letter "M" on it initially, later I will swap it out with a custom icon.
- There is a vertical sidebar on the right, which contains layer management, creating new layers is done here. Also in this bar is a selection panel, which shows all dxf files imported into the workspace. Selecting a dxf block in this panel will select and highlight it on the canvas.
- There is a thin horizontal toolbar at the bottom, which shows buttons with the different layers (i.e. their colours) to assign a selected dxf part to a layer.
- There is a thin horizontal toolbar at the top, which can manage export and potentially other options in the future.

## Features
1. The user can drag and drop a dxf file onto the canvas to add it to the workspace.
2. The user can drag and drop multiple dxf files onto the canvas to add them to the workspace in one go. They should be spread out evenly across the canvas.
3. The user can drag a dxf block on the canvas to move it.
4. There should be a handle to rotate a selection of elements
5. The user can select pieces of dxf elements by clicking on them, it should be clear which element or elements are selected
6. The user can hold shift to select multiple elements one by one
7. The user can drag a box around elements to select them
8. The user can mirror a selection of elements horizontally by clicking a button on the toolbar.

## Functional Requirements & Tasks (Phased Breakdown)

**Phase 1: Project Initialization & File Ingestion**
- Initialize Blazor WASM project and add the netDxf NuGet package.
- Implement a file upload dropzone component supporting multiple `.dxf` file selections.
- Parse uploaded byte streams into `DxfDocument` objects, automatically exploding any blocks into raw entities (lines, arcs, polylines).

**Phase 2: The 2D Canvas Viewport & State Management**
- Build a state container to track "PlacedParts" (position X/Y, rotation angle, mirror status, assigned layer/color).
- Render imported parts onto an HTML5 Canvas using C# to JS interop or a Blazor-driven canvas loop.
- Implement pan and zoom controls for the canvas workspace.

**Phase 3: Transformations (Move, Rotate, Mirror)**
- Implement click-to-select logic for parts on the canvas or via a sidebar parts list.
- Add transformation controls:
    - Translate (drag-and-drop or precise numeric coordinate entry)
    - Rotate (90-degree step buttons or custom angle)
    - Mirror (horizontal/vertical flip)

**Phase 4: Layer & Color Assignment**
- Create a layer configuration panel (e.g., Default layers: Cut = Red, Edge = Blue).
- Allow assigning selected parts to specific layers, ensuring entity colors are set to ByLayer so Lightburn picks them up instantly.
- Add a bounding box calculator that displays the total dimensions of your nested sheet to help minimize material waste.

**Phase 5: Master DXF Export & GitHub Pages Deployment**
- Combine all transformed parts into a single master `DxfDocument`.
- Generate a client-side file download trigger for the resulting `.dxf` file.
- Configure GitHub Actions workflow to automatically build and deploy the Blazor WASM output to GitHub Pages.