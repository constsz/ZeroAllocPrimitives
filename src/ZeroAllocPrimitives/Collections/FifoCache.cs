using System.Runtime.CompilerServices;

namespace ZeroAllocPrimitives.Collections;

/// <summary>
/// FIFO (First-In, First-Out) cache with fixed-capacity, zero-allocation.
/// A Key-Value dictionary useful for managing state, payloads, or network connections.
/// This implementation uses a pre-allocated ring buffer and bounded dictionary
/// to guarantee O(1) operations with strictly zero heap allocations after initialization.
/// We avoid using LinkedList to track eviction order, because it allocates a new node on the heap for every insertion. 
/// </summary>
public sealed class FifoCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _cache;
    
    private readonly TKey[] _evictionRing;
    private readonly int _capacity;
    private int _head; // Points to the oldest key (next to be evicted)
    private int _tail; // Points to the next insertion slot
    private int _count;

    public FifoCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;

        // Pre-allocate the dictionary capacity to prevent internal array resizing
        _cache = new Dictionary<TKey, TValue>(capacity);
        _evictionRing = new TKey[capacity];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrUpdate(TKey key, TValue value)
    {
        // 1. If key exists, update value.
        // Note: In pure FIFO, updating does NOT reset the eviction timer.
        if (_cache.ContainsKey(key))
        {
            _cache[key] = value;
            return;
        }

        // 2. If at maximum capacity, evict the oldest item
        if (_count == _capacity)
        {
            TKey oldestKey = _evictionRing[_head];
            _cache.Remove(oldestKey);

            // Advance head, wrapping around if necessary (faster than modulo '%')
            if (++_head == _capacity)
                _head = 0;
        }
        else
        {
            _count++;
        }

        // 3. Insert the new key
        // Safe from internal resizing because we guaranteed count <= capacity
        _cache.Add(key, value);

        _evictionRing[_tail] = key;

        if (++_tail == _capacity)
            _tail = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, out TValue value)
    {
        return _cache.TryGetValue(key, out value!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue? Get(TKey key)
    {
        return _cache.TryGetValue(key, out var value) ? value : default;
    }
}