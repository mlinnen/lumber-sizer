using System;
using System.Linq;
using WWA.Core.Models;
using Xunit;

namespace WWA.Core.Tests
{
    public class ModelsTests
    {
        [Fact]
        public void Create_Valid_Models_Succeed()
        {
            var item = new CutItem(24.0, 2.0, 2);
            item.Validate();

            var board = new Board(96.0, 6.0, 1.0, "A", 3);
            board.Validate();

            var inv = new Inventory();
            inv.Add(board);

            var cl = new CutList("Test Project");
            cl.Add(item);

            Assert.Single(cl.Items);
            Assert.Single(inv.EnumerateAvailable());
        }

        [Fact]
        public void Validation_Rejects_Invalid_CutItem()
        {
            var bad = new CutItem(-1.0, null, 1);
            Assert.Throws<ArgumentException>(() => bad.Validate());

            var zeroQty = new CutItem(10.0, null, 0);
            Assert.Throws<ArgumentException>(() => zeroQty.Validate());
        }

        [Fact]
        public void Validation_Rejects_Invalid_Board()
        {
            var b = new Board(-10, 2.0);
            Assert.Throws<ArgumentException>(() => b.Validate());

            var b2 = new Board(10, -2.0);
            Assert.Throws<ArgumentException>(() => b2.Validate());
        }

        [Fact]
        public void Inventory_Add_And_FindByMinLength()
        {
            var inv = new Inventory();
            var b1 = new Board(96, 6, quantity: 1);
            var b2 = new Board(48, 6, quantity: 2);
            inv.Add(b1);
            inv.Add(b2);

            var found = inv.FindByMinLength(50).ToList();
            Assert.Single(found);
            Assert.Equal(96, found.First().Length);

            var foundAll = inv.FindByMinLength(10).ToList();
            Assert.Equal(2, foundAll.Count);
        }

        [Fact]
        public void Constraints_ValidateAgainst_Board()
        {
            var c = new Constraints { MinRemnantLength = 10 };
            var b = new Board(96, 6);
            Assert.True(c.ValidateAgainst(b));

            var c2 = new Constraints { MinRemnantLength = 200 };
            Assert.False(c2.ValidateAgainst(b));
        }
    }
}