using FastEndpoints;

namespace API.Features.ExampleFeature;

public class ExampleEndpoint : Endpoint<ExampleRequest, ExampleResponse>
{
    
    //FOR LEARNING PURPOSES OF THE OPEN API DOCUMENTATION WITH SWAGGER
    public override void Configure()
    {
        Post("/example");
        AllowAnonymous();
      // MANUAL SWAGGER DOCUMENTATION FOR LEARNING PURPOSES
      // GENERATED WITH CLAUDE 4.0 SONNET
        Description(b => b
            // ============ CONTENT TYPE CONFIGURATION ============
            
            // Accepts() - Specifies what content types this endpoint can consume
            // Default behavior: POST/PUT/PATCH accept "application/json"
            // This overrides the default to accept a custom content type
            .Accepts<ExampleRequest>("application/json+custom")
            
            // Alternative: Accept multiple content types
            // .Accepts<ExampleRequest>("application/json", "application/xml")
            
            // Alternative: Accept any content type for form-based requests
            // .Accepts<ExampleRequest>() // Accepts */*
            
            // ============ RESPONSE CONFIGURATION ============
            
            // Produces() - Specifies what content types this endpoint returns
            // First parameter: Response DTO type
            // Second parameter: HTTP status code  
            // Third parameter: Content type(s) it produces
            .Produces<ExampleResponse>(200, "application/json+custom")
            
            // Alternative: Standard JSON response
            // .Produces<ExampleResponse>(200, "application/json")
            
            // Alternative: Multiple content types for same response
            // .Produces<ExampleResponse>(200, "application/json", "application/xml")
            
            // ============ ERROR RESPONSE SHORTCUTS ============
            
            // ProducesProblemFE() - FastEndpoints' built-in error response
            // This is a shortcut for .Produces<ErrorResponse>(400)
            // Used for validation errors and standard 400 responses
            .ProducesProblemFE(400)
            
            // ProducesProblemFE<T>() - Custom error response type
            // Use when you have custom error response models
            // Generic parameter specifies your custom error DTO
            .ProducesProblemFE<InternalErrorResponse>(500)
            
            // Alternative: Manual error responses
            // .Produces<ValidationErrorResponse>(422, "application/json")
            // .Produces<NotFoundResponse>(404, "application/json")
            
            // ============ SWAGGER ORGANIZATION ============
            
            // WithTags() - Groups endpoints in Swagger UI
            // Helps organize your API documentation
            .WithTags("Example", "Demo", "Testing")
            
            // Alternative: Single tag
            // .WithTags("Example")
            
            // WithName() - Sets the Operation ID in OpenAPI spec
            // Used by code generators and for referencing this endpoint
            // .WithName("CreateExampleItem")
            
            // AutoTagOverride() - Override the auto-generated tag
            // .AutoTagOverride("Custom Group Name")
            
            // ============ ADVANCED CONFIGURATION ============
            
            // The clearDefaults parameter is CRUCIAL:
            // true = Removes ALL default Accept/Produce metadata
            // false = Keeps defaults and adds your specifications
            // 
            // Default metadata includes:
            // - 200 OK for endpoints with response DTOs
            // - 204 No Content for endpoints without response DTOs  
            // - 400 Bad Request if endpoint has validators
            // - 401 Unauthorized if endpoint requires authentication
            // - 403 Forbidden if endpoint has authorization requirements
            ,clearDefaults: true);
            
        // ============ ADDITIONAL CONFIGURATION OPTIONS ============
        
        // You can also chain other endpoint configuration:
        // .Throttle(10, TimeSpan.FromMinutes(1)) // Rate limiting
        // .Roles("Admin", "Manager") // Role-based security
        // .Claims("permission", "read-data") // Claim-based security
        // .Policies("RequireElevatedRights") // Policy-based security
    }

    public override async Task HandleAsync(ExampleRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ExampleResponse
        {
            FullName = $"{req.FirstName} {req.LastName}",
            IsOver18 = req.Age > 18,
        }, ct);
    }
}