using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using tile38_viewer.Models;

namespace tile38_viewer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHubContext<tile38_viewer.Hubs.MovementHub> _hubContext;
        public IConfiguration _configuration { get; }
        private readonly ILogger _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, IHubContext<tile38_viewer.Hubs.MovementHub> hubContext){
            _hubContext = hubContext;
            _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from Home Controller");

            _configuration = configuration;
            _logger = logger;

            _logger.LogDebug("Hello from HomeController");
        }

        public IActionResult Index()
        {
            // _logger.LogInformation(geofencesJSON);
            return View();
        }

        public JsonResult Settings()
        {
            int refreshInterval = int.Parse(_configuration["RefreshInterval"]);
            bool useWebSocketsForMovementUpdates = bool.Parse(_configuration["useWebSocketsForMovementUpdates"]);
            return new JsonResult (new {
                refreshInterval = refreshInterval,
                useWebSocketsForMovementUpdates = useWebSocketsForMovementUpdates
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
