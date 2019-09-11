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
    [Route("api/[controller]")]
    [ApiController]
    public class Tile38Controller : ControllerBase
    {        
        private readonly IHubContext<tile38_viewer.Hubs.MovementHub> _hubContext;
        public IConfiguration _configuration { get; }
        private readonly ILogger _logger;

        ConnectionMultiplexer redis = null;
        // IServer server = null;
        IDatabase db = null;

        public Tile38Controller(IConfiguration configuration, ILogger<Tile38Controller> logger, IHubContext<tile38_viewer.Hubs.MovementHub> hubContext){
            // _hubContext = hubContext;
            // _hubContext.Clients.All.SendAsync("emitGeoJSON", "Hello from Tile38 Controller");

            _configuration = configuration;
            _logger = logger;

            _logger.LogDebug("Hello from Tile38 Controller");
        }

        [HttpGet("GeoFences")]
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

                    // Last property should be contain 'WITHIN', 'enter,exit', etc, and geofence GeoJSON
                    RedisResult lastChildResult = childRedisArrayResult[childRedisArrayResult.Count()-2];
                    System.Diagnostics.Debug.Assert(lastChildResult.Type == ResultType.MultiBulk);
                    RedisResult[] lastChildResultArray = (RedisResult[])lastChildResult;

                    RedisResult finalChildResult = lastChildResultArray[lastChildResultArray.Count()-1];
                    System.Diagnostics.Debug.Assert(finalChildResult.Type == ResultType.BulkString);

                    if(finalChildResult.ToString().StartsWith("{\"type\":\"Polygon\"")){
                        _logger.LogInformation("Found GeoJSON!");
                        GeoJSON.Net.Feature.Feature feature = new GeoJSON.Net.Feature.Feature(Newtonsoft.Json.JsonConvert.DeserializeObject<GeoJSON.Net.Geometry.Polygon>(finalChildResult.ToString()));
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

        [HttpGet("KEYS/{filter}")]
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

        

        [HttpGet("WITHIN/{key}/{xMin}/{yMin}/{xMax}/{yMax}")]
        // public JsonResult WITHIN(string key, double xMin, double yMin, double xMax, double yMax){
        public JsonResult WITHIN(string key, string xMin, string yMin, string xMax, string yMax){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.LogInformation($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                _logger.LogInformation($"WITHIN {key} BOUNDS {xMin} {yMin} {xMax} {yMax}");

                var result = db.Execute("WITHIN", key, "BOUNDS", xMin, yMin, xMax, yMax);
                _logger.LogInformation(result.ToString());
                
                GeoJSON.Net.Feature.FeatureCollection featureCollection = new GeoJSON.Net.Feature.FeatureCollection();

                // Top level - collection of features
                System.Diagnostics.Debug.Assert(result.Type == ResultType.MultiBulk);
                RedisResult[] withinResult = ((RedisResult[])result);

                System.Diagnostics.Debug.Assert(withinResult[0].Type == ResultType.Integer);
                _logger.LogInformation($"WITHIN returned {withinResult[0]} features");

                System.Diagnostics.Debug.Assert(withinResult[1].Type == ResultType.MultiBulk);

                RedisResult[] withinFeatures = ((RedisResult[])withinResult[1]);

                foreach(RedisResult featureResult in withinFeatures){
                    System.Diagnostics.Debug.Assert(featureResult.Type == ResultType.MultiBulk);
                    RedisResult[] featureDetails = ((RedisResult[])featureResult);
                    System.Diagnostics.Debug.Assert(featureDetails.Length == 2);
                    System.Diagnostics.Debug.Assert(featureDetails[0].Type == ResultType.BulkString); // ID
                    System.Diagnostics.Debug.Assert(featureDetails[1].Type == ResultType.BulkString); // GeoJSON

                    if(featureDetails[1].ToString().StartsWith("{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\"")){
                        _logger.LogInformation("Found GeoJSON!");
                        GeoJSON.Net.Feature.Feature feature = Newtonsoft.Json.JsonConvert.DeserializeObject<GeoJSON.Net.Feature.Feature>(featureDetails[1].ToString());

                        featureCollection.Features.Add(feature);
                    }

                }

                return new JsonResult(featureCollection);
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
             catch(Exception ex){
                string message = "Unable to execute WITHIN against Tile38";
                _logger.LogError(0, ex, message);
                HttpContext.Response.StatusCode = 500;
                return new JsonResult(new {message = message, exception = ex});
            }
        }
    }
}
