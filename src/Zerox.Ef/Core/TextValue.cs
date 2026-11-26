namespace Zerox.Ef.Core;

/// <summary>
/// A distinct value of a text column and the number of rows that have it, among the rows that were asked.
/// </summary>
/// <param name="Value">The value, never empty.</param>
/// <param name="Count">How many rows have it.</param>
public sealed record TextValue(string Value, int Count);
