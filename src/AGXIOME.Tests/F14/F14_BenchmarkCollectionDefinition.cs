// Benchmark tests run sequentially within this collection — tests in the same
// [Collection] are never run in parallel with each other, ensuring stable
// timing measurements without contention from sibling benchmark classes.

using Xunit;

namespace AGXIOME.Tests.F14;

[CollectionDefinition("Benchmarks")]
public sealed class BenchmarkCollection { }
