using System;

namespace WWA.Core.Models
{
    /// <summary>
    /// A single cut item request.
    /// Length and optional Width are in inches. Quantity must be >= 1.
    /// </summary>
    public class CutItem
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Length in inches (required, > 0).
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// Optional width in inches (nullable). If null, any board width is acceptable.
        /// </summary>
        public double? Width { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// How many identical pieces are requested. Must be >= 1.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// If true, the item may be rotated when cutting (length/width may be swapped).
        /// </summary>
        public bool AllowRotated { get; set; } = false;

        public CutItem() { }

        public CutItem(double length, double? width = null, int quantity = 1, bool allowRotated = false, string? description = null)
        {
            Length = length;
            Width = width;
            Quantity = quantity;
            AllowRotated = allowRotated;
            Description = description;
        }

        /// <summary>
        /// Validates dimensions and quantity; throws ArgumentException on invalid data.
        /// </summary>
        public void Validate()
        {
            if (Length <= 0) throw new ArgumentException("Length must be positive", nameof(Length));
            if (Width.HasValue && Width.Value <= 0) throw new ArgumentException("Width must be positive if specified", nameof(Width));
            if (Quantity <= 0) throw new ArgumentException("Quantity must be at least 1", nameof(Quantity));
        }

        public override string ToString()
            => $"CutItem(Id={Id}, Length={Length}, Width={(Width?.ToString() ?? "n/a")}, Qty={Quantity}, Rot={AllowRotated})";
    }
}