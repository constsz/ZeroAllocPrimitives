using System.Runtime.CompilerServices;

namespace ZeroAllocPrimitives.Identifiers;

/// <summary>
/// A high-performance, zero-allocation ID generator.
/// Replaces standard Guid generation to avoid heap allocations and string formatting overhead.
/// Utilizes lock-free thread safety (Interlocked) and bitwise operations to safely 
/// generate up to 1024 unique, time-ordered IDs per millisecond (~1 million/sec) 
/// across multiple threads without locking contention.
/// </summary>
public sealed class SnowflakeIdGenerator
{
    // Singleton: Instance.
    // 'static readonly' guarantees thread-safe initialization by the .NET runtime.
    private static readonly SnowflakeIdGenerator _instance = new SnowflakeIdGenerator();

    // Shared counter. We use a standard 'long' because Interlocked 
    // works natively and universally with signed 64-bit integers.
    private long _counter = 0;

    // Singleton: Hide constructor (prevent calling 'new SnowflakeIdGenerator()')
    private SnowflakeIdGenerator() { }

    // Singleton: Public access point for instance
    public static SnowflakeIdGenerator Instance => _instance;

    /// <summary>
    /// Thread-safe, lock-free, zero-allocation Message ID generation.
    /// Supports up to 1024 concurrent messages per millisecond.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long NextId()
    {
        // Get current millisecond timestamp
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Shift left by 10 (Leaves 10 empty binary slots = room for 1024 messages)
        long shiftedTimestamp = timestamp << 10;

        // Atomically increment the counter. 
        // Interlocked.Increment is thread-safe and extremely fast.
        // It safely handles multiple threads hitting this exact line at the same nanosecond.
        long currentCounter = Interlocked.Increment(ref _counter);

        // Bitwise AND 1023 perfectly loops the counter from 1023 back to 0.
        // Even if 'currentCounter' overflows to negative in 292,000 years,
        // bitwise masking safely strips the negative sign bit!
        long sequence = currentCounter & 1023;

        // Combine the timestamp and the sequence
        return shiftedTimestamp | sequence;
    }
}