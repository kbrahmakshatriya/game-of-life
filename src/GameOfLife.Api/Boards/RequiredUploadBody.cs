using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameOfLife.Api.Boards;

/// <summary>
/// Marks the upload's request body as required in the OpenAPI document. The endpoint takes the body as nullable only so
/// that an empty body reaches the validation, which says the cells are missing; a request still needs a body.
/// </summary>
public sealed class RequiredUploadBody : IRequestBodyFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiRequestBody requestBody, RequestBodyFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (requestBody is OpenApiRequestBody body && context.BodyParameterDescription?.Type == typeof(UploadBoardRequest))
        {
            body.Required = true;
        }
    }
}
