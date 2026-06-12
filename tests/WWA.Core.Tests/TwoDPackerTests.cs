using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using WWA.Core.BinPacking;
using WWA.Core.Models;
using WWA.Core.Interfaces;

namespace WWA.Core.Tests;

public class TwoDPackerTests
{
    [Fact]
    public async Task SimplePlacement_Fits_On_One_Board()
    {
        IPacker packer = new TwoDPacker();
        var cutList = new CutList();
        // Create 4 small items that should arrange into two shelves
        cutList.Add(new CutItem(20, 10));
        cutList.Add(new CutItem(20, 10));
        cutList.Add(new CutItem(20, 10));
        cutList.Add(new CutItem(20, 10));

        var inv = new Inventory();
        inv.Add(new Board(100, 25, quantity: 1));

        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
        var res = await packer.PackAsync(req);

        Assert.NotNull(res);
        Assert.Single(res.Allocations);
        var alloc = res.Allocations.First();
        Assert.Equal(4, alloc.Placements2D.Count);
    }

    [Fact]
    public async Task Rotation_Allows_Fit_When_Otherwise_Too_Large()
    {
        IPacker packer = new TwoDPacker();
        var cutList = new CutList();
        // Item longer than board length but narrow width; rotation should allow placement
        cutList.Add(new CutItem(120, 8, allowRotated: true));

        var inv = new Inventory();
        inv.Add(new Board(100, 130, quantity: 1)); // wide board but shorter length

        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
        var res = await packer.PackAsync(req);

        Assert.NotNull(res);
        Assert.Empty(res.UnplacedItems);
        Assert.Single(res.Allocations);
        var p = res.Allocations.First().Placements2D.First();
        Assert.True(p.Rotated);
    }

    [Fact]
    public async Task Determinism_Same_Seed_Produces_Same_Json()
    {
        IPacker packer = new TwoDPacker();
        var cutList = new CutList();
        cutList.Add(new CutItem(30, 10) { Quantity = 5 });
        cutList.Add(new CutItem(15, 5) { Quantity = 3 });

        var inv = new Inventory();
        inv.Add(new Board(200, 50, quantity: 1));

        var req1 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 42 };
        var req2 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 42 };

        var r1 = await packer.PackAsync(req1);
        var r2 = await packer.PackAsync(req2);

        var j1 = JsonSerializer.Serialize(r1);
        var j2 = JsonSerializer.Serialize(r2);

        Assert.Equal(j1, j2);
    }

    [Fact]
    public async Task Unplaced_Item_Reported_When_Too_Large()
    {
        IPacker packer = new TwoDPacker();
        var cutList = new CutList();
        cutList.Add(new CutItem(500, 500));

        var inv = new Inventory();
        inv.Add(new Board(100, 50, quantity: 1));

        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
        var res = await packer.PackAsync(req);

        Assert.Single(res.UnplacedItems);
    }
}
