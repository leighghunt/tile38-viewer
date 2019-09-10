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
        IServer server = null;
        IDatabase db = null;

        private string geofencesJSON = "";

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, IHubContext<tile38_viewer.Hubs.MovementHub> hubContext){
            _hubContext = hubContext;
            _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from Home Controller");

            _configuration = configuration;
            _logger = logger;

            _logger.LogDebug("Hello from HomeController");

            string tile38Connection = configuration.GetConnectionString("Tile38Connection");
            redis = ConnectionMultiplexer.Connect(tile38Connection);
            server = redis.GetServer(tile38Connection);

            _logger.LogInformation($"Connected to Tile38 server {tile38Connection}");
        }

        public IActionResult Index()
        {
            // _logger.LogInformation(geofencesJSON);
            return View();
        }


        public ActionResult<GeoJSON.Net.Feature.FeatureCollection> GeoFences(){
            
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


                // foreach(RedisResult childRedisResult in childRedisArrayResult.ToArray()){
                //     _logger.LogInformation(childRedisResult.ToString());

                //     if(childRedisResult.Type == ResultType.SimpleString){
                //         _logger.LogInformation("Child - ResultType.SimpleString");
                //         _logger.LogInformation(childRedisResult.ToString());
                //     }
                // }


            }
            return geofences;

            // if(result.Type == ResultType.MultiBulk){
            //     RedisResult[] topLevelRedisArrayResult = ((RedisResult[])result);
            //     foreach(RedisResult redisResult in topLevelRedisArrayResult.ToArray()){
            //         _logger.LogInformation(redisResult.ToString());

            //         if(redisResult.Type == ResultType.MultiBulk){
            //             RedisResult[] childRedisArrayResult = ((RedisResult[])redisResult);
            //             foreach(RedisResult childRedisResult in childRedisArrayResult.ToArray()){
            //                 _logger.LogInformation(childRedisResult.ToString());

            //                 if(childRedisResult.Type == ResultType.SimpleString){
            //                     _logger.LogInformation("Child - ResultType.SimpleString");
            //                     _logger.LogInformation(childRedisResult.ToString());
            //                 }
            //                 if(childRedisResult.Type == ResultType.BulkString){
            //                     _logger.LogInformation("Child - ResultType.BulkString");
            //                     _logger.LogInformation(childRedisResult.ToString());
            //                 }

            //                 if(childRedisResult.Type == ResultType.MultiBulk){
            //                     _logger.LogInformation("Child - ResultType.MultiBulk");
            //                     _logger.LogInformation(childRedisResult.ToString());

            //                     RedisResult[] finalChildRedisArrayResult = ((RedisResult[])childRedisResult);
            //                     foreach(RedisResult finalChildRedisResult in finalChildRedisArrayResult.ToArray()){
            //                         _logger.LogInformation(finalChildRedisResult.ToString());

            //                         if(finalChildRedisResult.Type == ResultType.SimpleString){
            //                             _logger.LogInformation("Final Child - ResultType.SimpleString");
            //                             _logger.LogInformation(finalChildRedisResult.ToString());
            //                         }
            //                         if(finalChildRedisResult.Type == ResultType.BulkString){
            //                             _logger.LogInformation("Final Child - ResultType.BulkString");
            //                             _logger.LogInformation(finalChildRedisResult.ToString());

            //                             if(finalChildRedisResult.ToString().StartsWith("{\"type\":\"Polygon\"")){
            //                                 _logger.LogInformation("Found GeoJSON!");
            //                             }
            //                         }
            //                     }


            //                 }
            //             }
            //         }

            //     }
            // }
            
            // string[] arrayResult = ((string[])result);
            // foreach(string str in arrayResult){
            //     _logger.LogInformation(str);
            // }
            // _logger.LogInformation(((string[])result).ToString());

            // // StackExchange.Redis.RedisServerException: ERR unknown command 'PUBSUB'
            // foreach(RedisChannel channel in server.SubscriptionChannels()){
            //     _logger.LogInformation(channel.ToString());
            // }

            // if(!result.IsNull && !result.IsEmpty){

            // }
            // GeoJSON.Net.Feature.FeatureCollection features = Newtonsoft.Json.JsonConvert.DeserializeObject<GeoJSON.Net.Feature.FeatureCollection>(result.ToString());
            // foreach(GeoJSON.Net.Feature.Feature feature in features.Features){
            //     _logger.LogInformation(feature.ToString());

            // }

            // string name = (string)feature.Properties[eventsNameField];

            // foreach(var result in results){
            //     _logger.LogInformation(result.ToString());
            // }
            // geofencesJSON = Newtonsoft.Json.JsonConvert.SerializeObject(geofences);
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
