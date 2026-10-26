§MBEL:5.0

# Active task progress only

[04-port-messaging-editing]
✓notes.md + plan (5 blocks,39 red lines);review:parsePlan valid,red 39=39,packages probed on net10
✓01 notifying-object:ComponentModel/NotifyingObject (virtual SetProperty returns changed,NotifyPropertyChanged(null = all),NotifyPropertiesChanged(params ReadOnlySpan),virtual OnPropertyChanged = the seam for stage D's pump);6 tests
✓02 validation:Validation/{RuleWithError delegate,ValidationRule<T> (Apply returns error|null),ValidationRules<T> (Apply null=all,""=whole object,name),ValidationParameters,IValidatable,Validator<T> (composition,INotifyDataErrorInfo,children IValidatable + IEnumerable<IValidatable>,ErrorsChanged once per changed property outside the lock),ValidationException};9 tests (2 ported)
✓03 poco-clone-compare:Editing/PocoExtensions (CloneByFields on FastDeepCloner 1.3.6 FieldInfo,IsChanged via IComparable<T> or ObjectsComparer 1.4.1,IsChanged out MemberDifference record) + JsonDefaults IncludeFields=true;packages in Directory.Packages.props + Zerox.Core.csproj,audit clean;7 tests
✓04 poco-proxy:Editing/PocoProxy<T> on NotifyingObject (protected Data:fields by ref,properties via SetProperty(x => x.P) on FastProperty;IsChanged notified only on real change;SaveState/ResetState/GetState clone/SetState/DataEquals/SaveStateOf;ToJson/Populate/SerializeAsync/PopulateAsync on JsonDefaults,populate deserializes first so no catch);9 tests (3 ported)
✓05 message-channel:Messaging/{Message<TKind> (Guid identity),IMessageSource (ProduceMessageAsync),IMessageDestination (CanAccept + AcceptAsync),MessageChannel<TKind> on NotifyingObject (weak source/destination,one message in memory,ReceiveAsync/SendAsync report bool,SendAndClearAsync clears always,notifications)};8 tests (1 ported),GC test stable 3 runs
