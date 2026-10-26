§MBEL:5.0

# History

## Today
(empty - completed tasks go here)

[2026-10-26 01-scaffold ✓]
✓repository created,.ai-agent submodule + setup,j-settings.md + lint config (sgconfig.yml,Directory.Build.props + .editorconfig templates)
✓plan written:tasks/01-scaffold/plan.md (3 TDDAB blocks)
✓j-review-plan (RepositoryRoot in assetforge's shape)
✓01 solution-skeleton:global.json,version.build (1),Directory.Build.props/targets/Packages.props,.editorconfig (+tests overrides),.gitignore,zerox.csharp.core.slnx,3 empty src + 3 test projects,tests/Shared/RepositoryRoot.cs linked;7 tests green,build 0 warnings,format+ast-grep clean
!gotcha::an XML comment in a .props must not contain '--' (MSB4024 → restore fails with a silent RestoreTask error)
✓02 headless-ui-session:TestApp (plain Application on headless),HeadlessUi.RunAsync (assetforge shape,session per assembly),4 tests + version = 5 green
!gotcha::headless Dispatcher.UIThread.CheckAccess() is true on ANY thread (UIThread bound to first toucher,reset by the session) → recognise the UI thread by SynchronizationContext.Current is AvaloniaSynchronizationContext (null outside);plan RED reworded in place
!fact::HeadlessUnitTestSession runs on a thread-pool thread,completes a plain TaskCompletionSource,rethrows the body's exception unchanged (no AggregateException)
✓03 verify-and-docs:tools/verify.sh ([1/4] build,[2/4] format,[3/4] tests,[4/4] ast-grep;stops at first failure;works from any folder),CLAUDE.md project part (Project,Stack locks,Rules incl. relative paths only,Build & Test),README.md;3 shell checks run from the session scratchpad (not in the repo)
✓FINAL REVIEW:tools/verify.sh 4/4 ok,11 tests,0 warnings

[2026-10-26 02-port-foundations ✓]
✓stage A on feature/02-port (CVM run-20261026-zerox-02,13/13 blocks,verify.sh 4/4,284 tests,0 warnings):Dnf port (148 tests),SortedSearch,BitMask,CircularLinkedList,AtomicBoolean,CriticalSection (LockAsync),Terminator + CancellationScope (TimeProvider),OwnerQueue (§3.7,StartDetached),HierId immutable,NumberRange<T> generic math,TextExtensions + JsonDefaults,ValueConversion,AttributeExtensions + FastProperty
!fixed+pinned::binary search bounds,BitArray == mutating operands,AtomicBoolean callback,HierId mutability/GetParts/Splice,long/ulong limits,Truncate over the limit,FastProperty cache not thread safe
!gotchas::CA1000 → factories on non-generic static class;CA1849/xUnit1031 in async tests;CA1861 constant arrays → static readonly;CA2263 → pass Type via variable;CA1711 suppressed only on OwnerQueue

[2026-10-26 03-port-statistics ✓]
✓stage B on feature/03-port-statistics (CVM run-20261026-zerox-03,8/8 blocks,verify.sh 4/4,370 tests):Interval<T> + CircularBuffer<T> + KeyComparer,ISeriesKeys (TimeKeys monotonic,IndexKeys),ISeries/IValueSeries/SeriesUpdate,DataSeries<TKey,TValue>,ArraySeries + CounterWidening,CyclicSeries (MinMean/MaxMean),SeriesAggregator + SeriesGroup,[Aggregable] + IAggregableSource + AggregableSources,basic RemoteDataProvider (channel + PeriodicTimer)
!fixed+pinned::pooled interval in lazy view,integer rate,first rate vs default key,keys going back,caller array stored,stale channel rate,check-then-add register,id index losing series
!gotchas::CA1716 (VB keywords) → NextKey(),Distance(earlier,later);CA1822 on throwing test getters

[2026-10-26 03/04/05 stages B,C,D ✓]
✓B statistics (370 tests),C notification+validation+edit copy+messaging (402),D Avalonia UI base (439):see each task's notes.md;removed the dependency locks and structural guard tests nobody decided (user)
