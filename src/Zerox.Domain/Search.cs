using DevExtreme.AspNet.Data;

namespace Zerox.Domain;

// The DevExtreme search shared by every grid and every client of the load options.
public static class Search
{
    // The options a client does not decide: no synchronous code on the database, grouping and
    // projection done by the provider (with EF, in SQL: false would bring the table into memory).
    public static void MakeSafe(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Skip < 0 || options.Take < 0)
            throw new InvalidRequestException("Skip and Take cannot be negative.");
        options.AllowAsyncOverSync = false;
        options.RemoteGrouping = null;
        options.RemoteSelect = null;
    }
}
