using System.Runtime.CompilerServices;

namespace Zerox.Db;

// The state of ONE context instance. It lives and dies with the context: attached to it through a
// ConditionalWeakTable, so nothing has to register it and nothing keeps a context alive.
internal sealed class DbSession
{
    private static readonly ConditionalWeakTable<object, DbSession> Sessions = [];

    internal static DbSession Of(object context) => Sessions.GetValue(context, static _ => new DbSession());
}
