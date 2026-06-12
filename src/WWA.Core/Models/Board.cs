using System;

namespace WWA.Core.Models
{
    /// <summary>
    /// Represents a raw board in inventory. Dimensions are in inches.
    /// </summary>
    public class Board
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Length in inches (must be > 0).
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// Width in inches (must be > 0).
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Thickness in inches (optional).
        /// </summary>
        public double? Thickness { get; set; }

        public string? Grade { get; set; }

        /// <summary>
        /// How many identical boards are available (>= 1).
        /// </summary>
        public int Quantity { get; set; } = 1;

        public Board() { }

        public Board(double length, double width, double? thickness = null, string? grade = null, int quantity = 1)
        {
            Length = length;
            Width = width;
            Thickness = thickness;
            Grade = grade;
            Quantity = quantity;
        }

        /// <summary>
        /// Basic validation for dimensions and quantity.
        /// </summary>
        public void Validate()
        {
            if (Length <= 0) throw new ArgumentException("Length must be positive", nameof(Length));
            if (Width <= 0) throw new ArgumentException("Width must be positive", nameof(Width));
            if (Thickness.HasValue && Thickness.Value <= 0) throw new ArgumentException("Thickness must be positive if specified", nameof(Thickness));
            if (Quantity <= 0) throw new ArgumentException("Quantity must be at least 1", nameof(Quantity));
        }

        /// <summary>
        /// Returns true if this board can provide at least one piece for the given cut item.
        /// This is a simple dimensional check; real packing rules live elsewhere.
        /// </summary>
        public bool IsUsableFor(CutItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            item.Validate();

            bool fitsLength = Length >= item.Length;
            bool fitsWidth = !item.Width.HasValue || Width >= item.Width.Value;

            if (fitsLength && fitsWidth) return true;

            if (item.AllowRotated)
            {
                // allow swapping length/width of the cut item
                bool fitsRotated = Length >= (item.Width ?? 0) && Width >= item.Length;
                return fitsRotated;
            }

            return false;
        }

        public Board Clone()
        {
            return new Board(Length, Width, Thickness, Grade, Quantity)
            {
                Id = Guid.NewGuid()
            };
        }

        public override string ToString()
            => $"Board(Id={Id}, L={Length}, W={Width}, T={(Thickness?.ToString() ?? "n/a")}, G={Grade ?? "n/a"}, Qty={Quantity})";
    }
}