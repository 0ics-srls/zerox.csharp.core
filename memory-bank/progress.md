§MBEL:5.0

# Active task progress only

[05-port-avalonia]
✓notes.md + plan (4 blocks,37 red lines);review:parsePlan valid,red count matches,headless probe green
✓01 property-container:Avalonia ComponentModel/{PropertyContainer : NotifyingObject (MemberChanged at once,PropertyChanged posted to UI,throttle+debounce,StopEventThrottling sends waiting,EventsOff/On/OnAndNotifyAll,SetComplexProperty lets the old value go,Dispose),ThrottlePump (one TimeProvider timer per (clock,interval),tick posts flush),PendingChange};12 tests (4 ported,deterministic on fake clock + RunJobs);leak test fixed (old asserted IsAlive)
!gotcha::Avalonia analyzer AVA2001 flags any OnPropertyChanged override not calling base → suppressed on NotifyingObject's seam with reason
