namespace ZeroAllocPrimitives.Collections;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Numeric sliding window.
/// A zero-allocation, O(1) rolling average designed for high-performance double-precision math.
/// Ideal for calculating moving averages on continuous telemetry or event streams.
/// </summary>
public sealed class RollingAverage
{
    private readonly double[] _buffer;
    private readonly int _capacity;
    
    private int _index;
    private int _count;
    private double _runningSum;

    public bool IsFull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count == _capacity;
    }

    public double Sum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _runningSum;
    }

    public double Average
    {
        // Avoid divide-by-zero on uninitialized buffers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count > 0 ? _runningSum / _count : 0d;
    }

    public RollingAverage(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;

        // Pre-allocated exactly once to keep the hot path completely allocation-free
        _buffer = new double[capacity];
    }

    /// <summary>
    /// HOT PATH: Adds a new value to the sliding window in O(1) time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double value)
    {
        // 1. If buffer is full, subtract the oldest value from the running sum
        if (_count == _capacity)
        {
            _runningSum -= _buffer[_index];
        }
        else
        {
            _count++;
        }

        // 2. Add the new value to the running sum and overwrite the old value
        _runningSum += value;
        _buffer[_index] = value;

        // 3. Increment index and wrap around
        // Why not use modulo (_index % _capacity)? Modulo division is notoriously slow on the CPU.
        // A simple branch (++_index == _capacity) is significantly faster for hot-path ring buffers.
        if (++_index == _capacity)
        {
            _index = 0;

            // IEEE 754 Floating point math introduces microscopic drift over millions of operations.
            // Recalculating the exact sum once every full rotation resets this error, guaranteeing long-term precision.
            RecalculateSum();
        }
    }

    /// <summary>
    /// Resets the running sum from scratch to eliminate accumulated floating-point drift.
    /// </summary>
    private void RecalculateSum()
    {
        double sum = 0d;

        for (int i = 0; i < _capacity; i++)
        {
            sum += _buffer[i];
        }

        _runningSum = sum;
    }
}

