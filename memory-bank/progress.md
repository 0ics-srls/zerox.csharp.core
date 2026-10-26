§MBEL:5.0

# Active task progress only

[02-port-foundations]
✓notes.md + plan (13 blocks) committed,j-review-plan (HierId null-comparison note)
✓01 dnf-port:7 sources + 8 test files (148 tests green);Italian comments translated;RuleSerializer constants PascalCase;analyzer fixes (DnfTerm fields → get-only properties + ==/!=,static helpers,no default initializers);docs of the binary format corrected to v3
✓02 sorted-search:Collections/SortedSearch (LowerBound,UpperBound,BinarySearchFirst/Last on ReadOnlySpan<T>,C# 14 receivers array/Span/List via CollectionsMarshal);std semantics (old:empty → -1,UpperBound of first element → 0);8 tests incl. 1000 random arrays vs linear scan
✓03 bit-mask:Collections/BitMask (ulong words,PopCount,GetSetBits via TrailingZeroCount,|/& + BitwiseOr/BitwiseAnd,value equality);fixed old == mutating operands + wrong mask,ArgumentOutOfRange,different sizes → ArgumentException;9 tests
✓04 circular-linked-list:Collections/CircularLinkedList<T> (round-robin set,O(1) index,private Node,Add = TryAdd or throw,Remove returns bool,int Count);7 tests (5 ported)
✓05 atomic-boolean:Concurrency/AtomicBoolean (int + Interlocked,callback only on real change,atomic setter);4 tests
✓06 critical-section:Concurrency/CriticalSection (SemaphoreSlim,Lock/Lock(timeout)/TryLock + LockAsync/TryLockAsync) + CriticalSectionLock struct (IsLocked);TimedLock dropped;8 tests (2 Integration with SpinWait,no Thread.Sleep:ast-grep rule)
!gotcha::CA1849 flags synchronous Lock in async tests → take the held lock with LockAsync,keep sync calls in sync helpers/lambdas;xUnit1031 forbids Task.WaitAll in tests → await Task.WhenAll(...).WaitAsync
✓07 cancellation-scope:Cancellation/Terminator + CancellationScope (linked CTS,CancellationTokenSource(timeout,TimeProvider),state Active|Canceled|TimedOut with parent rule first,children set with one Lock,DelayAsync on TimeProvider);FakeTimeProvider (TimeProvider.Testing 10.11.0,tests only);9 tests,no real wait;callback exceptions reach the caller (AggregateException)
✓08 owner-queue:Concurrency/OwnerQueue (Channel,one consumer as async-void work item started on demand,RunAsync/RunAsync<T> → caller gets its job's result or exception,AsyncLocal fail-fast for a job awaiting its own queue,StartDetached with ExecutionContext.SuppressFlow,Dispose cancels running token + queued jobs,PendingCount);CA1711 suppressed with justification (name of the §3.7 mechanism);12 tests,class runs in ~340 ms
