namespace WWA.Core.Models
{
    /// <summary>
    /// Represents a single cut item parsed from a simple cut-list text format.
    /// Minimal fields for current tests: Length, Width and optional Description.
    /// </summary>
    public class CutItem
    {
        public string Length { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}