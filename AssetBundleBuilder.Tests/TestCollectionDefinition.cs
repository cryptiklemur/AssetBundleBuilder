using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[CollectionDefinition("AssetBuilder Sequential Tests", DisableParallelization = true)]
public class TestCollectionDefinition {
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}