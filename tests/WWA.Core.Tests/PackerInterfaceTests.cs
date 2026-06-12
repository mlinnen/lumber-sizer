using System.Linq;
using System.Threading.Tasks;
using WWA.Core.BinPacking;
using WWA.Core.Models;
using WWA.Core.Interfaces;

namespace WWA.Core.Tests;

public class PackerInterfaceTests
{
    [Fact]
    public async Task Stub_Returns_Result_For_Simple_Request()
    {
        var packer = new DeterministicPackerStub() as IPacker;
        var cutList = new CutList();
        cutList.Add(new CutItem(30, quantity: 1));
        cutList.Add(new CutItem(30, quantity: 1));
        cutList.Add(new CutItem(30, quantity: 1));

        var inv = new Inventory();
        inv.Add(new Board(100, 5, quantity: 1));

        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
        var res = await packer.PackAsync(req);

        Assert.NotNull(res);
        Assert.True(res.Allocations.Count >= 1);
        var alloc = res.Allocations.First();
        Assert.Equal(3, alloc.Placements.Count);
    }

    [Fact]
    public async Task Determinism_Same_Seed_Produces_Same_Result()
    {
        var packer = new DeterministicPackerStub() as IPacker;
        var cutList = new CutList();
        cutList.Add(new CutItem(10) { Quantity = 5 });
        cutList.Add(new CutItem(15) { Quantity = 3 });

        var inv = new Inventory();
        inv.Add(new Board(100, 5, quantity: 1));
        inv.Add(new Board(50, 5, quantity: 1));

        var req1 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };
        var req2 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };

        var r1 = await packer.PackAsync(req1);
        var r2 = await packer.PackAsync(req2);

        // Simple determinism: allocations and placements counts should match
        Assert.Equal(r1.Allocations.Count, r2.Allocations.Count);
        Assert.Equal(r1.TotalUsedLength, r2.TotalUsedLength);
        Assert.Equal(r1.WastePercent, r2.WastePercent);
    }

    [Fact]
    public async Task Output_Contains_Allocations_For_Provided_CutItems()
    {
        var packer = new DeterministicPackerStub() as IPacker;
        var cutItem = new CutItem(25) { Quantity = 2 };
        var cutList = new CutList();
        cutList.Add(cutItem);

        var inv = new Inventory();
        inv.Add(new Board(60, 5, quantity: 1));

        var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
        var res = await packer.PackAsync(req);

        var placedIds = res.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();
        Assert.True(placedIds.Count >= 1);
        Assert.Contains(cutItem.Id, placedIds);
    }
}
