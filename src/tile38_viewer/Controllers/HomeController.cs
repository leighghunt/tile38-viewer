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

        ConnectionMultiplexer redis = null;
        // IServer server = null;
        IDatabase db = null;

        private string geofencesJSON = "";

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


        // public ActionResult<GeoJSON.Net.Feature.FeatureCollection> GeoFences(){
        public JsonResult GeoFences(){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.LogInformation($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                var result = db.Execute("CHANS", "*");
                _logger.LogInformation(result.ToString());
                
                GeoJSON.Net.Feature.FeatureCollection geofences = new GeoJSON.Net.Feature.FeatureCollection();

                // Top level - collection of features
                System.Diagnostics.Debug.Assert(result.Type == ResultType.MultiBulk);
                RedisResult[] topLevelRedisArrayResult = ((RedisResult[])result);
                foreach(RedisResult redisResult in topLevelRedisArrayResult){

                    // Child level - the geofence
                    System.Diagnostics.Debug.Assert(redisResult.Type == ResultType.MultiBulk);
                    RedisResult[] childRedisArrayResult = ((RedisResult[])redisResult);


                    // First property should be geofence name
                    System.Diagnostics.Debug.Assert(childRedisArrayResult[0].Type == ResultType.BulkString);

                    GeoJSON.Net.Feature.Feature feature = null;

                    // Last property should be contain 'WITHIN', 'enter,exit', etc, and geofence GeoJSON
                    RedisResult lastChildResult = childRedisArrayResult[childRedisArrayResult.Count()-2];
                    System.Diagnostics.Debug.Assert(lastChildResult.Type == ResultType.MultiBulk);
                    RedisResult[] lastChildResultArray = (RedisResult[])lastChildResult;

                    RedisResult finalChildResult = lastChildResultArray[lastChildResultArray.Count()-1];
                    System.Diagnostics.Debug.Assert(finalChildResult.Type == ResultType.BulkString);

                    if(finalChildResult.ToString().StartsWith("{\"type\":\"Polygon\"")){
                        _logger.LogInformation("Found GeoJSON!");
                        feature = new GeoJSON.Net.Feature.Feature(Newtonsoft.Json.JsonConvert.DeserializeObject<GeoJSON.Net.Geometry.Polygon>(finalChildResult.ToString()));
                        feature.Properties["Name"] = childRedisArrayResult[0].ToString();

                        geofences.Features.Add(feature);
                    }
                }
                return new JsonResult(geofences);
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
             catch(Exception ex){
                string message = "Unable to retrieve GeoFences from Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
        }

        public JsonResult KEYS(string filter){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.LogInformation($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                if(filter == null){
                    filter = "*";
                }

                var result = db.Execute("KEYS", filter);

                List<string> keyCollection = new List<string>();

                System.Diagnostics.Debug.Assert(result.Type == ResultType.MultiBulk);
                RedisResult[] redisArrayResult = ((RedisResult[])result);
                foreach(RedisResult redisResult in redisArrayResult){
                    System.Diagnostics.Debug.Assert(redisResult.Type == ResultType.BulkString);
                    keyCollection.Add(redisResult.ToString());
                }

                return new JsonResult(keyCollection);
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
             catch(Exception ex){
                string message = "Unable to retrieve Keys from Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
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
