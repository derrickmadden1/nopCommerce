using System.Diagnostics;
using System.Linq.Expressions;
using FluentAssertions;
using Nop.Services.Localization;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Localization;

[TestFixture]
public class PropertySelectorCacheTests
{
    private sealed class FakeEntity
    {
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    private sealed class OtherEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Test]
    public void GetCompiledReturnsSameDelegateForSameProperty()
    {
        Expression<Func<FakeEntity, string>> sel1 = x => x.Name;
        Expression<Func<FakeEntity, string>> sel2 = x => x.Name;

        var d1 = PropertySelectorCache.GetCompiled(sel1);
        var d2 = PropertySelectorCache.GetCompiled(sel2);

        d1.Should().BeSameAs(d2);
    }

    [Test]
    public void GetCompiledReturnsDifferentDelegateForDifferentProperty()
    {
        var dName = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.Name);
        var dShort = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.ShortName);

        dName.Should().NotBeSameAs(dShort);
    }

    [Test]
    public void GetCompiledReturnsDifferentDelegateForDifferentEntityType()
    {
        var d1 = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.Name);
        var d2 = PropertySelectorCache.GetCompiled<OtherEntity, string>(x => x.Name);

        d1.Should().NotBeSameAs(d2);
    }

    [Test]
    public void GetCompiledDoesNotCollideOnDifferentPropertyType()
    {
        var dName = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.Name);
        var dId = PropertySelectorCache.GetCompiled<FakeEntity, int>(x => x.Id);

        dName.Should().NotBeSameAs(dId);
    }

    [Test]
    public void GetCompiledFallsBackForNonPropertySelector()
    {
        Expression<Func<FakeEntity, string>> selector = x => x.Name.ToUpperInvariant();
        var entity = new FakeEntity { Name = "abc" };

        var compiled = PropertySelectorCache.GetCompiled(selector);

        compiled(entity).Should().Be("ABC");
    }

    [Test]
    public void GetCompiledReturnsCorrectValue()
    {
        var entity = new FakeEntity { Name = "Casio", ShortName = "CAS" };

        var nameGetter = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.Name);
        var shortGetter = PropertySelectorCache.GetCompiled<FakeEntity, string>(x => x.ShortName);

        nameGetter(entity).Should().Be("Casio");
        shortGetter(entity).Should().Be("CAS");
    }

    [Test]
    public void GetCompiledThrowsOnNullSelector()
    {
        var act = () => PropertySelectorCache.GetCompiled<FakeEntity, string>(null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void GetCompiledBenchmarkFindings()
    {
        const int iterations = 10_000;
        var entity = new FakeEntity { Name = "Tissot" };

        //warmup: JIT and populate the cache
        for (var i = 0; i < 100; i++)
        {
            Expression<Func<FakeEntity, string>> sel = x => x.Name;
            _ = PropertySelectorCache.GetCompiled(sel)(entity);
            _ = sel.Compile()(entity);
        }

        //baseline: a fresh Expression.Compile() on every iteration
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            Expression<Func<FakeEntity, string>> sel = x => x.Name;
            _ = sel.Compile()(entity);
        }
        sw.Stop();
        var plainElapsedMs = sw.Elapsed.TotalMilliseconds;
        var plainAllocBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        //cached: PropertySelectorCache.GetCompiled() on every iteration
        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            Expression<Func<FakeEntity, string>> sel = x => x.Name;
            _ = PropertySelectorCache.GetCompiled(sel)(entity);
        }
        sw.Stop();
        var cachedElapsedMs = sw.Elapsed.TotalMilliseconds;
        var cachedAllocBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Console.WriteLine($"Iterations:           {iterations:N0}");
        Console.WriteLine($"Plain Compile():      {plainElapsedMs,10:F2} ms   {plainAllocBytes,15:N0} bytes alloc");
        Console.WriteLine($"Cached GetCompiled(): {cachedElapsedMs,10:F2} ms   {cachedAllocBytes,15:N0} bytes alloc");
        Console.WriteLine($"Speedup:              {plainElapsedMs / Math.Max(cachedElapsedMs, 0.001),10:F1}x");
        Console.WriteLine($"Allocation reduction: {plainAllocBytes / (double)Math.Max(cachedAllocBytes, 1),10:F1}x");
        Console.WriteLine($"Per-call plain:       {plainElapsedMs / iterations * 1_000_000,10:F2} ns");
        Console.WriteLine($"Per-call cached:      {cachedElapsedMs / iterations * 1_000_000,10:F2} ns");

        //structural guarantee: cache hit must not allocate a DynamicMethod / DynamicResolver,
        //so cached allocations must be far below the plain-compile baseline
        cachedAllocBytes.Should().BeLessThan(plainAllocBytes / 2);
    }
}
