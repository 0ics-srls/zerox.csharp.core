§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DONE stage B (CVM run-20261026-zerox-03,8/8 blocks,verify.sh 4/4,370 tests,0 warnings);next stage C 04-port-messaging-editing
@feature::03-port-statistics (stage B of 4:A foundations ✓,B statistics,C messaging+editing,D Avalonia)
@branch::feature/03-port-statistics (from feature/02-port)
@plan::tasks/03-port-statistics/index.md (TDDAB multi-file,8 blocks,85 red lines;notes.md = inventory + code organization)
@source::old code read-only at ../../tmp/zerox (assetforge/tmp/zerox):Core/Data,Collections/IInterval.cs,tests CircularBufferTest + DataTests
@decisions::©User>remote provider = basic testable implementation (HalRemoteDataProvider absent in the copy);cyclic Min/Max of the moving mean kept as MinMean/MaxMean,Min/Max = window values
@design::Collections/{Interval<T>,CircularBuffer<T>,KeyComparer};Statistics/{ISeriesKeys(TimeKeys,IndexKeys),ISeries,DataSeries<TKey,TValue>,ArraySeries + CounterWidening,CyclicSeries,SeriesAggregator + SeriesGroup,AggregableSources + [Aggregable] + IAggregableSource,RemoteDataProvider};persistence = Snapshot/Load (consumer stores);charts/colors/notifications → stage D

[NEXT]
?stage C 04-port-messaging-editing,D 05-port-avalonia
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
