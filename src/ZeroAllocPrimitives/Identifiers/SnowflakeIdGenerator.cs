using System.Runtime.CompilerServices;
using System.Threading;

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

    // Unified state: Top 54 bits for Timestamp, Bottom 10 bits for Sequence.
    // Replaces the naive '_counter' to prevent sequence wrapping bugs.
    private long _state = 0;

    // Singleton: Hide constructor (prevent calling 'new SnowflakeIdGenerator()')
    private SnowflakeIdGenerator() { }

    // Singleton: Public access point for instance
    public static SnowflakeIdGenerator Instance => _instance;

    /// <summary>
    /// Thread-safe, lock-free, zero-allocation Message ID generation.
    /// Utilizes a Compare-And-Swap (CAS) spin-loop to guarantee strict chronological
    /// ordering and prevent sequence wrapping within the same millisecond.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long NextId()
    {
        var spin = new SpinWait();

        while (true)
        {
            // 1. Read current unified state
            long currentState = Volatile.Read(ref _state);
            long currentTimestamp = currentState >> 10;
            long currentSequence = currentState & 1023;

            // 2. Get real-world time
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long newSequence;

            // Handle clock drift / NTP backwards leaps safely
            if (timestamp < currentTimestamp)
            {
                timestamp = currentTimestamp;
            }

            if (timestamp == currentTimestamp)
            {
                newSequence = currentSequence + 1;

                // Hardware limit reached for this millisecond.
                // Spin until the next millisecond ticks over.
                if (newSequence > 1023)
                {
                    spin.SpinOnce();
                    continue;
                }
            }
            else
            {
                // Millisecond advanced, reset sequence
                newSequence = 0;
            }

            // 3. Pack the new state
            long newState = (timestamp << 10) | newSequence;

            // 4. Atomically Compare-And-Swap. If no other thread modified '_state'
            // since our read, apply 'newState' and return.
            if (Interlocked.CompareExchange(ref _state, newState, currentState) == currentState)
            {
                return newState;
            }

            // Another thread won the race. Spin to reduce CPU cache contention, then retry.
            spin.SpinOnce();
        }
    }
}
