using FluentAssertions;
using ZeroAllocPrimitives.Collections;

namespace ZeroAllocPrimitives.Tests.Collections;

public class RollingAverageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        Action act = () => new RollingAverage(invalidCapacity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InitialState_ReturnsZeroes_AndIsNotFull()
    {
        // Arrange & Act
        var target = new RollingAverage(5);

        // Assert
        target.Sum.Should().Be(0);
        target.Average.Should().Be(0);
        target.IsFull.Should().BeFalse();
    }

    [Fact]
    public void Add_WhenBelowCapacity_UpdatesSumAndAverageCorrectly()
    {
        // Arrange
        var target = new RollingAverage(4);

        // Act
        target.Add(10.0);
        target.Add(20.0);

        // Assert
        target.IsFull.Should().BeFalse();
        
        // Using BeApproximately is best practice for double precision math
        target.Sum.Should().BeApproximately(30.0, 0.00001);
        target.Average.Should().BeApproximately(15.0, 0.00001); 
    }

    [Fact]
    public void Add_WhenExceedingCapacity_EvictsOldestValue()
    {
        // Arrange
        var target = new RollingAverage(3);
        
        target.Add(10.0);
        target.Add(20.0);
        target.Add(30.0);
        
        target.IsFull.Should().BeTrue("Capacity of 3 has been reached");

        // Act
        // This should evict the 10.0. The buffer now holds [20.0, 30.0, 40.0]
        target.Add(40.0);

        // Assert
        target.IsFull.Should().BeTrue();
        target.Sum.Should().BeApproximately(90.0, 0.00001);
        target.Average.Should().BeApproximately(30.0, 0.00001);
    }

    [Fact]
    public void Add_ManyValues_HandlesWrapAroundAndDriftCorrectionSeamlessly()
    {
        // Arrange
        var target = new RollingAverage(3);

        // Act
        // Add 8 values to a capacity 3 buffer.
        // This forces the internal index to wrap around multiple times, 
        // silently triggering the RecalculateSum() drift correction.
        for (int i = 1; i <= 8; i++)
        {
            target.Add(i); 
        }

        // Assert
        // The last 3 values added were 6, 7, 8
        target.Sum.Should().BeApproximately(21.0, 0.00001);
        target.Average.Should().BeApproximately(7.0, 0.00001);
    }
}