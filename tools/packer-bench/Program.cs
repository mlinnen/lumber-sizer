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

            // discover dataset files
            var datasetsDir = Path.Combine(Path.GetFullPath(Path.Combine(repo, "..", "..", "..")), "tools", "packer-bench", "datasets");
            var datasetFiles = Directory.Exists(datasetsDir) ? Directory.GetFiles(datasetsDir, "*.txt") : Array.Empty<string>();
            if (datasetFiles.Length == 0)
            {
                Console.WriteLine("No dataset files found in tools/packer-bench/datasets. Running synthetic default dataset.");
                datasetFiles = new[] { "__generated__" };
            }

            foreach (var datasetPath in datasetFiles)
            {
                List<CutItem> cutItemsList;
                string datasetName;

                if (datasetPath == "__generated__")
                {
                    datasetName = "synthetic_default";
                    // keep existing synthetic generator
                    var rand2 = new Random(42);
                    cutItemsList = new List<CutItem>();
                    for (int i = 0; i < 200; i++)
                    {
                        double r = rand2.NextDouble();
                        double len, wid;
                        if (r < 0.10)
                        {
                            len = rand2.Next(48, 97);
                            wid = rand2.Next(24, 49);
                        }
                        else if (r < 0.60)
                        {
                            len = rand2.Next(12, 49);
                            wid = rand2.Next(6, 25);
                        }
                        else
                        {
                            len = rand2.Next(2, 13);
                            wid = rand2.Next(2, 7);
                        }
                        if (wid > len) { var tmp = len; len = wid; wid = tmp; }
                        cutItemsList.Add(new CutItem { Length = len, Width = wid, Description = $"item-{i}" });
                    }
                }
                else
                {
                    datasetName = Path.GetFileNameWithoutExtension(datasetPath);
                    cutItemsList = new List<CutItem>();
                    var lines = File.ReadAllLines(datasetPath);
                    foreach (var line in lines)
                    {
                        var t = line.Trim();
                        if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                        // simple parser: "<len>in x <wid>in # desc"
                        try
                        {
                            var parts = t.Split('#')[0].Trim().Split('x');
                            var lenPart = parts[0].Trim();
                            var widPart = parts[1].Trim();
                            double len = double.Parse(lenPart.Replace("in", "").Trim());
                            double wid = double.Parse(widPart.Replace("in", "").Trim());
                            if (wid > len) { var tmp = len; len = wid; wid = tmp; }
                            cutItemsList.Add(new CutItem { Length = len, Width = wid, Description = t });
                        }
                        catch
                        {
                            // skip malformed
                        }
                    }
                }

                // build CutList
                var cl2 = new CutList();
                foreach (var c in cutItemsList) cl2.Add(c);

                // inventory (use a realistic set)
                var inv2 = new Inventory();
                inv2.Add(new Board(96, 48, quantity: 3));
                inv2.Add(new Board(48, 24, quantity: 6));
                inv2.Add(new Board(72, 12, quantity: 2));

                Console.WriteLine($"Running dataset: {datasetName} (items={cutItemsList.Count})");
                File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tDataset: " + datasetName + " Items: " + cutItemsList.Count + Environment.NewLine);

                IPacker packer2 = new GuillotinePacker();
                var req2 = new PackingRequest { CutList = cl2, Inventory = inv2, Constraints = new Constraints(), Seed = 12345 };

                var swDataset = Stopwatch.StartNew();
                var resDataset = await packer2.PackAsync(req2);
                swDataset.Stop();

                var info2 = $"Dataset {datasetName} run completed. Items: {cutItemsList.Count}, Allocations: {resDataset.Allocations.Count}, Unplaced: {resDataset.UnplacedItems.Count}, TimeMs: {swDataset.ElapsedMilliseconds}";
                Console.WriteLine(info2);
                File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + info2 + Environment.NewLine);
                if (resDataset.Counters != null && resDataset.Counters.Count > 0)
                {
                    var metrics2 = string.Join(", ", resDataset.Counters.Select(kv => $"{kv.Key}={kv.Value}"));
                    Console.WriteLine("Metrics: " + metrics2);
                    File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tMetrics: " + metrics2 + Environment.NewLine);
                }

                // multiple iterations per dataset
                int runs2 = 5;
                long total2 = 0;
                for (int i = 0; i < runs2; i++)
                {
                    var rreq = new PackingRequest { CutList = cl2, Inventory = inv2, Constraints = new Constraints(), Seed = 12345 + i };
                    var sw2 = Stopwatch.StartNew();
                    var rres = await packer2.PackAsync(rreq);
                    sw2.Stop();
                    var line = $"{datasetName}: Iter {i+1}/{runs2}: TimeMs={sw2.ElapsedMilliseconds}, Alloc={rres.Allocations.Count}, Unplaced={rres.UnplacedItems.Count}";
                    Console.WriteLine(line);
                    File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + line + Environment.NewLine);
                    if (rres.Counters != null && rres.Counters.Count > 0)
                    {
                        var metrics = string.Join(", ", rres.Counters.Select(kv => $"{kv.Key}={kv.Value}"));
                        Console.WriteLine("Metrics: " + metrics);
                        File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tMetrics: " + metrics + Environment.NewLine);
                    }
                    total2 += sw2.ElapsedMilliseconds;
                }

                var avg2 = total2 / runs2;
                var summary2 = $"{datasetName} AverageTimeMs={avg2}";
                Console.WriteLine(summary2);
                File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + summary2 + Environment.NewLine);
            }

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

        var packers = new (string name, IPacker packer)[]
                        {
                            ("Guillotine", new GuillotinePacker()),
                            ("MaxRects", new MaxRectsPacker())
                        };

                        foreach (var (name, packer) in packers)
                        {
                            var req = new PackingRequest { CutList = cl, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };

                            var sw = Stopwatch.StartNew();
                            var res = await packer.PackAsync(req);
                            sw.Stop();

                            var info = $"{name} Run completed. Items: {cutItems.Count}, Allocations: {res.Allocations.Count}, Unplaced: {res.UnplacedItems.Count}, TimeMs: {sw.ElapsedMilliseconds}";
                            Console.WriteLine(info);
                            File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\t" + info + Environment.NewLine);
                            if (res.Counters != null && res.Counters.Count > 0)
                            {
                                var metrics = string.Join(", ", res.Counters.Select(kv => $"{kv.Key}={kv.Value}"));
                                Console.WriteLine("Metrics: " + metrics);
                                File.AppendAllText(outFile, DateTime.UtcNow.ToString("o") + "\tMetrics: " + metrics + Environment.NewLine);
                            }
                        }

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
