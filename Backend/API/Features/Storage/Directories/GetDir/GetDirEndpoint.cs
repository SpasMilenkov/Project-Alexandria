using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.GetDir;
 
 public class GetDirEndpoint(IDirectoryService dirService): Endpoint<GetDirRequest, GetDirResult>
 {
     public override void Configure()
     {
         Get("/directories");
         Description(x => x.WithTags("Directories"));

     }
 
     public override async Task HandleAsync(GetDirRequest req, CancellationToken ct)
     {
         var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                            ?? User.FindFirst("sub")?.Value
                            ?? throw new UnauthorizedAccessException("User ID not found in token");
    
         var userId = Guid.Parse(userIdString);
         
         var dir = await dirService.GetDirectoryDtoByIdAsync(req.DirectoryId, userId, ct);
         await Send.OkAsync(new GetDirResult
         {
             Directory = dir
         }, cancellation:ct);
     }
 }