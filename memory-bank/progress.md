§MBEL:5.0

# Active task progress only

[02-port-foundations]
✓notes.md + plan (13 blocks) committed,j-review-plan (HierId null-comparison note)
✓01 dnf-port:7 sources + 8 test files (148 tests green);Italian comments translated;RuleSerializer constants PascalCase;analyzer fixes (DnfTerm fields → get-only properties + ==/!=,static helpers,no default initializers);docs of the binary format corrected to v3
✓02 sorted-search:Collections/SortedSearch (LowerBound,UpperBound,BinarySearchFirst/Last on ReadOnlySpan<T>,C# 14 receivers array/Span/List via CollectionsMarshal);std semantics (old:empty → -1,UpperBound of first element → 0);8 tests incl. 1000 random arrays vs linear scan
✓03 bit-mask:Collections/BitMask (ulong words,PopCount,GetSetBits via TrailingZeroCount,|/& + BitwiseOr/BitwiseAnd,value equality);fixed old == mutating operands + wrong mask,ArgumentOutOfRange,different sizes → ArgumentException;9 tests
✓04 circular-linked-list:Collections/CircularLinkedList<T> (round-robin set,O(1) index,private Node,Add = TryAdd or throw,Remove returns bool,int Count);7 tests (5 ported)
✓05 atomic-boolean:Concurrency/AtomicBoolean (int + Interlocked,callback only on real change,atomic setter);4 tests
