Build a lightweight C# desktop application (.NET 8/9, preferably WPF or WinForms) to layout and color-code DXF files for laser cutting.

Requirements:

Use the netDxf NuGet package for all DXF parsing and exporting.

Import: Allow importing multiple DXF files simultaneously. Automatically parse entities (lines, arcs, circles, lightweights polylines) and explode any blocks into individual components.

Canvas UI: Provide an interactive 2D canvas workspace where imported parts are rendered as vectors.

Transformations: Allow clicking a part to select it, and provide controls/shortcuts to Translate (move), Rotate (e.g., 90-degree steps), and Mirror (across X/Y axes).

Layers & Colors: Include a layer panel where I can assign parts/entities to specific layers with custom line colors (e.g., Red for 'Cut', Blue for 'Edge/Score') ensuring colors are saved 'ByLayer'.

Export: Combine all placed, transformed, and color-coded parts into a single master DXF file and save it via a file dialog.