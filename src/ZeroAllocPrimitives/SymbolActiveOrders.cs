namespace ZeroAllocPrimitives;

/// <summary>
/// Manages open orders for all strategies. 
/// Organized by [StrategyId]:[ClientOrderId]:OpenOrder
/// </summary>
public class SymbolActiveOrders
{
    /// <summary>
    /// Dictionary [StrategyId] : { ClientOrderId: OpenOrder }
    /// </summary>
    private readonly Dictionary<int, Dictionary<long, Order>> _orders;

    /// <summary>
    /// Fast way to iterate over orders.
    /// Zero-allocation, exposes the raw memory, zero state to maintain.
    /// </summary>
    /// <returns></returns>
    public Dictionary<int, Dictionary<long, Order>>.ValueCollection GetOrdersDictionaries() => _orders.Values;
    
    // Reusable buffer to prevent allocations during removals.
    // 100% thread-safe because SymbolBookManager processes messages sequentially.
    private readonly List<long> _removalBuffer = new(128);
    
    public int GetTotalCount()
    {
        int total = 0;

        foreach (var strategyOrders in _orders.Values)
        {
            total += strategyOrders.Count;
        }

        return total;
    }
    
    public bool IsEmpty => GetTotalCount() == 0;

    /// <summary>
    /// CTORs
    /// </summary>
    public SymbolActiveOrders(int totalNumberOfStrategies)
    {
        _orders = new Dictionary<int, Dictionary<long, Order>>(totalNumberOfStrategies);
    }

    // ------------------------------------------------------------------------

    #region READ

    
    /// <summary>
    /// Get order for specified strategy.
    /// </summary>
    public bool TryGetOrder(int strategyId, long clientOrderId, out Order order)
    {
        if (_orders.TryGetValue(strategyId, out var strategyOrders))
        {
            return strategyOrders.TryGetValue(clientOrderId, out order);
        }

        order = default;
        return false;
    }

    /// <summary>
    /// Gets open orders for specified strategy for this Symbol.
    /// </summary>
    public Dictionary<long, Order> GetOrders(int strategyId)
    {
        if (_orders.TryGetValue(strategyId, out var strategyOrders))
        {
            return strategyOrders;
        }

        var newDict = new Dictionary<long, Order>(100);
        _orders[strategyId] = newDict;
        return newDict;
    }
    
    #endregion

    #region WRITE

    /// <summary>
    /// Adds or updates Open Order.
    /// </summary>
    public void AddOrUpdate(Order newOrder)
    {
        if (!_orders.TryGetValue(newOrder.StrategyId, out var strategyOrders))
        {
            strategyOrders = new Dictionary<long, Order>(100);
            _orders[newOrder.StrategyId] = strategyOrders;
        }

        strategyOrders[newOrder.ClientOrderId] = newOrder;
    }

    /// <summary>
    /// Removes open order by ClientOrderId for specified strategy.
    /// </summary>
    public void Remove(int strategyId, long clientOrderId)
    {
        if (_orders.TryGetValue(strategyId, out var strategyOrders))
        {
            strategyOrders.Remove(clientOrderId);
        }
    }

    public void RemoveOrdersForPositionSide(PositionSide side)
    {
        foreach (var strategyOrders in _orders.Values)
        {
            if (strategyOrders.Count == 0) continue;
            
            _removalBuffer.Clear();

            // 1. Collect keys (iterating KeyValuePair is faster than .Keys + indexer lookup)
            foreach (var kvp in strategyOrders)
            {
                if (kvp.Value.PositionSide == side)
                {
                    _removalBuffer.Add(kvp.Key);
                }
            }

            // 2. Remove the collected keys safely
            foreach (var clientOrderId in _removalBuffer)
            {
                strategyOrders.Remove(clientOrderId);
            }
        }        
    }
    
    #endregion

    #region TRANSFORM

    /// <summary>
    /// Creates read-only Snapshot - the way outer modules
    /// can read the SymbolBook state.
    /// </summary>
    /// <returns></returns>
    public ActiveOrdersSnapshot ToSnapshot()
    {
        int totalOrders = GetTotalCount();
        
        if (totalOrders == 0)
        {
            return new ActiveOrdersSnapshot(Array.Empty<Order>());
        }
        
        Order[] snapshotArray = new Order[totalOrders];
        int index = 0;
        
        foreach (var strategyOrders in _orders.Values)
        {
            foreach (var kvp in strategyOrders)
            {
                // Order kvp.Value
                snapshotArray[index++] = kvp.Value;
            }
        }
        
        
        return new ActiveOrdersSnapshot(snapshotArray);

    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OpenOrders:");

        if (IsEmpty)
        {
            sb.Append(" (Empty)");
        }
        else
        {
            foreach (var kvp in _orders)
            {
                sb.AppendLine($"- StrategyId: {kvp.Key}");
                
                foreach (var orderKv in kvp.Value)
                {
                    var order = orderKv.Value;
                    sb.AppendLine($"    - Order: {order.ClientOrderId}");
                }
            }
        }

        return sb.ToString();
    }

    #endregion


}


public readonly record struct ActiveOrdersSnapshot
{
    private readonly Order[] _orders;

    internal ActiveOrdersSnapshot(Order[] orders)
    {
        _orders = orders;
    }

    /// <summary>
    /// Get Order by ClientOrderId
    /// </summary>
    /// <returns>True if found and out variable, and False if not found.</returns>
    public bool TryGetOrder(long clientOrderId, out Order order)
    {
        foreach (Order openOrder in _orders)
        {
            if (openOrder.ClientOrderId == clientOrderId)
            {
                order = openOrder;
                return true;
            }
        }

        order = default;
        return false;
    }

    /// <summary>
    /// Returns enumerator for orders of specific strategy.
    /// Strategies can use this in a standard 'foreach' loop without creating GC garbage.
    /// </summary>
    public StrategyOrderEnumerable GetOrders(int strategyId)
    {
        // It looks like it returns a list/array, but it allocates ZERO heap memory.
        // The compiler unravels the struct enumerator directly onto the CPU stack.
        //
        // Example of usage inside Strategy:
        // var snapshot = _sbm.GetSnapshot();
        // foreach (var order in snapshot.GetOrders(MyStrategyId))
        // {
        //     if (order.Price > targetPrice) 
        //     {
        //         // Do something
        //     }
        // }
        
        return new StrategyOrderEnumerable(_orders, strategyId);
    }
    
    
    /// <summary>
    /// Zero-Allocation Enumerator
    /// </summary>
    public readonly struct StrategyOrderEnumerable
    {
        private readonly Order[] _orders;
        private readonly int _strategyId;

        public StrategyOrderEnumerable(Order[] orders, int strategyId)
        {
            _orders = orders;
            _strategyId = strategyId;
        }

        public Enumerator GetEnumerator() => new Enumerator(_orders, _strategyId);

        public ref struct Enumerator
        {
            private readonly Order[] _orders;
            private readonly int _strategyId;
            private int _index;

            public Enumerator(Order[] orders, int strategyId)
            {
                _orders = orders;
                _strategyId = strategyId;
                _index = -1;
            }

            public bool MoveNext()
            {
                while (++_index < _orders.Length)
                {
                    if (_orders[_index].StrategyId == _strategyId)
                    {
                        return true;
                    }
                }
                return false;
            }

            public Order Current => _orders[_index];
        }
    }

}