using System;

namespace WWA.Core.Models
{
    /// <summary>
    /// Rules about remnants and preserving long remnants.
    /// </summary>
    public class Constraints
    {
        /// <summary>
        /// Minimum remnant length in inches. Must be >= 0.
        /// </summary>
        public double MinRemnantLength { get; set; } = 0.0;

        /// <summary>
        /// If true, prefer keeping long remnants intact rather than cutting them.
        /// </summary>
        public bool PreserveLongRemnants { get; set; } = false;

        public void Validate()
        {
            if (MinRemnantLength < 0) throw new ArgumentException("MinRemnantLength must be >= 0", nameof(MinRemnantLength));
        }

        /// <summary>
        /// Validate these constraints against a specific board. Returns true when board satisfies constraints.
        /// Throws if constraints are invalid.
        /// </summary>
        public bool ValidateAgainst(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            Validate();

            // If MinRemnantLength is greater than the board length, it's invalid for this board
            if (MinRemnantLength > board.Length) return false;

            // Additional domain rules could go here.
            return true;
        }
    }
}