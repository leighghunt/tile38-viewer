using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace tile38_viewer.Controllers
{
    internal class TimedHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private Timer _timer;

        private readonly IHubContext<tile38_viewer.Hubs.MovementHub> _hubContext;

        public TimedHostedService(ILogger<TimedHostedService> logger,  IHubContext<tile38_viewer.Hubs.MovementHub> hubContext)
        {
            _hubContext = hubContext;
            _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from TimedHostedService");

            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, 
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from Timed Background Service DoWork()");
            _logger.LogInformation("Timed Background Service is working.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}