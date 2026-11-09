using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Swagger;

/// <summary>Describes IFormFile parameters as multipart file uploads in Swagger.</summary>
public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(parameter => parameter.ParameterType == typeof(IFormFile)
                || parameter.ParameterType == typeof(IEnumerable<IFormFile>))
            .ToList();

        if (fileParams.Count == 0)
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = fileParams.ToDictionary(
                            parameter => parameter.Name!,
                            _ => (IOpenApiSchema)new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }),
                        Required = fileParams.Select(parameter => parameter.Name!).ToHashSet()
                    }
                }
            }
        };
    }
}
