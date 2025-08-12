using Xunit;
using CryptikLemur.AssetBundleBuilder;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class HashUtilityTests
{
    [Fact]
    public void ComputeHash_WithSameInput_ShouldReturnSameHash()
    {
        var input = "test input string";
        
        var hash1 = HashUtility.ComputeHash(input);
        var hash2 = HashUtility.ComputeHash(input);
        
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_WithDifferentInput_ShouldReturnDifferentHash()
    {
        var input1 = "test input 1";
        var input2 = "test input 2";
        
        var hash1 = HashUtility.ComputeHash(input1);
        var hash2 = HashUtility.ComputeHash(input2);
        
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ShouldReturn8CharacterLowercaseHex()
    {
        var input = "test input";
        
        var hash = HashUtility.ComputeHash(input);
        
        Assert.Equal(8, hash.Length);
        Assert.All(hash, c => Assert.True(char.IsDigit(c) || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void ComputeHash_WithEmptyString_ShouldReturnValidHash()
    {
        var hash = HashUtility.ComputeHash("");
        
        Assert.Equal(8, hash.Length);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void ComputeHash_WithNullInput_ShouldThrowException()
    {
        Assert.Throws<ArgumentNullException>(() => HashUtility.ComputeHash(null!));
    }
}