using FastEndpoints;

var bld = WebApplication.CreateBuilder();
bld.Services.AddFastEndpoints();
bld.Services.AddHealthChecks();

var app = bld.Build();
app.UseFastEndpoints();
app.MapHealthChecks("/health");
app.Run();