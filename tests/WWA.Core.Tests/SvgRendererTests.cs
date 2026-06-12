using System;
using System.IO;
using Xunit;
using WWA.Core.Reporting;
using WWA.Core.Models;

namespace WWA.Core.Tests
{
    public class SvgRendererTests
    {
        [Fact]
        public void Renders_Simple_Placement_To_Svg()
        {
            var result = new PackingResult();
            var alloc = new BoardAllocation { BoardId = Guid.NewGuid(), OriginalBoardLength = 100 };
            var item = new CutItem(24, 6, 1, true, "shelf") { Id = Guid.NewGuid() };
            alloc.Placements2D.Add(new Placement2D { CutItemId = item.Id, CutItem = item, XOffset = 0, YOffset = 0, Width = 6, Length = 24, Rotated = false });
            result.Allocations.Add(alloc);

            var renderer = new SvgRenderer();
            var svg = renderer.Render(result);

            Assert.False(string.IsNullOrWhiteSpace(svg));
            Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("shelf", svg, StringComparison.OrdinalIgnoreCase);
            // Expect rectangle for placement
            Assert.Contains("<rect", svg);
        }
    }
}
