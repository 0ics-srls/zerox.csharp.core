§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::PLAN
@feature::01-scaffold
@branch::main (new repository,first task)
@goal::solution skeleton,build + test foundations,headless UI test session,verify script,CLAUDE.md + README;no ported code yet
@plan::tasks/01-scaffold/plan.md (TDDAB,3 blocks)

[NEXT]
✓j-review-plan done (RepositoryRoot fix);!executed by the assetforge session through CVM,working in assetforge/externals/zerox.csharp.core
?task 02-port::inventory piece by piece from assetforge/tmp/zerox (keep,fix,framework covers) → multi-file TDDAB plan (NUnit tests → xunit RED,then port + clean);order Collections+Async → Statistics → Reflection/declarative → Messaging → extensions → Avalonia → Dnf
?remote::github repo created by the user;then assetforge adds externals/zerox.csharp.core as submodule
