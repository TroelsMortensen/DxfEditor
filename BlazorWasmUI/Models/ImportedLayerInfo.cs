namespace BlazorWasmUI.Models;

public sealed class ImportedLayerInfo
{
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public int EntityCount { get; init; }
}
