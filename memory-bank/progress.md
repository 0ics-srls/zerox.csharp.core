§MBEL:5.0

# Active task progress only

[06-port-scheduler]
✓notes.md + plan (4 blocks,33 red lines);review:parsePlan valid,red count matches,exact tick counts computed from the ring order
✓01 jobs:Scheduling/{JobKind,JobState,IJob,JobBase (Suspended → Ready ⇄ Running,suspend during run kept,resume during run stays running),Job (Cyclic/Once),DelayedJob (TimeProvider due time)};5 tests (1 ported)
!decision::CA1716 (VB keywords as member names) turned off in .editorconfig:no VB consumer;the old names (Resume) stay
