using SanguoSLG.Core.Simulation;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

public class SeededRandomSourceTests
{
    [Fact]
    public void 같은_시드는_같은_순서의_값을_낸다()
    {
        var a = new SeededRandomSource(42);
        var b = new SeededRandomSource(42);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(a.Next(0, 1000), b.Next(0, 1000));
        }
    }

    [Fact]
    public void Next는_요청한_범위_안의_값을_낸다()
    {
        var random = new SeededRandomSource(7);

        for (var i = 0; i < 100; i++)
        {
            var value = random.Next(10, 20);
            Assert.InRange(value, 10, 19);
        }
    }
}
