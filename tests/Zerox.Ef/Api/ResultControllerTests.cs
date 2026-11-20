using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests;

// A controller that is not the controller of an entity answers an outcome with the same translation: it
// derives from ResultController and gets Answer, and nothing else — no action, no route, no service.
public sealed class ResultControllerTests : IAsyncLifetime
{
    private TestHost? _host;
    private AsyncServiceScope _scope;

    private TestHost Host => _host ?? throw new InvalidOperationException("InitializeAsync has not run.");

    public async ValueTask InitializeAsync()
    {
        _host = await TestHost.StartAsync(_ => { });
        _scope = Host.Services.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await Host.DisposeAsync();
    }

    public static TheoryData<OpStatus, int> AnsweredStatuses => new()
    {
        { OpStatus.Ok, StatusCodes.Status200OK },
        { OpStatus.Created, StatusCodes.Status201Created },
        { OpStatus.Deleted, StatusCodes.Status204NoContent },
        { OpStatus.NotFound, StatusCodes.Status404NotFound },
        { OpStatus.Conflict, StatusCodes.Status409Conflict },
        { OpStatus.Invalid, StatusCodes.Status400BadRequest },
        { OpStatus.Failed, StatusCodes.Status400BadRequest },
    };

    // A controller with no entity and no service: only the translation, open to the tests.
    private sealed class ProbeController : ResultController
    {
        public ActionResult Translate<TValue>(OpResult<TValue> result) => Answer(result);
    }

    private ProbeController Probe() => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = _scope.ServiceProvider } },
    };

    private static OpResult<string> Outcome(OpStatus status) => status switch
    {
        OpStatus.Ok => OpResult.Ok("value"),
        OpStatus.Created => OpResult.Created("value"),
        OpStatus.Deleted => OpResult.Deleted<string>(),
        OpStatus.NotFound => OpResult.NotFound<string>(),
        OpStatus.Conflict => OpResult.Conflict<string>("changed"),
        OpStatus.Invalid => OpResult.Invalid<string>("wrong"),
        _ => OpResult.Failed<string>("refused"),
    };

    [Theory]
    [MemberData(nameof(AnsweredStatuses))]
    public void Should_AnswerTheStatusOfTheOutcome_WhenTheControllerHasNoEntity(OpStatus status, int expected)
    {
        var answer = Probe().Translate(Outcome(status));

        answer.Should().BeAssignableTo<IStatusCodeActionResult>().Which.StatusCode.Should().Be(expected);
    }

    [Fact]
    public void Should_AnswerTheProblemWithTheReasonAndCode_WhenConflict()
    {
        var answer = Probe().Translate(OpResult.Conflict<string>("changed"));

        var details = answer.Should().BeAssignableTo<ObjectResult>().Which.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        details.Detail.Should().Be("changed");
        details.Extensions[ZeroxProblemDetails.CodeMember].Should().Be(nameof(ApiErrorCode.Conflict));
    }

    [Fact]
    public void Should_RethrowTheException_WhenFaulted()
    {
        var error = new InvalidOperationException("boom");
        var probe = Probe();

        var translate = () => probe.Translate(OpResult.Faulted<int>(error));

        translate.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(error);
    }

    [Fact]
    public void Should_BeTheBaseOfTheEntityController_SoTheTranslationIsWrittenOnce()
    {
        var entityController = typeof(EntityController<>);

        var declaresItsOwn = entityController.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == "Answer");

        (entityController.BaseType, declaresItsOwn).Should().Be((typeof(ResultController), false));
    }

    [Fact]
    public void Should_BringNoActionAndNoRoute_WhenAControllerDerivesFromIt()
    {
        var type = typeof(ResultController);

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any());
        var routes = type.GetCustomAttributes<RouteAttribute>(inherit: false);

        (actions.Count(), routes.Count()).Should().Be((0, 0));
    }
}
