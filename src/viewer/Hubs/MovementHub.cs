using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace tile38_viewer.Hubs
{
    public class MovementHub : Hub
    {
        public async Task EmitGeoJSON(string geoJSON)
        {
            await Clients.Others.SendAsync("emitGeoJSON", geoJSON);
        }
    }
}
