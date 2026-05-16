using System.Text.RegularExpressions;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Storage.VectorStores;

internal static class VectorStoreGuards
{
    internal static void ValidateCollectionName(string provider, string collection, string pattern)
    {
        if (string.IsNullOrWhiteSpace(collection) || !Regex.IsMatch(collection, pattern))
            throw AgxiomeStorageException.InvalidCollectionName(provider, collection, pattern);
    }

    internal static TCollection GetCollectionOrThrow<TCollection>(
        Dictionary<string, TCollection> collections,
        string collection,
        string provider)
        where TCollection : class
    {
        if (collections.TryGetValue(collection, out var value))
            return value;

        throw AgxiomeStorageException.CollectionMissing(provider, collection);
    }
}