using OnlineStoreAPI.Models;

namespace OnlineStoreAPI.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateJwtToken(User user);
    }
}