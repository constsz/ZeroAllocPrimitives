namespace ZeroAllocPrimitives.Collections;

/// <summary>
/// A zero-allocation, O(1) circular buffer designed specifically for high-performance double precision math.
/// </summary>
public sealed class RingBuffer
{
    private readonly double[] _buffer;
    private readonly int _capacity;
    
    private int _index;
    private int _count;
    private double _runningSum;

    public bool IsFull => _count == _capacity;
    public double Sum => _runningSum;
    
    // Returns 0 if empty, otherwise O(1) average
    public double Average => _count > 0 ? _runningSum / _count : 0d;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        
        _capacity = capacity;
        _buffer = new double[capacity]; // Pre-allocated exactly once
    }

    /// <summary>
    /// HOT PATH: Adds a new value in O(1) time.
    /// </summary>
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

        // 3. Increment index and wrap around (faster than modulo '%')
        if (++_index == _capacity)
        {
            _index = 0;
            
            // Optional HFT Drift Correction:
            // Floating point math (double) can introduce microscopic drift over millions of additions/subtractions.
            // Recalculating the exact sum once every full rotation guarantees precision stays flawless.
            // (Uncomment if exact precision over days of uptime is strictly required)
            // RecalculateSum(); 
        }
    }

    private void RecalculateSum()
    {
        double sum = 0d;
        for (int i = 0; i < _capacity; i++) sum += _buffer[i];
        _runningSum = sum;
    }
}