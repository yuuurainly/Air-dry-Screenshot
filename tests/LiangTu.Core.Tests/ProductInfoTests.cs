using LiangTu.Core;

namespace LiangTu.Core.Tests;

[TestClass]
public sealed class ProductInfoTests
{
    [TestMethod]
    [DataRow("晾图", "1.0.0", "net10.0-windows")]
    public void ProductIdentity_MatchesReleaseBaseline(
        string expectedName,
        string expectedVersion,
        string expectedTargetFramework)
    {
        Assert.AreEqual(ProductInfo.Name, expectedName);
        Assert.AreEqual(ProductInfo.Version, expectedVersion);
        Assert.AreEqual(ProductInfo.WindowsTargetFramework, expectedTargetFramework);
    }
}
