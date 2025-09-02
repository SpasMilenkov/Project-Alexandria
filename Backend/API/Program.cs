using Data.Context;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Models;

var bld = WebApplication.CreateBuilder();

var connStr = bld.Configuration.GetConnectionString("AlexandriaPostgres");
bld.Services.AddDbContext<AlexandriaDbContext>(opt => opt.UseNpgsql(connStr));
bld.Services.AddAuthorization();
bld.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 107374182400; //set to max allowed file size of your system
});
var minioConfig = bld.Configuration.GetSection("Minio").Get<API.Config.MinioConfig>();

if(minioConfig is null) throw new InvalidOperationException("Minio configuration is missing");

bld.Services.Configure<API.Config.MinioConfig>(bld.Configuration.GetSection("Minio"));

// Manual MinIO client registration
bld.Services.AddSingleton<IMinioClient>(_ =>
{
    var client = new MinioClient()
        .WithEndpoint(minioConfig.Endpoint)
        .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey);
    
    return client.Build();
});

bld.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AlexandriaDbContext>()
    .AddDefaultTokenProviders();

bld.Services.AddFastEndpoints().SwaggerDocument();
bld.Services.AddHealthChecks();
bld.Services.AddCors(c =>
{
    c.AddPolicy("AllowOrigin",
        options => options.WithOrigins(
                "https://unitrack.io:8080",
                "http://localhost:3000",
                "http://localhost:5173")
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader()
    );
});

var app = bld.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints().UseSwaggerGen();
app.MapHealthChecks("/health");

// MinIO setup
using (var scope = app.Services.CreateScope())
{
    var minioClient = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    try
    {
        // Ensure bucket exists
        bool bucketExists = await minioClient.BucketExistsAsync(new BucketExistsArgs()
            .WithBucket(minioConfig.UploadBucket));
        
        if (!bucketExists)
        {
            await minioClient.MakeBucketAsync(new MakeBucketArgs()
                .WithBucket(minioConfig.UploadBucket));
        }
        
        // Enable versioning
        await minioClient.SetVersioningAsync(new SetVersioningArgs()
            .WithBucket(minioConfig.UploadBucket)
            .WithVersioningEnabled());
            
        Console.WriteLine($"MinIO bucket '{minioConfig.UploadBucket}' is ready with versioning enabled");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error setting up MinIO bucket: {ex.Message}");
    }
}

app.UseCors("AllowOrigin");

app.Run();