§MBEL:5.0

# Why zerox.csharp.core exists

[VISION]
@zerox.csharp.core::reusable C# core of 0ics/Ladislav Sopko,rebuilt clean from the old Zerox.Wpf.Core + Zerox.Dnf (source copy at assetforge/tmp/zerox deleted 2026-10-26;old code retrieved by the user on request,see techContext [NOT PORTED])
@consumers::assetforge first (submodule externals/zerox.csharp.core,project references);any later C# project

[PROBLEMS SOLVED]
!old Zerox::tied to WPF,Windows,Autofac service locator,Newtonsoft,P80 machine domain;deep inheritance;swallowed exceptions → reuse impossible
!kept capabilities::serial owner queue,critical section usable across await,cancellation tree with timeout,atomic boolean,arbitrary-length bit mask,lower/upper bound + first/last binary search,circular buffer,interval views,realtime statistics stack (series,cyclic series,aggregator,[Aggregable] declarative discovery,remote provider),message channels,reflection extensions + compiled property accessors,hierarchical id,typed numeric interval math,string/type/conversion extensions,validation rules,edit-copy proxy;UI:throttled property notifications,async commands;Dnf boolean rule compiler

[USERS]
@the user::Ladislav Sopko,senior developer (40 years);decides design;conversation Italian,repository English
!user rules::questions only via AskUserQuestion;commit only when asked or by the plan;push to origin (GitHub 0ics-srls) so no work lives in one copy only;never delete branches;KISS,DRY,SOLID as heuristics (signal = real or very likely change);no exception hidden (P9)

[SUCCESS]
✓criteria::every kept capability ported with its tests green on xunit v3;zero warnings;assetforge consumes it as submodule
