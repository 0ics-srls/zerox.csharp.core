§MBEL:5.0

# Active task progress only

[04-port-messaging-editing]
✓notes.md + plan (5 blocks,39 red lines);review:parsePlan valid,red 39=39,packages probed on net10
✓01 notifying-object:ComponentModel/NotifyingObject (virtual SetProperty returns changed,NotifyPropertyChanged(null = all),NotifyPropertiesChanged(params ReadOnlySpan),virtual OnPropertyChanged = the seam for stage D's pump);6 tests
✓02 validation:Validation/{RuleWithError delegate,ValidationRule<T> (Apply returns error|null),ValidationRules<T> (Apply null=all,""=whole object,name),ValidationParameters,IValidatable,Validator<T> (composition,INotifyDataErrorInfo,children IValidatable + IEnumerable<IValidatable>,ErrorsChanged once per changed property outside the lock),ValidationException};9 tests (2 ported)
