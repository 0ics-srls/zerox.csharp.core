§MBEL:5.0

# Active task progress only

[03-port-statistics]
✓notes.md + plan (8 blocks,85 red lines) committed 877e004;review:parsePlan valid,red count 85=85,stage A symbols exist,no capability rewritten
✓01 interval-and-circular-buffer:Collections/Interval<T> (record struct,half-open [From,To) + last N;factories Range/Since/LastN/Last(span,now)) + CircularBuffer<T> (one Lock,push/pop both ends,View = copy via SortedSearch.LowerBound on the 2 ring segments) + KeyComparer.For<TKey,TValue>;16 tests (7 ported,CB_MultiThreadTest Integration without Sleep);KeyComparer test in IntervalTests
✓02 series-keys:Statistics/SeriesKeys (ISeriesKeys<TKey> NextKey()+Distance(earlier,later);TimeKeys monotonic UTC on TimeProvider,seconds;IndexKeys Interlocked counter);4 tests
!gotcha::CA1716 (VB keywords) rejects interface members/params named Next,To,From... → NextKey(),Distance(earlier,later);plan updated in place
✓03 data-series:Statistics/ISeries (ISeries,IValueSeries<in T>,SeriesUpdate(Name,Count,Array Points),internal SeriesWindow.NewPoints) + DataSeries<TKey,TValue> (generic math,one Lock,Count long,Min/Max/Last/Sum/Mean/Rate in double,keys never back → ArgumentOutOfRange,rate 0 first + kept at distance 0,View copy,ViewSum double half-open,Get O(1),Snapshot/Load (ascending or ArgumentException,unchanged),UpdateSince);16 tests (5 ported:persistor tests on snapshots,Mongo dropped)
✓04 array-series:Statistics/ArraySeries<TKey,TValue> (rows stored as ImmutableArray copies,width enforced,per-channel Min/Max/Last/Sum/Mean/Rate as ImmutableArray,rate 0 for an unchanged channel,AddParts,ViewChannel,ViewSum double[],UpdateSince) + CounterWidening (Widen ushort/uint vs last,AddCounters extensions on ArraySeries<TKey,long>);11 tests (2 ported)
✓05 cyclic-series:Statistics/CyclicSeries<TValue> on CircularBuffer (window sum/mean,MinMean/MaxMean = old Min/Max of the moving mean,Min/Max of the window values,Count long,UpdateSince TValue[],fixed size);7 tests (1 ported)
✓06 aggregator:Statistics/SeriesAggregator (sealed registry,ConcurrentDictionary TryAdd atomic,Register/TryRegister series+groups,Remove,Add<TValue> via IValueSeries,Get<T>,GetByHierId,GetSeriesOrGroup,AvailableNames ordinal;no id index) + SeriesGroup (Of ids | WithPrefix);10 tests (3 ported)
