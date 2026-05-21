using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// IStoreFactory implementation — resolves ICommandStore by provider type.
// Registered as singleton in DI. Allows fully agnostic store selection:
//   var store = factory.GetStore(StorageProviderType.SQLite);
//   await store.SaveAsync(entity);
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Default implementation of <see cref="IStoreFactory"/>.
/// Holds a dictionary of registered stores keyed by <see cref="StorageProviderType"/>.
/// </summary>
public sealed class StoreFactory : IStoreFactory
{
    private readonly IReadOnlyDictionary<StorageProviderType, ICommandStore> _stores;

    public StoreFactory(IEnumerable<ICommandStore> stores)
    {
        _stores = stores
            .GroupBy(s => s.ProviderType)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <inheritdoc/>
    public ICommandStore GetStore(StorageProviderType provider)
    {
        if (_stores.TryGetValue(provider, out var store))
            return store;

        throw new InvalidOperationException(
            $"No ICommandStore registered for provider '{provider}'. " +
            $"Registered providers: {string.Join(", ", _stores.Keys)}");
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<StorageProviderType, ICommandStore> GetAllStores()
        => _stores;
}
