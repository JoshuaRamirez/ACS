using System.Linq;

namespace ACS.WebResources.Tests.Integration;

[TestClass]
public class ResourcesTests
{
    [TestMethod]
    public void ExpectedResource_IsPresent()
    {
        var assembly = typeof(ACS.WebResources.Class1).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Assert.IsTrue(resourceNames.Any(n => n.EndsWith("Strings.resx")),
            "Embedded resource Strings.resx should exist");
    }
}
