using System.IO;

namespace WWA.Core.Reporting
{
    // Stubbed helper for optional QuestPDF integration.
    // Kept as a no-op to avoid compile-time dependency on a specific QuestPDF API surface.
    public static class QuestPdfExtensions
    {
        public static object ImageFromBytes(this object container, byte[] imageBytes)
        {
            // Runtime integration can replace this with a proper binding when QuestPDF API is stable.
            return null;
        }
    }
}
