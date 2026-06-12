using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WWA.Core.Reporting
{
    // Small helper extensions for QuestPDF to accept raw image bytes from Skia.
    public static class QuestPdfExtensions
    {
        public static IContainer ImageFromBytes(this IContainer container, byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            return container.Image(ms);
        }
    }
}
