using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Zerox.Core.ComponentModel;
using Zerox.Core.Reflection;
using Zerox.Core.Text;

namespace Zerox.Core.Editing;

// An edit copy of a plain object: the data being edited and the saved state; IsChanged compares them.
public abstract class PocoProxy<T> : NotifyingObject
    where T : class, new()
{
    private T _saved;

    protected PocoProxy()
    {
        Data = new T();
        _saved = new T();
    }

    protected PocoProxy(PocoProxy<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Data = source.Data.CloneByFields();
        _saved = source.Data.CloneByFields();
    }

    public bool IsChanged => Data.IsChanged(_saved);

    // The data being edited (fields by ref: SetProperty(ref Data.Name, value)).
    protected T Data { get; private set; }

    public T GetState() => Data.CloneByFields();

    public void SetState(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Replace(state.CloneByFields());
    }

    public void SaveState()
    {
        _saved = Data.CloneByFields();
        NotifyPropertyChanged(nameof(IsChanged));
    }

    public void ResetState() => Replace(_saved.CloneByFields());

    public bool DataEquals(PocoProxy<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !Data.IsChanged(other.Data);
    }

    public Task SerializeAsync(Stream stream, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(stream, Data, JsonDefaults.Options, cancellationToken);

    // Deserializes first: a bad document leaves the data as it was.
    public async Task PopulateAsync(Stream stream, CancellationToken cancellationToken = default)
        => Replace(await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false)
                   ?? throw new JsonException("The document holds no data"));

    protected override bool SetProperty<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)
    {
        if (!base.SetProperty(ref field, value, propertyName))
            return false;
        NotifyPropertyChanged(nameof(IsChanged));
        return true;
    }

    // For a data object with properties: SetProperty(x => x.Name, value).
    protected bool SetProperty<TValue>(Expression<Func<T, TValue>> member, TValue value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        var property = (member.Body as MemberExpression)?.Member as PropertyInfo
            ?? throw new ArgumentException("The expression must select a property of the data", nameof(member));
        var accessor = FastProperty.For<T, TValue>(property);
        if (EqualityComparer<TValue>.Default.Equals(accessor.Get(Data), value))
            return false;
        accessor.Set(Data, value);
        NotifyPropertyChanged(propertyName);
        NotifyPropertyChanged(nameof(IsChanged));
        return true;
    }

    // Saves one field or property only; the other members keep their saved state.
    protected void SaveStateOf(string member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var type = typeof(T);
        if (type.GetField(member) is { } field)
            field.SetValue(_saved, field.GetValue(Data));
        else if (type.GetProperty(member) is { } property)
            property.SetValue(_saved, property.GetValue(Data));
        else
            throw new ArgumentException($"{type.Name} has no member {member}", nameof(member));
        NotifyPropertyChanged(nameof(IsChanged));
    }

    private void Replace(T data)
    {
        Data = data;
        NotifyPropertyChanged(propertyName: null);
    }
}
