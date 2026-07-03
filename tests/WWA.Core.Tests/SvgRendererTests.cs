using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
#if HAS_SKIA
using SkiaSharp;
using Svg.Skia;
#endif
using Xunit;
using WWA.Core.Reporting;
using WWA.Core.Models;
using WWA.Core.BinPacking;

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

        [Fact]
        public void Renders_OneDimensional_Placement_To_Svg()
        {
            var result = new PackingResult();
            var alloc = new BoardAllocation { BoardId = Guid.NewGuid(), OriginalBoardLength = 96, RemnantLength = 72 };
            var item = new CutItem(24, 6, 1, true, "shelf") { Id = Guid.NewGuid() };
            alloc.Placements.Add(new Placement { CutItemId = item.Id, CutItem = item, Offset = 0, Rotated = false, Length = item.Length });
            result.Allocations.Add(alloc);

            var renderer = new SvgRenderer();
            var svg = renderer.Render(result);

            Assert.Contains("shelf", svg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fill-opacity=\"0.85\"", svg, StringComparison.Ordinal);
        }

        [Fact]
        public async System.Threading.Tasks.Task FullPacker_Render_Uses_Inventory_Board_Width_And_Reserves_Space_For_Scale_And_Legend()
        {
            var cutList = new CutList();
            cutList.Add(new CutItem(24, 6, 1, true, "shelf"));
            cutList.Add(new CutItem(12, 2, 1, true, "leg"));

            var inventory = new Inventory();
            inventory.Add(new Board(96, 48, quantity: 1));

            var result = await new FullPacker().PackAsync(new PackingRequest
            {
                CutList = cutList,
                Inventory = inventory,
                Constraints = new Constraints()
            });

            var renderer = new SvgRenderer();
            var svg = renderer.Render(result);

            Assert.Contains("Board 0 (96in x 48in)", svg, StringComparison.OrdinalIgnoreCase);

            var document = XDocument.Parse(svg);
            var svgNs = document.Root!.Name.Namespace;

            var boardRect = document.Root!
                .Elements(svgNs + "g")
                .First(e => string.Equals((string?)e.Attribute("id"), "board-0", StringComparison.Ordinal))
                .Elements(svgNs + "rect")
                .First();
            var scaleRect = document.Root!
                .Elements(svgNs + "g")
                .First(e => string.Equals((string?)e.Attribute("id"), "scale", StringComparison.Ordinal))
                .Elements(svgNs + "rect")
                .First();
            var legendRect = document.Root!
                .Elements(svgNs + "g")
                .First(e => string.Equals((string?)e.Attribute("id"), "legend", StringComparison.Ordinal))
                .Elements(svgNs + "rect")
                .First();

            var boardY = double.Parse(boardRect.Attribute("y")!.Value, CultureInfo.InvariantCulture);
            var boardHeight = double.Parse(boardRect.Attribute("height")!.Value, CultureInfo.InvariantCulture);
            var scaleY = double.Parse(scaleRect.Attribute("y")!.Value, CultureInfo.InvariantCulture);
            var scaleHeight = double.Parse(scaleRect.Attribute("height")!.Value, CultureInfo.InvariantCulture);
            var legendY = double.Parse(legendRect.Attribute("y")!.Value, CultureInfo.InvariantCulture);

            Assert.Equal(480, boardHeight, precision: 6);
            Assert.True(boardY >= scaleY + scaleHeight + 12);
            Assert.True(legendY >= boardY + boardHeight);
        }

        [Fact]
        public void Rendered_Svg_Is_WellFormed_Xml()
        {
            var result = new PackingResult();
            var alloc = new BoardAllocation { BoardId = Guid.NewGuid(), OriginalBoardLength = 96, RemnantLength = 60 };
            result.Allocations.Add(alloc);

            var renderer = new SvgRenderer();
            var svg = renderer.Render(result);

            var document = XDocument.Parse(svg);

            Assert.Equal("svg", document.Root?.Name.LocalName);
        }

        [Fact]
        public void Renders_Numeric_Attributes_Using_Invariant_Culture()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            try
            {
                var germanCulture = CultureInfo.GetCultureInfo("de-DE");
                CultureInfo.CurrentCulture = germanCulture;
                CultureInfo.CurrentUICulture = germanCulture;

                var result = new PackingResult();
                var alloc = new BoardAllocation { BoardId = Guid.NewGuid(), OriginalBoardLength = 10.5, RemnantLength = 1.25 };
                var item = new CutItem(2.5, 1.25, 1, true, "shelf") { Id = Guid.NewGuid() };
                alloc.Placements2D.Add(new Placement2D { CutItemId = item.Id, CutItem = item, XOffset = 0.5, YOffset = 0.25, Width = 1.25, Length = 2.5, Rotated = false });
                result.Allocations.Add(alloc);

                var renderer = new SvgRenderer();
                var svg = renderer.Render(result, pxPerInch: 1.5);

                Assert.Contains("width=\"15.75\"", svg, StringComparison.Ordinal);
                Assert.DoesNotContain("width=\"15,75\"", svg, StringComparison.Ordinal);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }

#if HAS_SKIA
        [Fact]
        public void Rendered_Svg_Rasterizes_To_NonEmpty_Skia_Bitmap()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            try
            {
                var germanCulture = CultureInfo.GetCultureInfo("de-DE");
                CultureInfo.CurrentCulture = germanCulture;
                CultureInfo.CurrentUICulture = germanCulture;

                var result = new PackingResult();
                var alloc = new BoardAllocation { BoardId = Guid.NewGuid(), OriginalBoardLength = 10.5, RemnantLength = 1.25 };
                var item = new CutItem(2.5, 1.25, 1, true, "shelf") { Id = Guid.NewGuid() };
                alloc.Placements2D.Add(new Placement2D { CutItemId = item.Id, CutItem = item, XOffset = 0.5, YOffset = 0.25, Width = 1.25, Length = 2.5, Rotated = false });
                result.Allocations.Add(alloc);

                var renderer = new SvgRenderer();
                var svg = renderer.Render(result, pxPerInch: 1.5);

                using var svgStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
                var skSvg = new SKSvg();
                skSvg.Load(svgStream);

                var picture = skSvg.Picture;
                Assert.NotNull(picture);

                var cull = picture!.CullRect;
                var width = Math.Max(1, (int)Math.Ceiling(cull.Width));
                var height = Math.Max(1, (int)Math.Ceiling(cull.Height));
                Assert.True(width > 1);
                Assert.True(height > 1);

                using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPicture(picture);
                canvas.Flush();

                var hasOpaquePixel = false;
                for (var y = 0; y < height && !hasOpaquePixel; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        if (bitmap.GetPixel(x, y).Alpha > 0)
                        {
                            hasOpaquePixel = true;
                            break;
                        }
                    }
                }

                Assert.True(hasOpaquePixel);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }

#endif
    }
}
