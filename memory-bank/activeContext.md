§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DEVELOP (CVM run-20261026-zerox-02 from the assetforge session;13/13 blocks done,final review next)
@feature::02-port-foundations (stage A of 4:A foundations,B statistics,C messaging+editing,D Avalonia)
@branch::feature/02-port
@plan::tasks/02-port-foundations/index.md (TDDAB multi-file,13 blocks,117 red lines;notes.md = inventory + code organization)
@source::old code read-only at ../../tmp/zerox (assetforge/tmp/zerox)
@rules::ported tests keep their names (may keep xunit Assert);new tests Should_X_WhenY + AwesomeAssertions;bugs found while reading fixed + pinned by a test

[NEXT]
?stage B 03-port-statistics,C 04-port-messaging-editing,D 05-port-avalonia (each its own notes + plan + review + CVM)
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
