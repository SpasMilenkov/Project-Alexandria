using Models;

namespace Common.Repositories;

public interface IContentObjectRepository: IRepository<ContentObject>
{
    Task<ContentObject?> HashExists(byte[] hash);
}