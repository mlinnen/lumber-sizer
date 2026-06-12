using System;
using System.Collections.Generic;
using System.Linq;

namespace WWA.Core.Models
{
    /// <summary>
    /// In-memory collection of boards.
    /// </summary>
    public class Inventory
    {
        private readonly List<Board> _boards = new();

        public void Add(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            board.Validate();
            _boards.Add(board);
        }

        public bool Remove(Board board)
        {
            if (board == null) return false;
            return _boards.Remove(board);
        }

        public IEnumerable<Board> EnumerateAvailable()
            => _boards.ToList();

        /// <summary>
        /// Find boards with at least the requested minimum length and available quantity > 0.
        /// </summary>
        public IEnumerable<Board> FindByMinLength(double length)
        {
            if (length <= 0) throw new ArgumentException("length must be positive", nameof(length));
            return _boards.Where(b => b.Length >= length && b.Quantity > 0).ToList();
        }
    }
}