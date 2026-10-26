# 04-port-messaging-editing — notes

## Requirement

Stage C of the port of the old Zerox code (A foundations and B statistics are done): a minimal
change-notification base, validation rules, the edit-copy proxy and the point-to-point message
channel. The source is the old code at `assetforge/tmp/zerox` (never edited):
`Zerox.Wpf.Core/Core/Containers/Validation/*`, `Containers/PocoProxy.cs`, `Support/PocoExtensions.cs`,
`Messaging/MessagesContext.cs`, the validation part of `Wpf/Popup/DialogPopupContentValidableBaseVM.cs`;
tests `Zerox.Wpf.XTest.Core/Core/PropertyContainers/PocoProxyTest.cs`,
`Core/Wpf/Popup/PropertyValidationTest.cs`, `Core/Messaging/MessagingTest.cs`.

Decisions taken with the user (2026-10-26):
- a MINIMAL notification base lives in `Zerox.Core` (`NotifyingObject`: `INotifyPropertyChanged`,
  `SetProperty`, no throttle, no dispatcher); stage D extends it with the throttle pump and the UI
  thread;
- the edit copy keeps the old libraries: FastDeepCloner 1.3.6 (deep clone by fields) and
  ObjectsComparer 1.4.1 (comparison with the differences member by member), from NuGet, in
  `Zerox.Core` (checked on .NET 10: clean build, NuGet audit clean, same behaviour);
- the message channel keeps the message in memory only (the old `PersistMessage`/`LoadMessage`
  hooks had no implementation and swallowed exceptions).

## Code Organization

1. **What is shared.** `NotifyingObject` is the one change notification of the library: the edit
   copy and the channel derive from it, stage D's property container extends it. `JsonDefaults`
   (stage A) is the one JSON setup: the edit copy serializes with it, so it now also reads and
   writes public fields (the old POCOs have fields).
2. **Who owns it.** `src/Zerox.Core/ComponentModel` (`NotifyingObject`),
   `src/Zerox.Core/Validation` (rules, validator, parameters, exception),
   `src/Zerox.Core/Editing` (clone/compare extensions, `PocoProxy<T>`),
   `src/Zerox.Core/Messaging` (message, source, destination, channel). Tests mirror the folders.
3. **Contracts.** Interfaces only with several implementations: `IValidatable` (any object the
   validator walks into), `IMessageSource<TKind>` / `IMessageDestination<TKind>` (the nodes at the
   two ends of a channel). The validator is a component the validated object holds (composition):
   it implements `INotifyDataErrorInfo`, the object forwards to it, so a view model of stage D can
   be validated without a base class of validation.
4. **What this stage does not own.** Throttled notifications, `InvalidateCommands`, dialogs and
   message boxes (`MessagingContext`, `MessageServiceBase`) → stage D.

## Inventario — capacità e dove esistono già

| Capability (what it does) | Old code | Here / .NET 10 | Decision |
|---|---|---|---|
| Raise `PropertyChanged`, set a backing field and notify only on a real change | `Containers/PropertyContainer.cs` (606 lines, dispatcher + throttle pump + `InvalidateCommands`), `MemberContainer` | `INotifyPropertyChanged` | minimal `NotifyingObject` here (user decision); the pump and the dispatcher go to stage D |
| Validation rule on a property: message + predicate, or a predicate that produces its message | `IRule`, `Rule<T>`, `DelegateRule<T>` (the extended rule wrote its message into a shared field: two threads, last message wins) | — | ONE `ValidationRule<T>` (sealed) whose `Apply` returns the error or null |
| Rules of a type, applied for one property or all | `IRuleCollection`, `RuleCollection<T>` | — | `ValidationRules<T>` |
| Errors per property, `HasErrors`, `ErrorsChanged`, children validated, parameters passed down to children | `ValidableBase<T>` + a second copy of the same map in `DialogPopupContentValidableBaseVM` | `INotifyDataErrorInfo` | ONE `Validator<T>` (composition, `INotifyDataErrorInfo`); `IValidatable`; `ValidationParameters`; fix: the two copies of the error map disagreed, the old no-rules test asserted on the text of a `List` |
| Exception carrying validation errors | `ValidationException(IValidable)` | — | `ValidationException` with `Errors` and the standard constructors |
| Deep clone by fields; comparison with differences | `Support/PocoExtensions.cs` (`PocoCloneByFields`, `IsChanged`) on FastDeepCloner / ObjectsComparer (compiled from source) | NuGet FastDeepCloner 1.3.6, ObjectsComparer 1.4.1 | port as `PocoExtensions.CloneByFields`, `IsChanged`, `IsChanged(out differences)`; differences as our `MemberDifference` record (package types do not leak); `ApplicationException` → `ArgumentNullException` |
| Edit copy: current data + saved state, `IsChanged`, save, reset, clone of another proxy, data equality, JSON | `Containers/PocoProxy.cs` (+ `PocoProxyTest`, 3 tests) on Newtonsoft | `JsonDefaults` (stage A) | port on `NotifyingObject` + System.Text.Json; `IsChanged` notified only on a real change; a failed populate leaves the data unchanged without a catch (deserialize first, then copy) |
| Point-to-point channel: weak source and destination, one current message, receive, send if accepted | `Messaging/MessagesContext.cs` (+ `MessagingTest`) | — | port as `MessageChannel<TKind>` on `NotifyingObject`, async receive/send (everything async), `Send` reports whether it delivered; no persistence (user decision); `IJsonMessage`/`ISerializableIntoJson` (Newtonsoft) dropped: a message carries `object? Data` |
| Empty `MessagesContext`, `IMessageContext` | `Messaging/MessagesContext.cs` | — | drop (empty) |
| Message boxes, notify/confirm/error dialogs | `Wpf/MessagingContext.cs`, `Wpf/Services/MessageServiceBase.cs` | — | stage D (Avalonia) |
