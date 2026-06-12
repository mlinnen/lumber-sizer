using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WWA.Core.BinPacking;
using WWA.Core.IO;
using WWA.Core.Models;
using WWA.Core.Interfaces;

namespace WWA.Core.Tests;

public class TwoDPackerIntegrationTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory!);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        if (dir == null) throw new InvalidOperationException("Could not locate repository root (.git folder)");
        return dir.FullName;
    }

    [Fact]
    public async Task Runs_On_Sample_Cutlist_Without_Exception()
    {
        var repoRoot = FindRepoRoot();
        var sample = Path.Combine(Path.GetTempPath(), $"runtime_simple_cutlist_{Guid.NewGuid()}.txt");
                using (var fs = new System.IO.FileStream(sample, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                using (var sw = new System.IO.StreamWriter(fs))
                {
                    sw.Write("12in x 2in # leg\r\n24in x 6in # shelf\r\n");
                }

        var cutList = CutListParser.Parse(sample);

        var inv = new Inventory();
        // provide a variety of boards
        inv.Add(new Board(100, 2, quantity: 1));
        inv.Add(new Board(100, 6, quantity: 1));
        inv.Add(new Board(200, 12, quantity: 1));

        IPacker packer = new TwoDPacker();
        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 7 };
        var res = await packer.PackAsync(req);

        Assert.NotNull(res);
        Assert.True(res.Allocations.Count >= 1);
    }
}
