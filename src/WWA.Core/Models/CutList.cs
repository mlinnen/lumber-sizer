using System;
using System.Collections.Generic;

namespace WWA.Core.Models
{
    /// <summary>
    /// Wrapper for a collection of cut items with metadata.
    /// </summary>
    public class CutList
    {
        public string? ProjectName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<CutItem> Items { get; } = new List<CutItem>();

        public CutList() { }

        public CutList(string projectName) : this()
        {
            ProjectName = projectName;
        }

        public CutList(IEnumerable<CutItem> items, string? projectName = null) : this(projectName)
        {
            if (items != null) Items.AddRange(items);
        }

        public void Add(CutItem item)
        {
            item.Validate();
            Items.Add(item);
        }
    }
}