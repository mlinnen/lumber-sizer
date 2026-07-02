using System;
using System.IO;
using System.Threading.Tasks;
using WWA.Core.IO;
using WWA.Core.Models;
using WWA.Core.BinPacking;
using WWA.Core.Reporting;
using WWA.Core.Interfaces;

const string ExportPdfUsage = "export-pdf <input-cutlist> <output-pdf-or-html> [--inventory path] [--packer deterministic|full|two-d] [--seed N]";

Inventory CreateDefaultInventory()
{
    var inventory = new Inventory();
    inventory.Add(new Board(96, 48, null, "A", 5));
    return inventory;
}

async Task<int> Main(string[] args)
{
    if (args.Length == 0)
    {
        Console.WriteLine($"WWA CLI\nCommands:\n  {ExportPdfUsage}");
        return 0;
    }

    var cmd = args[0].ToLowerInvariant();
    if (cmd == "export-pdf")
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine($"Usage: {ExportPdfUsage}");
            return 2;
        }

        var input = args[1];
        var output = args[2];
        string? inventoryPath = null;
        string packerName = "full";
        int? seed = null;

        for (int i = 3; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--packer" && i + 1 < args.Length)
            {
                packerName = args[++i].ToLowerInvariant();
            }
            else if (a == "--inventory")
            {
                if (i + 1 >= args.Length
                    || string.IsNullOrWhiteSpace(args[i + 1])
                    || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("Error: --inventory requires a file path.");
                    Console.Error.WriteLine($"Usage: {ExportPdfUsage}");
                    return 2;
                }

                inventoryPath = args[++i];
            }
            else if (a == "--seed" && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var s)) seed = s;
            }
        }

        try
        {
            var cutlist = CutListParser.Parse(input);
            var inventory = string.IsNullOrWhiteSpace(inventoryPath)
                ? CreateDefaultInventory()
                : InventoryParser.Parse(inventoryPath);

            var request = new PackingRequest { CutList = cutlist, Inventory = inventory, Seed = seed };

            IPacker packer = packerName switch
            {
                "deterministic" => new DeterministicPackerStub(),
                "full" => new FullPacker(),
                "two-d" => new TwoDPacker(),
                _ => new FullPacker()
            };

            var result = await packer.PackAsync(request);

            // Render SVG and write PDF/HTML via PdfReporter
            var renderer = new SvgRenderer();
            var svg = renderer.Render(result);
            PdfReporter.GenerateFromSvg(svg, output);

            Console.WriteLine($"Exported visuals to {output}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 3;
        }
    }

    Console.Error.WriteLine($"Unknown command: {cmd}");
    return 1;
}

return await Main(args);
