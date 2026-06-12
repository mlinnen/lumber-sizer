using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.BinPacking;
using WWA.Core.Models;
using WWA.Core.Interfaces;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var repo = AppContext.BaseDirectory;
        var artifacts = Path.Combine(Path.GetFullPath(Path.Combine(repo, "..", "..", "..")), "artifacts");
        Directory.CreateDirectory(artifacts);
        var outFile = Path.Combine(artifacts, "packer_bench.txt");

        Console.WriteLine("Starting 2D packer benchmark...");

        // Generate synthetic cutlist with a realistic distribution:
        // - 10% large (tabletops)
        // - 50% medium
        // - 40% small
        var rand = new Random(42);
        var cutItems = new List<CutItem>();
        for (int i = 0; i < 200; i++)
        {
            double r = rand.NextDouble();
            double len, wid;
            if (r < 0.10)
            {
                // large
                len = rand.Next(48, 97); // 48-96
                wid = rand.Next(24, 49);  // 24-48
            }
            else if (r < 0.60)
            {
                // medium
                len = rand.Next(12, 49); // 12-48
                wid = rand.Next(6, 25);  // 6-24
            }
            else
            {
                // small
                len = rand.Next(2, 13);  // 2-12
                wid = rand.Next(2, 7);   // 2-6
            }
            // ensure width <= length
            if (wid > len) { var tmp = len; len = wid; wid = tmp; }
            cutItems.Add(new CutItem { Length = len, Width = wid, Description = $"item-{i}" });
        }

        var cl = new CutList();
        foreach (var c in cutItems) cl.Add(c);

        var inv = new Inventory();
        inv.Add(new Board(96, 48, quantity: 4));
        inv.Add(new Board(48, 24, quantity: 6));

        IPacker packer = new TwoDPacker();
        var req = new PackingRequest { CutList = cl, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };

        var sw = Stopwatch.StartNew();
        var res = await packer.PackAsync(req);
        sw.Stop();

        var info = $"Run completed. Items: {cutItems.Count}, Allocations: {res.Allocations.Count}, Unplaced: {res.UnplacedItems.Count}, TimeMs: {sw.ElapsedMilliseconds}";
        Console.WriteLine(info);
        File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + info + Environment.NewLine);
        if (res.Counters != null && res.Counters.Count > 0)
        {
            var metrics = string.Join(", ", res.Counters.Select(kv => $"{kv.Key}={kv.Value}"));
            Console.WriteLine("Metrics: " + metrics);
            File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tMetrics: " + metrics + Environment.NewLine);
        }

        // Run multiple iterations to average
        int runs = 10;
        long total = 0;
        for (int i = 0; i < runs; i++)
        {
            var rreq = new PackingRequest { CutList = cl, Inventory = inv, Constraints = new Constraints(), Seed = 12345 + i };
            var sw2 = Stopwatch.StartNew();
            var rres = await packer.PackAsync(rreq);
            sw2.Stop();
            var line = $"Iter {i+1}/{runs}: TimeMs={sw2.ElapsedMilliseconds}, Alloc={rres.Allocations.Count}, Unplaced={rres.UnplacedItems.Count}";
            Console.WriteLine(line);
            File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + line + Environment.NewLine);
            if (rres.Counters != null && rres.Counters.Count > 0)
            {
                var metrics = string.Join(", ", rres.Counters.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine("Metrics: " + metrics);
                File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tMetrics: " + metrics + Environment.NewLine);
            }
            total += sw2.ElapsedMilliseconds;
        }

        var avg = total / runs;
        var summary = $"AverageTimeMs={avg}";
        Console.WriteLine(summary);
        File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + summary + Environment.NewLine);

        Console.WriteLine($"Benchmark results written to {outFile}");
        return 0;
    }
}
