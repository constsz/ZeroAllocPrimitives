using FluentAssertions;
using ZeroAllocPrimitives.Collections;

namespace ZeroAllocPrimitives.Tests.Collections;

public class FifoCacheTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        Action act = () => new FifoCache<int, string>(invalidCapacity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddOrUpdate_WithNewItem_StoresSuccessfully()
    {
        // Arrange
        var cache = new FifoCache<int, string>(2);

        // Act
        cache.AddOrUpdate(1, "A");

        // Assert
        cache.TryGetValue(1, out var val).Should().BeTrue();
        val.Should().Be("A");
        cache.Get(1).Should().Be("A");
    }

    [Fact]
    public void AddOrUpdate_WhenCapacityReached_EvictsOldestItemFirst()
    {
        // Arrange
        var cache = new FifoCache<int, string>(3);
        cache.AddOrUpdate(1, "A");
        cache.AddOrUpdate(2, "B");
        cache.AddOrUpdate(3, "C");

        // Act - Adding a 4th item should evict the 1st item
        cache.AddOrUpdate(4, "D");

        // Assert
        cache.TryGetValue(1, out _).Should().BeFalse("Item 1 was the oldest and should be evicted");
        cache.TryGetValue(2, out _).Should().BeTrue("Item 2 should still be in cache");
        cache.TryGetValue(4, out _).Should().BeTrue("Item 4 was just added");
    }

    [Fact]
    public void AddOrUpdate_WithExistingKey_UpdatesValueButDoesNotResetEvictionOrder()
    {
        // Arrange
        var cache = new FifoCache<int, string>(3);
        cache.AddOrUpdate(1, "A");
        cache.AddOrUpdate(2, "B");
        cache.AddOrUpdate(3, "C");

        // Act
        cache.AddOrUpdate(1, "A_Updated"); // Update oldest item
        cache.AddOrUpdate(4, "D");         // Trigger an eviction

        // Assert
        // Because it's FIFO and not LRU, updating Item 1 does not make it "new". 
        // It remains the oldest item in the ring buffer, so it should be evicted when 4 is added.
        cache.TryGetValue(1, out _).Should().BeFalse("Updating a value does not reset its FIFO position");
        cache.Get(4).Should().Be("D");
    }

    [Fact]
    public void AddOrUpdate_WithManyItems_WrapsRingBufferSafely()
    {
        // Arrange
        var cache = new FifoCache<int, string>(3);

        // Act
        // Add 10 items to a capacity 3 cache. 
        // This forces the internal _head and _tail pointers to wrap around the array multiple times.
        for (int i = 0; i < 10; i++)
        {
            cache.AddOrUpdate(i, $"Val_{i}");
        }

        // Assert
        // Only the last 3 items (7, 8, 9) should remain.
        cache.TryGetValue(6, out _).Should().BeFalse();
        cache.Get(7).Should().Be("Val_7");
        cache.Get(8).Should().Be("Val_8");
        cache.Get(9).Should().Be("Val_9");
    }
}