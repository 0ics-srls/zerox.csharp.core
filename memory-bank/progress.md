§MBEL:5.0

# Active task progress only

[01-scaffold]
✓repository created,.ai-agent submodule + setup,j-settings.md + lint config (sgconfig.yml,Directory.Build.props + .editorconfig templates)
✓plan written:tasks/01-scaffold/plan.md (3 TDDAB blocks)
✓j-review-plan (RepositoryRoot in assetforge's shape)
✓01 solution-skeleton:global.json,version.build (1),Directory.Build.props/targets/Packages.props,.editorconfig (+tests overrides),.gitignore,zerox.csharp.core.slnx,3 empty src + 3 test projects,tests/Shared/RepositoryRoot.cs linked;7 tests green,build 0 warnings,format+ast-grep clean
!gotcha::an XML comment in a .props must not contain '--' (MSB4024 → restore fails with a silent RestoreTask error)
✓02 headless-ui-session:TestApp (plain Application on headless),HeadlessUi.RunAsync (assetforge shape,session per assembly),4 tests + version = 5 green
!gotcha::headless Dispatcher.UIThread.CheckAccess() is true on ANY thread (UIThread bound to first toucher,reset by the session) → recognise the UI thread by SynchronizationContext.Current is AvaloniaSynchronizationContext (null outside);plan RED reworded in place
!fact::HeadlessUnitTestSession runs on a thread-pool thread,completes a plain TaskCompletionSource,rethrows the body's exception unchanged (no AggregateException)
⚡03 verify-and-docs next
