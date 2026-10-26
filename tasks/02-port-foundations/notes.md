# 02-port-foundations — notes

## Requirement

Port the reusable part of the old Zerox code into this repository, cleaned. The source is a copy
of the old code at `assetforge/tmp/zerox` (outside this repository, never edited): the projects
`Zerox.Dnf`, `Zerox.Dnf.Tests`, `Zerox.Wpf.Core`, `Zerox.Wpf.XTest.Core`.

The port runs in four stages, one task each, in this order:

| Task | Stage | Content |
|---|---|---|
| 02-port-foundations (this one) | A | Dnf; collections (bit mask, sorted search, circular linked list); concurrency (atomic boolean, critical section, owner queue); cancellation; hierarchical id; number range; text, JSON and value conversion; reflection |
| 03-port-statistics | B | circular buffer, intervals, series, cyclic series, aggregator with `[Aggregable]`, remote data provider |
| 04-port-messaging-editing | C | message channels, validation rules, edit-copy proxy |
| 05-port-avalonia | D | throttled property container, async commands, observable task |

"Port" means: the capability and its tests come over; the code is rewritten where the old one
breaks a rule of this repository (CLAUDE.md): no Autofac or service locator, no WPF, no
Newtonsoft, no swallowed exception, no `IndexOutOfRangeException` / `ApplicationException` thrown
by library code (analyzer CA2201), English only, immutable where a value is a value. Bugs found
while reading are fixed, and each fix is pinned by a test.

Ported tests keep their old names (they are the contract the user knows); new tests follow
`Should_<Outcome>_When<Condition>`. Ported tests may keep xunit `Assert`; new ones use
AwesomeAssertions.

## Code Organization

1. **What is shared.** Every capability of this stage is a public type of `Zerox.Core` (or
   `Zerox.Dnf`), consumed by the later stages and by assetforge. Inside the stage: the sorted
   search (`SortedSearch`) is used by stage B's circular buffer; `ValueConversion` is used by
   stage C (validation, edit-copy proxy); `CancellationScope` is used by stage D's commands.
2. **Who owns it.** `src/Zerox.Dnf` owns the rule compiler. `src/Zerox.Core` owns the rest, one
   folder (and namespace `Zerox.Core.<Area>`) per area: `Collections`, `Concurrency`,
   `Cancellation`, `Identity`, `Numbers`, `Text`, `Reflection`.
3. **Contracts.** Concrete public types; no interface in this stage, because no consumer needs to
   substitute any of them (KISS). Dependencies arrive through constructors: `TimeProvider` for
   everything that waits or times out (testable with a fake clock), `CancellationToken` for
   everything that can be cancelled. No container, no static service locator.
4. **What this stage does not own.** Statistics (stage B), messaging and editing (stage C), the
   Avalonia helpers (stage D); P80 machine code of the old repository (dropped).
5. **Mechanism.** The owner queue is the mechanism of assetforge specification §3.7: one consumer,
   one job at a time, each to its end with its awaits, the caller awaiting its own job. The
   cancellation scope is a tree over linked `CancellationTokenSource`s. Everything else is a
   plain value type or a static helper.

## Inventario — capacità e dove esistono già

Searched: the old code (`assetforge/tmp/zerox`, read file by file), this repository (empty after
01-scaffold), the .NET 10 base library.

