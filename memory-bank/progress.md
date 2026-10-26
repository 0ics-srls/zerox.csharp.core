§MBEL:5.0

# Active task progress only

[03-port-statistics]
✓notes.md + plan (8 blocks,85 red lines) committed 877e004;review:parsePlan valid,red count 85=85,stage A symbols exist,no capability rewritten
✓01 interval-and-circular-buffer:Collections/Interval<T> (record struct,half-open [From,To) + last N;factories Range/Since/LastN/Last(span,now)) + CircularBuffer<T> (one Lock,push/pop both ends,View = copy via SortedSearch.LowerBound on the 2 ring segments) + KeyComparer.For<TKey,TValue>;16 tests (7 ported,CB_MultiThreadTest Integration without Sleep);KeyComparer test in IntervalTests
