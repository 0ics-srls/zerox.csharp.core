using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Zerox.Ef.Api;

/// <summary>
/// The OpenAPI document a client is generated from: every operation named, the load of every entity
/// documented with the entity it lists, a day typed as the day it is.
/// </summary>
public static class ZeroxOpenApi
{
    /// <summary>
    /// The name of the schema of the groups a load returns.
    /// </summary>
    public const string GroupSchema = "LoadGroup";

    /// <summary>
    /// The OpenAPI document of the application (AddOpenApi) with the transformers of Zerox.Ef.
    /// </summary>
    public static IServiceCollection AddZeroxOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<OperationNames>();
            options.AddOperationTransformer<Responses>();
            options.AddOperationTransformer<LoadResults>();
            options.AddSchemaTransformer<Days>();
            options.AddDocumentTransformer<UnusedLoadResult>();
        });
    }

    /// <summary>
    /// The name of the schema of the result of the load of an entity.
    /// </summary>
    public static string ResultSchema(Type entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return "LoadResultOf" + entity.Name;
    }

    // Every controller action is named {Controller}_{Action} (Customers_Load, Customers_GetById): the
    // generated client calls its methods by that name, and the name is unique in the document.
    private sealed class OperationNames : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor is ControllerActionDescriptor action)
                operation.OperationId ??= $"{action.ControllerName}_{action.ActionName}";
            return Task.CompletedTask;
        }
    }

    // The answers the platform cannot see, all errors as the one ProblemDetails of AddZeroxProblemDetails:
    // 400 and 500 on every action (the error middleware, a failed binding), and those of the actions of
    // EntityController<T> — 201 of a creation, 204 of a deletion, 404 of a missing row, 409 of a stale
    // Version. Declared here and not with attributes: an attribute on the base would hide the success
    // answer the platform infers for every action of a project. An error a project's action declares is
    // documented as the same ProblemDetails.
    private sealed class Responses : IOpenApiOperationTransformer
    {
        private const string ProblemMediaType = "application/problem+json";

        public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor is not ControllerActionDescriptor action || context.Document is not { } document)
                return;

            await ProblemSchemaAsync(document, context, cancellationToken).ConfigureAwait(false);
            var responses = operation.Responses ??= [];
            responses.TryAdd("400", Problem(document, "The request is invalid: the detail says why."));
            responses.TryAdd("500", Problem(document, "An unexpected error."));

            if (action.MethodInfo.GetBaseDefinition().DeclaringType is { IsGenericType: true } declaring
                && declaring.GetGenericTypeDefinition() == typeof(EntityController<>))
            {
                switch (action.ActionName)
                {
                    case nameof(EntityController<>.Create):
                        if (responses.Remove("200", out var created))
                            responses["201"] = created;
                        break;
                    case nameof(EntityController<>.Delete):
                        responses.Remove("200");
                        responses["204"] = new OpenApiResponse { Description = "Deleted." };
                        responses["404"] = Problem(document, "No row with that Id.");
                        responses["409"] = Problem(document, "The row was changed by another user.");
                        break;
                    case nameof(EntityController<>.GetById):
                        responses["404"] = Problem(document, "No row with that Id.");
                        break;
                    case nameof(EntityController<>.Update):
                        responses["404"] = Problem(document, "No row with that Id.");
                        responses["409"] = Problem(document, "The row was changed by another user.");
                        break;
                }
            }

            foreach (var (status, response) in responses.ToList())
                if (int.TryParse(status, System.Globalization.CultureInfo.InvariantCulture, out var code) && code >= StatusCodes.Status400BadRequest)
                    responses[status] = Problem(document, response.Description ?? "An error.");
        }

        // The platform's ProblemDetails schema with the members AddZeroxProblemDetails and the framework add:
        // the code of the domain, and the errors per field of a failed binding.
        private static async Task ProblemSchemaAsync(OpenApiDocument document, OpenApiOperationTransformerContext context, CancellationToken ct)
        {
            if (document.Components?.Schemas?.TryGetValue(nameof(ProblemDetails), out var existing) == true && existing is OpenApiSchema { Properties: { } known } && known.ContainsKey(ZeroxProblemDetails.CodeMember))
                return;
            if (document.Components?.Schemas?.ContainsKey(nameof(ApiErrorCode)) != true)
                document.AddComponent(nameof(ApiErrorCode), await context.GetOrCreateSchemaAsync(typeof(ApiErrorCode), null, ct).ConfigureAwait(false));
            var problem = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct).ConfigureAwait(false);
            problem.Properties ??= new Dictionary<string, IOpenApiSchema>();
            problem.Properties[ZeroxProblemDetails.CodeMember] = new OpenApiSchemaReference(nameof(ApiErrorCode), document);
            problem.Properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            };
            document.Components ??= new OpenApiComponents();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            document.Components.Schemas[nameof(ProblemDetails)] = problem;
        }

        private static OpenApiResponse Problem(OpenApiDocument document, string description) => new()
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemMediaType] = new() { Schema = new OpenApiSchemaReference(nameof(ProblemDetails), document) },
            },
        };
    }

    // The load of EntityController<T> answers DevExtreme's LoadResult, whose data the platform cannot
    // type: the response is documented as LoadResultOf<T>, data being the rows of T (whole, or the fields
    // Select asks) or the groups, which carry keys, counts and sums and never rows.
    private sealed class LoadResults : IOpenApiOperationTransformer
    {
        public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor is not ControllerActionDescriptor { ActionName: nameof(EntityController<>.Load) } action
                || EntityOf(action.ControllerTypeInfo) is not { } entity
                || context.Document is not { } document)
                return;

            // The entity's own schema, registered in the document under its name.
            await context.GetOrCreateSchemaAsync(entity, null, cancellationToken).ConfigureAwait(false);
            var row = new OpenApiSchemaReference(entity.Name, document);
            var group = new OpenApiSchemaReference(GroupSchema, document);
            var summary = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null } };
            var groups = new OpenApiSchema { Type = JsonSchemaType.Array, Items = group };
            document.AddComponent(GroupSchema, new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "key", "items" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["key"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Number | JsonSchemaType.Boolean | JsonSchemaType.Null },
                    ["items"] = new OpenApiSchema { OneOf = [groups, new OpenApiSchema { Type = JsonSchemaType.Null }] },
                    ["count"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    ["summary"] = summary,
                },
            });
            document.AddComponent(ResultSchema(entity), new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "data", "totalCount", "groupCount" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["data"] = new OpenApiSchema { OneOf = [new OpenApiSchema { Type = JsonSchemaType.Array, Items = row }, groups] },
                    ["totalCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    ["groupCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    ["summary"] = summary,
                },
            });
            if (operation.Responses?.TryGetValue("200", out var ok) == true)
                foreach (var media in ok.Content?.Values.AsEnumerable() ?? [])
                    media.Schema = new OpenApiSchemaReference(ResultSchema(entity), document);
        }

        internal static Type? EntityOf(Type? controller)
        {
            for (var type = controller; type is not null; type = type.BaseType)
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EntityController<>))
                    return type.GetGenericArguments()[0];
            return null;
        }
    }

    // A DateOnly travels through the converter of ZeroxJson, which the platform cannot read: without this
    // the schema has a format and no type, and a generated client sees an unknown value.
    private sealed class Days : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            var type = context.JsonTypeInfo.Type;
            if (type == typeof(DateOnly))
                (schema.Type, schema.Format) = (JsonSchemaType.String, "date");
            else if (type == typeof(DateOnly?))
                (schema.Type, schema.Format) = (JsonSchemaType.String | JsonSchemaType.Null, "date");
            return Task.CompletedTask;
        }
    }

    // DevExtreme's LoadResult, the declared type of every load, stays in the document only while a response
    // still uses it: its data has no type, and a generated client would carry it as unknown.
    private sealed class UnusedLoadResult : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var used = document.Paths?.Values
                .SelectMany(p => p.Operations?.Values.AsEnumerable() ?? [])
                .SelectMany(o => o.Responses?.Values.AsEnumerable() ?? [])
                .SelectMany(r => r.Content?.Values.AsEnumerable() ?? [])
                .Any(m => m.Schema is OpenApiSchemaReference { Reference.Id: nameof(LoadResult) }) ?? false;
            if (!used)
                document.Components?.Schemas?.Remove(nameof(LoadResult));
            return Task.CompletedTask;
        }
    }
}
