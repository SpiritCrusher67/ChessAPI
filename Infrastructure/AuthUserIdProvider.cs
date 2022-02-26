using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Infrastructure
{
    public class AuthUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) => connection.User?.Identity?.Name;
    }
}
