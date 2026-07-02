using System;
using System.Globalization;
using System.Xml.Linq;
#if HAS_SKIA
using SkiaSharp;
using Svg.Skia;
#endif
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
