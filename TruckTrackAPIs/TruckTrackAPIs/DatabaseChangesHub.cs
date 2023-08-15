using Microsoft.AspNetCore.SignalR;

namespace TruckTrack
{
    public class DatabaseChangesHub:Hub
    {
        public async Task ReceiveMessage()
        {
            await Clients.All.SendAsync("ReceiveMessage"); // Broadcast a notification to all connected clients
        }

        public async Task UpdateApps(int id)
        {
            await Clients.All.SendAsync("UpdateApps",id); // Broadcast a notification to all connected clients
        }
    }
}
