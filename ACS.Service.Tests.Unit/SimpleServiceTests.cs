using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class SimpleServiceTests
{
    [TestMethod]
    public void SimpleTest_AlwaysPasses_ReturnsTrue()
    {
        // Arrange
        var expected = true;

        // Act
        var result = true;

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void MockLogger_CanBeCreated_IsNotNull()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SimpleServiceTests>>();

        // Act & Assert
        Assert.IsNotNull(mockLogger);
        Assert.IsNotNull(mockLogger.Object);
    }

    [TestMethod]
    public void StringComparison_CaseInsensitive_ReturnsExpectedResult()
    {
        // Arrange
        var str1 = "TEST";
        var str2 = "test";

        // Act
        var result = string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void List_AddItem_ContainsItem()
    {
        // Arrange
        var list = new List<string>();
        var item = "test-item";

        // Act
        list.Add(item);

        // Assert
        Assert.IsTrue(list.Contains(item));
        Assert.AreEqual(1, list.Count);
    }

    [TestMethod]
    public void DateTime_UtcNow_IsNotDefault()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;

        // Assert
        Assert.AreNotEqual(default(DateTime), now);
        Assert.AreEqual(DateTimeKind.Utc, now.Kind);
    }
}