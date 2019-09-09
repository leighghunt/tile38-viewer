using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using tile38_viewer.Models;

namespace tile38_viewer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHubContext<tile38_viewer.Hubs.MovementHub> _hubContext;

        public HomeController(ILogger<HomeController> logger, IHubContext<tile38_viewer.Hubs.MovementHub> hubContext){
            _hubContext = hubContext;
            _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from Home Controller");
            logger.LogDebug("Hello from HomeController");
        }

        public IActionResult Index()
        {
            return View();
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
