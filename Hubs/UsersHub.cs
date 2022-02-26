using ChessAPI.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Hubs
{
    [Authorize]
    public class UsersHub : Hub
    {
        OnlineUsersService _onlineUsersService;
        public UsersHub(OnlineUsersService onlineUsersService)
        {
            _onlineUsersService = onlineUsersService;
        }
        public async Task SendMessageToUser(string message, string userLogin)
        {
            await Clients.User(userLogin).SendAsync("ReceiveMessage", $"{Context.User?.Identity?.Name}: {message}");
        }

        public async override Task OnConnectedAsync()
        {
            var userLogin = Context.User?.Identity?.Name;
            if (userLogin != null)
                _onlineUsersService.AddUserToOnlineList(userLogin);

            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userLogin = Context.User?.Identity?.Name;
            _onlineUsersService.RemoveUserFromOnlineList(userLogin);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task GetOnlineFriends()
        {
            var userLogin = Context.User?.Identity?.Name;
            var friendsData = await _onlineUsersService.GetOnlineFriends(userLogin);

            await Clients.Caller.SendAsync("ReceiveOnlineFriends", friendsData);
        }
    }
}
