using ZeroAllocPrimitives.Identifiers;
using FluentAssertions;

namespace ZeroAllocPrimitives.Tests.Identifiers;

public class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_MultiThreaded_ShouldNotProduceDuplicates()
    {
        // Arrange
        // We limit to 1000 to respect the 1024/ms hardware limit of this specific implementation.
        // Proves that Interlocked.Increment safely prevents race conditions on the sequence counter.
        const int concurrentRequests = 1000;
        var generatedIds = new long[concurrentRequests];

        // Act
        // Simulate high-throughput concurrent access hitting 
        // the generator in the exact same millisecond window.
        Parallel.For(0, concurrentRequests, i =>
        {
            generatedIds[i] = SnowflakeIdGenerator.Instance.NextId();
        });

        // Assert
        // If thread-safety was broken, threads would read the same sequence number 
        // and generate duplicate IDs. FluentAssertions handles the uniqueness check natively.
        generatedIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void NextId_SingleThread_ShouldBeStrictlyIncreasing()
    {
        // Arrange
        const int iterations = 1000;
        var ids = new long[iterations];

        // Act
        for (int i = 0; i < iterations; i++)
        {
            ids[i] = SnowflakeIdGenerator.Instance.NextId();
        }

        // Assert
        // Crucial for event-sourcing or message ordering: 
        // Later messages MUST have mathematically larger IDs.
        ids.Should().BeInAscendingOrder();
    }
    [Fact]
    public void NextId_ShouldNeverReturnNegative()
    {
        // Arrange & Act
        long id = SnowflakeIdGenerator.Instance.NextId();

        // Assert
        // Validates that our bit shift (timestamp << 10) doesn't push data into 
        // the 64th bit (the sign bit for signed longs), which would corrupt DB indexing.
        id.Should().BePositive("because bitwise shifting should not overflow into the sign bit");
    }
    
    [Fact]
    public void NextId_WhenExceedingMaxSequence_ShouldSpinWaitUntilNextMillisecond()
    {
        // Act & Assert
        // We force 2000 generations in a tight loop. 
        // The hardware limit is 1024/ms. The new CAS spin-loop will safely
        // yield the thread until the next millisecond, guaranteeing strict
        // chronological ordering without throwing an exception.
        Action generate = () =>
        {
            for (int i = 0; i < 2000; i++)
            {
                SnowflakeIdGenerator.Instance.NextId();
            }
        };

        generate.Should().NotThrow();
    }
}