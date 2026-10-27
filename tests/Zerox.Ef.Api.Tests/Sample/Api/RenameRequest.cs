namespace Zerox.Ef.Api.Tests.Sample.Api;

// The shape of one action's request (not an entity): the new name and the version the client read.
public sealed record RenameRequest(string Name, int Version);
