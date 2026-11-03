using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api.Tests;

// The one translation of an outcome into HTTP, in process: the status, the value, the problem with its Code;
// a Faulted outcome goes on to the error middleware as the exception it carries.
public sealed class AnswerTests : IAsyncLifetime
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

    public static TheoryData<OpStatus> RefusedStatuses => [OpStatus.Invalid, OpStatus.Failed];

    // A controller of the base, its Answer open to the tests, on the services of the application.
    private sealed class ProbeController(IEntityService<Plain> service) : EntityController<Plain>(service)
    {
        public ActionResult Translate<TValue>(OpResult<TValue> result) => Answer(result);
    }

    private ProbeController Probe() => new(new EntityService<Plain>(_scope.ServiceProvider.GetRequiredService<IRepository<Plain>>()))
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
        OpStatus.Failed => OpResult.Failed<string>("refused"),
        OpStatus.Faulted => OpResult.Faulted<string>(new InvalidOperationException("boom")),
        _ => throw new UnreachableException($"No outcome for {status}."),
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Boom() => throw new InvalidOperationException("boom");

    // An exception thrown for real, with its stack trace.
    private static InvalidOperationException Thrown()
    {
        try
        {
            Boom();
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
        throw new UnreachableException();
    }

    [Fact]
    public void Should_MapEveryStatus()
    {
        var probe = Probe();
        var answered = Enum.GetValues<OpStatus>().Where(s => s != OpStatus.Faulted);

        var translate = () => answered.Select(s => probe.Translate(Outcome(s))).ToList();

        translate.Should().NotThrow<UnreachableException>();
    }

    [Fact]
    public void Should_Answer200WithTheValue_WhenOk()
    {
        var answer = Probe().Translate(OpResult.Ok("value"));

        answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be("value");
    }

    [Fact]
    public void Should_Answer201WithTheValue_WhenCreated()
    {
        var answer = Probe().Translate(OpResult.Created("value"));

        var created = answer.Should().BeAssignableTo<ObjectResult>().Subject;
        (created.StatusCode, created.Value).Should().Be((StatusCodes.Status201Created, "value"));
    }

    [Fact]
    public void Should_Answer204_WhenDeleted()
    {
        var answer = Probe().Translate(OpResult.Deleted<string>());

        answer.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void Should_Answer404_WhenNotFound()
    {
        var answer = Probe().Translate(OpResult.NotFound<string>());

        answer.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Should_Answer409WithTheReasonAndCode_WhenConflict()
    {
        var answer = Probe().Translate(OpResult.Conflict<string>("changed"));

        var problem = answer.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var details = problem.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        details.Detail.Should().Be("changed");
        details.Extensions[ZeroxProblemDetails.CodeMember].Should().Be(nameof(ApiErrorCode.Conflict));
    }

    [Theory]
    [MemberData(nameof(RefusedStatuses))]
    public void Should_Answer400WithTheReasonAndCode_WhenInvalidOrFailed(OpStatus status)
    {
        var answer = Probe().Translate(Outcome(status));

        var problem = answer.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var details = problem.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        details.Detail.Should().Be(Outcome(status).Reason);
        details.Extensions[ZeroxProblemDetails.CodeMember].Should().Be(nameof(ApiErrorCode.InvalidRequest));
    }

    [Fact]
    public void Should_RethrowTheCapturedException_WhenFaulted()
    {
        var error = Thrown();
        var probe = Probe();

        var translate = () => probe.Translate(OpResult.Faulted<int>(error));

        var thrown = translate.Should().Throw<InvalidOperationException>().Which;
        thrown.Should().BeSameAs(error);
        thrown.StackTrace.Should().Contain(nameof(Boom));
    }
}
