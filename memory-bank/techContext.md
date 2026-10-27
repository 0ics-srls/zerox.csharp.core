§MBEL:5.0

# Technical facts

[STACK]
@dotnet::SDK 10.0.400 (global.json latestFeature),C# 14,zerox.csharp.core.slnx,CPM + transitive pinning,artifacts output,version.build → 1.0.<n>
@tests::xunit.v3.mtp-v2 4.0.2 + AwesomeAssertions 9.6.0,Microsoft Testing Platform;names Should_<Outcome>_When<Condition>
@ui::Avalonia 12.1.4 + Avalonia.Headless 12.1.4 (HeadlessUnitTestSession + plain [Fact];Avalonia.Headless.XUnit 12.1.4 incompatible with xunit v3 4.x);UI thread in tests = SynchronizationContext.Current is AvaloniaSynchronizationContext,never Dispatcher.UIThread.CheckAccess() (true on any thread headless)
@lint::analyzers latest-recommended as errors;.editorconfig CA2007/CS8618/CA1849/IDE0005 errors (tests:CA2007,CA1707,CA1515 off);dotnet format --verify-no-changes;ast-grep via .claude/support/lint/lint-run.sh (sgconfig.yml)
@location::working copy = assetforge submodule externals/zerox.csharp.core (origin = local repo ~/dev/zerox.csharp.core,no GitHub remote yet,never push);.claude links recreated by hand after clone
@ai-agent::.ai-agent submodule (branch feature/08-audit-coherence),setup.sh → .claude links;MCP lsai + cvm (.mcp.json)

[CATALOG]
@docs/catalog.md::what the library offers by need (type + file),entry point for LSAI lookups
!rule::public surface changed → catalog updated in same commit

[NOT PORTED]
!old source copy deleted 2026-10-26 (©User:"se servirà vado a recuperare il codice") → ask the user for the old code when a stage needs it
?charts (own stage,ScottPlot.Avalonia 5)::old Zerox.Wpf.Core/Core/Data/{ChartSeriesSource,IChartDataSourceTP (IChartDataSource*,IChartDataProvider),MixClassPalette,HistCurveSegments,DataSeriesBase (DataSeriesTag colors)} + ViewBars,View3D,ActivateForChart;Core/Wpf/Renderable/ScottPlotWrittableBitmapRenderable
✗Zerox.Wpf.Danger::never ported (10 files:MbfWrapper photo blobs,SimulatedShootSynthesizer,MyColors,MetaTypeEnum,IElaboration,IImageWrapper,IByteBuffer/DataWithSize,NetWorkAround Windows hooks = product/Windows code;LittleEndianNumbers → BinaryPrimitives)
✗dropped by decision::ActionsContext,Auth* commands,P80 machine code,Mongo persistor

[COMMANDS]
@build::dotnet build zerox.csharp.core.slnx -warnaserror
@fast-tests::dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"
@all-tests::dotnet test --solution zerox.csharp.core.slnx
@verify::tools/verify.sh ([k/4] build,format,tests,ast-grep)
