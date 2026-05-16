using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Vyron.API.Hubs;

[Authorize]
public class OrderTrackingHub : Hub
{
    public async Task JoinOrderGroup(string orderNumber)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderNumber}");

    public async Task LeaveOrderGroup(string orderNumber)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderNumber}");

    public async Task JoinAdminGroup()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
}
