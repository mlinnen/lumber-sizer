using System.Collections.Generic;

namespace WWA.Core.Models
{
    /// <summary>
    /// Represents a cut list containing multiple CutItem entries.
    /// </summary>
    public class CutList
    {
        public List<CutItem> Items { get; } = new List<CutItem>();
    }
}