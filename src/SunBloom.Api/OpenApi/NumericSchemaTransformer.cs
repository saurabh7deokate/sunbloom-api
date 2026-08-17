using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SunBloom.Api.OpenApi;

/// <summary>
/// Narrows numeric schemas that .NET declares as <c>["integer", "string"]</c> down to
/// the numeric type alone.
/// </summary>
/// <remarks>
/// .NET 10 emits integer properties as a union of integer and string with a digit
/// pattern, permitting a string form the API never actually produces. Generated
/// TypeScript therefore types every number as <c>string | number</c>, forcing a coercion
/// at each use site.
/// <para>
/// That is worth correcting here rather than in the client. Scoring is the heart of this
/// product (SCORING.md) and is numeric throughout — levels, readiness, confidence — so
/// left alone this would spread a needless union across every score the UI renders. The
/// schema should describe what the API sends, and the API sends JSON numbers.
/// </para>
/// </remarks>
internal sealed class NumericSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is not { } type || !type.HasFlag(JsonSchemaType.String))
        {
            return Task.CompletedTask;
        }

        if (type.HasFlag(JsonSchemaType.Integer))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Pattern = null;
        }
        else if (type.HasFlag(JsonSchemaType.Number))
        {
            schema.Type = JsonSchemaType.Number;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
