using FinAI.Api.Services.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinAI.UnitTests.Services.Caching;

[Trait("Category", "Unit")]
public class MemoryCacheServiceTests
{
    private static MemoryCacheService CreateService() => new(
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<MemoryCacheService>.Instance);

    [Fact]
    public async Task GetOrCreateAsync_CachesValue_OnFirstCall()
    {
        var service = CreateService();
        var calls = 0;

        var first = await service.GetOrCreateAsync("k1", () => Task.FromResult(++calls), TimeSpan.FromMinutes(5));
        var second = await service.GetOrCreateAsync("k1", () => Task.FromResult(++calls), TimeSpan.FromMinutes(5));

        first.Should().Be(1);
        second.Should().Be(1); // factory não executada novamente
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_DoesNotCacheNull()
    {
        var service = CreateService();
        var calls = 0;

        var first = await service.GetOrCreateAsync<string?>("k-null", () => Task.FromResult<string?>(null), TimeSpan.FromMinutes(5));
        var second = await service.GetOrCreateAsync<string?>("k-null", () => Task.FromResult<string?>("x"), TimeSpan.FromMinutes(5));

        first.Should().BeNull();
        second.Should().Be("x"); // null não foi cacheado
    }

    [Fact]
    public async Task GetOrCreateAsync_ExpiresAfterTtl()
    {
        var service = CreateService();
        var calls = 0;

        await service.GetOrCreateAsync("k-ttl", () => Task.FromResult(++calls), TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        var second = await service.GetOrCreateAsync("k-ttl", () => Task.FromResult(++calls), TimeSpan.FromMilliseconds(1));

        second.Should().Be(2);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task RemoveByPrefix_InvalidatesMatchingKeysOnly()
    {
        var service = CreateService();

        await service.GetOrCreateAsync("analytics:spending:u1:d", () => Task.FromResult(1), TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("analytics:trend:u1:d", () => Task.FromResult(2), TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("other:key", () => Task.FromResult(3), TimeSpan.FromMinutes(5));

        service.RemoveByPrefix("analytics:spending:");

        var spending = await service.GetOrCreateAsync("analytics:spending:u1:d", () => Task.FromResult(10), TimeSpan.FromMinutes(5));
        var trend = await service.GetOrCreateAsync("analytics:trend:u1:d", () => Task.FromResult(20), TimeSpan.FromMinutes(5));
        var other = await service.GetOrCreateAsync("other:key", () => Task.FromResult(30), TimeSpan.FromMinutes(5));

        spending.Should().Be(10); // invalidado → refabricado
        trend.Should().Be(2);     // preservado
        other.Should().Be(3);     // preservado
    }

    [Fact]
    public async Task Remove_RemovesSingleKey()
    {
        var service = CreateService();

        await service.GetOrCreateAsync("k-rm", () => Task.FromResult(1), TimeSpan.FromMinutes(5));
        service.Remove("k-rm");

        var value = await service.GetOrCreateAsync("k-rm", () => Task.FromResult(2), TimeSpan.FromMinutes(5));
        value.Should().Be(2);
    }
}
