using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests;

// The transport never touches the storage: the controller of an entity is built on its service, and the
// storage members of the old base are gone.
public sealed class ControllerSurfaceTests
{
    private const BindingFlags Every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Fact]
    public void Should_TakeTheServiceNotTheRepository_WhenTheControllerIsBuilt()
    {
        var constructors = typeof(EntityController<>).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var parameters = constructors.Should().ContainSingle().Which.GetParameters();

        parameters.Should().ContainSingle().Which.ParameterType.GetGenericTypeDefinition().Should().Be(typeof(IEntityService<>));
    }

    [Fact]
    public void Should_ExposeNoStorage_WhenTheControllerIsInspected()
    {
        var names = typeof(EntityController<>).GetMembers(Every).Select(m => m.Name);

        names.Should().NotContain(["Repository", "SaveUnlessMissingAsync", "SearchSource", "MaxTake"]);
    }

    [Fact]
    public void Should_BindTheLoadOptions_AsTheServiceType()
    {
        var load = typeof(EntityController<Plain>).GetMethod(nameof(EntityController<Plain>.Load))!;

        var shape = (Body: load.GetParameters().Single(p => p.GetCustomAttribute<FromBodyAttribute>() is not null).ParameterType, Result: load.ReturnType);

        shape.Body.Should().Be<LoadOptions>();
        shape.Result.Should().Be<Task<ActionResult<Loaded>>>();
    }
}
