using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using NLog.Extensions.Logging;

namespace worker
{
    public class Tile38Listener
    {        
        public IConfiguration _configuration { get; }
        private readonly NLog.Logger _logger;

        ConnectionMultiplexer redis = null;
        IDatabase db = null;

        Dictionary<string, DateTime> lastUpdated = new Dictionary<string, DateTime>();

        private const string viewPrefix = "view";

        public Tile38Listener(IConfiguration configuration){
            _configuration = configuration;
            _logger = NLog.LogManager.GetCurrentClassLogger();

            _logger.Debug("Hello from Tile38 Listener");
        }

        public List<string> KEYS(string filter){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.Info($"Connected to Tile38 {tile38Connection}");
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

                return keyCollection;
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.Error(ex, message);
                return null;
            }
             catch(Exception ex){
                string message = "Unable to retrieve Keys from Tile38";
                _logger.Error(ex, message);
                return null;
            }
        }

        public bool SubscribeToGeoFences(){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.Info($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                _logger.Info("CHANS *");
                var result = db.Execute("CHANS", "*");
                _logger.Info(result.ToString());
                
                // GeoJSON.Net.Feature.FeatureCollection geofences = new GeoJSON.Net.Feature.FeatureCollection();

                // Top level - collection of features
                System.Diagnostics.Debug.Assert(result.Type == ResultType.MultiBulk);
                RedisResult[] topLevelRedisArrayResult = ((RedisResult[])result);
                foreach(RedisResult redisResult in topLevelRedisArrayResult){

                    // Child level - the geofence
                    System.Diagnostics.Debug.Assert(redisResult.Type == ResultType.MultiBulk);
                    RedisResult[] childRedisArrayResult = ((RedisResult[])redisResult);

                    // First property should be geofence name
                    System.Diagnostics.Debug.Assert(childRedisArrayResult[0].Type == ResultType.BulkString);

                    string channelName = childRedisArrayResult[0].ToString();
                    if(!channelName.StartsWith(viewPrefix)){
                        _logger.Info($"Subscribing to GeoFence {childRedisArrayResult[0].ToString()} ...");

                        ISubscriber sub = redis.GetSubscriber();
                        sub.Subscribe(channelName, (channel, message) => {
                            _logger.Info($"Emitting GeoFence {channel} ... {message.ToString()}");
                            // _hubContext.Clients.All.SendAsync("emitGeoFence", message.ToString());
                        });
                    }
                }

                return true;
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.Error(ex, message);
                return false;
            }
             catch(Exception ex){
                string message = "Unable to retrieve GeoFences from Tile38";
                _logger.Error(ex, message);
                return false;
            }
        }

        public bool SubscribeToEvents(string filter){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.Info($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                if(filter == null){
                    filter = "*";
                }

                foreach(string key in KEYS(filter)){
                    string viewName = viewPrefix + key;

                    _logger.Info($"SETCHAN {viewName} WITHIN {key} FENCE DETECT enter,exit,cross,inside BOUNDS -180 0 180 180");
                    var result = db.Execute("SETCHAN",  viewName, "WITHIN", key, "FENCE", "DETECT", "enter,exit,cross,inside", "BOUNDS", -180, 0, 180, 180);
                    _logger.Debug(result.ToString());

                    ISubscriber sub = redis.GetSubscriber();
                    sub.Subscribe(viewName, (channel, message) => {

                        string strMessage = message.ToString();
                        
                        _logger.Info($"Emitting Event.... {message.ToString()}");
                        // _hubContext.Clients.All.SendAsync("emitGeoJSON", message.ToString());
                    });
                }

                return true;
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.Error(ex, message);
                return false;
            }
             catch(Exception ex){
                string message = "Unable to execute WITHIN against Tile38";
                _logger.Error(ex, message);
                return false;
            }
        }

        public bool SubscribeToGeoFences(string filter){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
                    // server = redis.GetServer(tile38Connection);
                    _logger.Info($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                string viewName = "*";

                ISubscriber sub = redis.GetSubscriber();
                sub.Subscribe(viewName, (channel, message) => {
                    _logger.Info($"Emitting GeoFence.... {message.ToString()}");
                    // _hubContext.Clients.All.SendAsync("emitGeoFence", message.ToString());
                });

                return true;
            } catch (StackExchange.Redis.RedisConnectionException ex){
                string message = "Unable to connect to Tile38";
                _logger.Error(ex, message);
                return false;
            }
             catch(Exception ex){
                string message = "Unable to execute WITHIN against Tile38";
                _logger.Error(ex, message);
                return false;
            }
        }
    }
}
