§MBEL:5.0

# Architecture

[PROJECTS]
@Zerox.Core::no UI;areas Async,Cancellation,Collections,Statistics,Messaging,Reflection,Text,Numbers,Identity,Validation,Editing
@Zerox.Core.Avalonia::Avalonia 12.1.4;references Zerox.Core;PropertyContainer + shared throttle pump,async commands
@Zerox.Dnf::boolean expression → DNF masks → compiled rules
@tests::tests/<Project>.Tests per project (xunit v3 + AwesomeAssertions on MTP);Avalonia tests through HeadlessUnitTestSession

[RULES]
!KISS::no interface without a consumer that substitutes it;no registration method without something to wire;no base applied by dogma
!DRY::one implementation per capability (binary search on ReadOnlySpan,one circular buffer,one change notification,one JSON)
!SOLID::S one actor per class;L no deep hierarchies,composition,no NotImplemented subclasses;I small interfaces seen by the user;D abstraction owned by its consumer;O only on real axes of change
!P9::expected failure = Try-pattern or result;misuse = ArgumentException;unexpected exception never caught to go on
!DI::constructor injection,no container inside components
!visibility::internal sealed by default,public is a decision