| Capability (what it does) | Old code | .NET 10 | Decision |
|---|---|---|---|
| Compile a boolean expression over 64 operands to DNF masks, evaluate it, serialize it for the embedded board | `Zerox.Dnf/*` (+ 8 test files, xunit) | — | port as is: comments to English, constants to PascalCase where the analyzers require it, behaviour and tests unchanged |
| Bit mask of arbitrary length with set/reset/count/and/or/equality | `Collections/BitArray.cs` (+ `BitArrayTest`) | `System.Collections.BitArray` has no fast population count | port as `BitMask` (the old name clashes with `System.Collections.BitArray`); fix: `==` modified its operands and its mask kept the wrong bits; out of range → `ArgumentOutOfRangeException` |
| First index ≥ v, first index > v, first/last index of v in a sorted sequence | `ArrayExtensions`, `ArraySegmentExtensions`, `ListExtensions`, `SortedListExtensions` (four copies, no tests) | `MemoryExtensions.BinarySearch` returns any equal index, no lower/upper bound | write ONE implementation on `ReadOnlySpan<T>` (`SortedSearch`); fix: `UpperBound` returned 0 for a value equal to the first element, empty input returned -1 |
| Round-robin set: unique items, `Next()` cycles, O(1) remove | `Collections/LinkedList/LinkedList.cs` (+ `CircularLinkedListTest`) | — | port as `CircularLinkedList<T>`; node type internal (the old public `LinkedListNode<T>` clashed with the BCL); `Add` = `TryAdd` or throw (one insertion path) |
| Boolean with compare-and-set and a change callback | `Atomic/AtomicBoolean.cs` (+ test) | `Interlocked` only on int/long | port; fix: the callback ran even when the value did not change, the setter was not atomic |
| Mutual exclusion that holds across `await` | `Async/CriticalSection.cs` (+ `CriticalSectionTest`) | `SemaphoreSlim` (used inside) | port; add `LockAsync` / `TryLockAsync` so a waiter does not block a thread |
| Lock with timeout on `Monitor` | `Async/TimedLock.cs` | `Monitor.TryEnter` | drop: breaks across `await` (the old test proves it) |
| Serial queue: one job at a time to its end, caller gets its own result | `Async/AsyncJobQueue.cs` (+ tests) | `Channel<T>` (used inside) | rewrite as `OwnerQueue` (assetforge §3.7): no Start/Stop, no error event (the exception goes to the caller), awaiting a job of the same queue from a job fails fast |
| Fire and forget with exception handler | `Async/SafeFireAndForgetExtensions.cs` | — | drop: without a handler it swallowed the exception (P9) |
| Run async code synchronously | `Async/AsyncUtil.cs` | — | drop: everything is async |
| Cancellation tree with timeout, state Canceled vs TimedOut, one root that cancels everything | `CancelContext/*` (+ `TerminatorTest`) | linked `CancellationTokenSource`, `CancellationTokenSource(TimeSpan, TimeProvider)` | rewrite small as `Terminator` + `CancellationScope` on `TimeProvider`; drop polling `WaitUntil`, the `Dispose` loop with `Thread.Sleep`, the logging catch in `Cancel` (P9) |
| Hierarchical id `a/b/c` with prefix, suffix, pattern match | `Support/HierId.cs` (+ `HierIdTest`) | — | port immutable: `+` and `-` return a new id (the old `+ string` modified its left operand); `Splice(n ≥ count)` is empty; `TryGetPart` dropped (it caught nothing that could throw) |
| Numbers clamped, parsed, stepped inside an interval of their type | `Support/NumberValue.cs` (object based, + `NumberParserTest`) | generic math `INumber<T>`, `IMinMaxValue<T>` | rewrite as `NumberRange<T>`: same behaviour (over max → max, invalid text → no value, step saturates at the bounds) |
| Truncate with ellipsis, clip to a length, UTF-8 Base64 | `Support/StringExtensions.cs` | — | port to `Text/TextExtensions`; fix: `Truncate` returned "..." longer than the limit for small limits; WPF `FlowDocument` and `SecureString` parts dropped |
| One JSON setup (names as-is, enums as member names) | `JsonToObject`, `ObjectExtensions.ToJson`, `StreamJsonExtensions` (Newtonsoft, three option sets) | `System.Text.Json` | write ONE `JsonDefaults.Options` + `ToJson` / `FromJson` on System.Text.Json |
| Convert a value to a type (nullable, enum by number or name, IConvertible) | `TypeExtensions.To<T>`, `ConversionExtensions.Convert` (two copies) | `Convert.ChangeType`, `Enum.ToObject` | ONE `ValueConversion.ConvertTo`; the old wrapper into `ApplicationException` dropped (the original exception reaches the caller) |
| Type by name, descendant by type name, safe cast | `TypeExtensions` | `OfType<T>()` | drop (no consumer; `OfType` covers the cast) |
| Properties carrying an attribute; attribute of a named property or of its type | `Support/ReflectionExtensions.cs` | `CustomAttributeExtensions` covers single attribute lookups | keep only what the framework lacks (`GetPropertiesWith<TAttribute>`, `GetPropertyAttribute<TAttribute>(name)`, `GetAttributeOfPropertyType<TAttribute>(name)`); drop the `IPropertyContainer` overloads |
| Compiled getter/setter of a property (no reflection per call) | `Reflection/FastPropertyTP.cs` (no tests) | — | port as `FastProperty<TInstance, TValue>`; fix: the cache was a `Dictionary` written by several threads |
