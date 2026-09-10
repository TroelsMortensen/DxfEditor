namespace BlazorWasmUI.Models;

public sealed class LayerDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ColorHex { get; init; }
}
