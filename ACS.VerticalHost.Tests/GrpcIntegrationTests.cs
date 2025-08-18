using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACS.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class GrpcIntegrationTests
{
    [TestMethod]
    public void TenantProcessManager_CanBeCreated()
    {
        // Arrange
        var logger = new NullLogger<TenantProcessManager>();
        
        // Act
        var manager = new TenantProcessManager(logger);
        
        // Assert
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public async Task ProtoSerializer_CanSerializeAndDeserialize()
    {
        // Arrange
        var testData = new TestCommand { Value = 42, Name = "Test" };
        
        // Act
        var serialized = ACS.Infrastructure.ProtoSerializer.Serialize(testData);
        var deserialized = ACS.Infrastructure.ProtoSerializer.Deserialize<TestCommand>(serialized);
        
        // Assert
        Assert.IsNotNull(serialized);
        Assert.IsTrue(serialized.Length > 0);
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(testData.Value, deserialized.Value);
        Assert.AreEqual(testData.Name, deserialized.Name);
    }

    private class TestCommand
    {
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}