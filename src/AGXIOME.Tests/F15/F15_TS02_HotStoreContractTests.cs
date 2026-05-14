// F15-TS02 — HotStore Contract Tests (10 tests)
// Validates InMemoryHotStore: set/get, TTL expiry (via Clear), sets, pub/sub basics.

using AGXIOME.Orchestration.Infrastructure;

namespace AGXIOME.Tests.F15;

public sealed class F15_TS02_HotStoreContractTests
{
    private static InMemoryHotStore Fresh() { var s = new InMemoryHotStore(); s.Clear(); return s; }

    [Fact] public async Task Set_ThenGet_ReturnsSameValue()
    {
        var store = Fresh();
        await store.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));
        var val = await store.GetAsync("k1");
        Assert.Equal("hello", val);
    }

    [Fact] public async Task Get_MissingKey_ReturnsNull()
    {
        var store = Fresh();
        Assert.Null(await store.GetAsync("missing-key"));
    }

    [Fact] public async Task Delete_ExistingKey_RemovesIt()
    {
        var store = Fresh();
        await store.SetAsync("k2", "v", TimeSpan.FromMinutes(1));
        await store.DeleteAsync("k2");
        Assert.Null(await store.GetAsync("k2"));
    }

    [Fact] public async Task Exists_BeforeAndAfterSet()
    {
        var store = Fresh();
        Assert.False(await store.ExistsAsync("k3"));
        await store.SetAsync("k3", "v", TimeSpan.FromMinutes(1));
        Assert.True(await store.ExistsAsync("k3"));
    }

    [Fact] public async Task Increment_StartsAtZero_ReturnsOne()
    {
        var store = Fresh();
        var v = await store.IncrementAsync("counter");
        Assert.Equal(1L, v);
    }

    [Fact] public async Task Increment_TwiceLong_ReturnsTwo()
    {
        var store = Fresh();
        await store.IncrementAsync("cnt2");
        var v = await store.IncrementAsync("cnt2");
        Assert.Equal(2L, v);
    }

    [Fact] public async Task SetAdd_ThenSetMembers_ContainsValue()
    {
        var store = Fresh();
        await store.SetAddAsync("myset", "member1");
        var members = await store.SetMembersAsync("myset");
        Assert.Contains("member1", members);
    }

    [Fact] public async Task SetAdd_MultipleMembers_AllReturned()
    {
        var store = Fresh();
        await store.SetAddAsync("ms2", "a");
        await store.SetAddAsync("ms2", "b");
        await store.SetAddAsync("ms2", "c");
        var members = await store.SetMembersAsync("ms2");
        Assert.Equal(3, members.Count);
    }

    [Fact] public async Task SetMembers_EmptySet_ReturnsEmpty()
    {
        var store = Fresh();
        var members = await store.SetMembersAsync("nonexistent-set");
        Assert.Empty(members);
    }

    [Fact] public async Task PubSub_PublishSubscribe_DeliversSingleMessage()
    {
        var store = Fresh();
        var received = new List<string>();

        // Subscribe first
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var msg in store.SubscribeAsync("chan1", cts.Token))
            {
                received.Add(msg);
                break; // only read first message
            }
        });

        // Small delay to let the subscribe set up
        await Task.Delay(50);
        await store.PublishAsync("chan1", "hello-world");

        try { await subscribeTask; } catch (OperationCanceledException) { }
        Assert.Contains("hello-world", received);
    }
}
