using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace WebmailClient.Hubs
{
    public class WebmailHub : Hub
    {
        // Clients can connect to this hub to receive real-time notifications
        // Examples: New Email, Folder Sync Progress, etc.

        public async Task JoinAccountGroup(string emailAddress)
        {
            // Secure this in a real app to ensure the user owns the emailAddress
            await Groups.AddToGroupAsync(Context.ConnectionId, emailAddress);
        }

        public async Task LeaveAccountGroup(string emailAddress)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, emailAddress);
        }
    }
}
