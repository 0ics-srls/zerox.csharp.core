§MBEL:5.0

# Active task progress only

[03-port-statistics]
✓notes.md + plan (8 blocks,85 red lines) committed 877e004;review:parsePlan valid,red count 85=85,stage A symbols exist,no capability rewritten
✓01 interval-and-circular-buffer:Collections/Interval<T> (record struct,half-open [From,To) + last N;factories Range/Since/LastN/Last(span,now)) + CircularBuffer<T> (one Lock,push/pop both ends,View = copy via SortedSearch.LowerBound on the 2 ring segments) + KeyComparer.For<TKey,TValue>;16 tests (7 ported,CB_MultiThreadTest Integration without Sleep);KeyComparer test in IntervalTests
✓02 series-keys:Statistics/SeriesKeys (ISeriesKeys<TKey> NextKey()+Distance(earlier,later);TimeKeys monotonic UTC on TimeProvider,seconds;IndexKeys Interlocked counter);4 tests
!gotcha::CA1716 (VB keywords) rejects interface members/params named Next,To,From... → NextKey(),Distance(earlier,later);plan updated in place
✓03 data-series:Statistics/ISeries (ISeries,IValueSeries<in T>,SeriesUpdate(Name,Count,Array Points),internal SeriesWindow.NewPoints) + DataSeries<TKey,TValue> (generic math,one Lock,Count long,Min/Max/Last/Sum/Mean/Rate in double,keys never back → ArgumentOutOfRange,rate 0 first + kept at distance 0,View copy,ViewSum double half-open,Get O(1),Snapshot/Load (ascending or ArgumentException,unchanged),UpdateSince);16 tests (5 ported:persistor tests on snapshots,Mongo dropped)
