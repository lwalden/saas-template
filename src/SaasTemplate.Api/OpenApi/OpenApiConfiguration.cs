using Microsoft.OpenApi;

namespace SaasTemplate.Api.OpenApi;

/// <summary>
/// OpenAPI document configuration for the public API surface (FEAT-16).
///
/// The current API surface is treated as <c>v1</c>: the document is exposed at
/// <c>/openapi/v1.json</c> with the title "SaasTemplate API" and version "v1".
///
/// Versioning strategy (low-risk): existing routes are left exactly as-is and are
/// documented as v1. Future breaking versions should be introduced under a
/// <c>/api/v2/...</c> URL-segment prefix (or an <c>Api-Version</c> header) and exposed as a
/// separate OpenAPI document (e.g. <c>/openapi/v2.json</c>). No existing unversioned
/// route paths are rewritten by this feature.
/// </summary>
public static class OpenApiConfiguration
{
    public const string DocumentName = "v1";
    private const string BearerSchemeId = "Bearer";

    public static IServiceCollection AddPublicOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "SaasTemplate API",
                    Version = "v1",
                    Description =
                        "Public JSON/JWT API for SaasTemplate. " +
                        "Authenticate with a Bearer JWT obtained from the /api/auth endpoints."
                };

                // Security schemes. JWT bearer is the current scheme.
                // FEAT-06 EXTENSION POINT: when end-user API keys land, register an
                // additional "ApiKey" security scheme here (e.g. an X-Api-Key header
                // scheme) and reference it from the API-key-protected operations.
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerSchemeId] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT bearer token. Format: 'Authorization: Bearer {token}'."
                };

                return Task.CompletedTask;
            });

            // Attach the Bearer requirement to operations whose endpoint requires authorization.
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                    .Any();

                if (requiresAuth)
                {
                    operation.Security ??= new List<OpenApiSecurityRequirement>();
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerSchemeId, context.Document, null)] =
                            new List<string>()
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
