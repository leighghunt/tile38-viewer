using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Http;

namespace worker
{
    public class Tile38Listener
    {        
        public IConfiguration _configuration { get; }
        private readonly NLog.Logger _logger;

        ConnectionMultiplexer redis = null;
        IDatabase db = null;

        HubConnection connection;

        Dictionary<string, DateTime> lastUpdated = new Dictionary<string, DateTime>();

        private const string viewPrefix = "view";

        private Timer scanForNewGeoFencesTimer = null;
        private Timer scanForNewKeysTimer = null;

        bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // TODO: You can do custom validation here, or just return true to always accept the certificate.
            // DO NOT use custom validation logic in a production application as it is insecure.
            return true;
        }
        
        public Tile38Listener(IConfiguration configuration){
            _configuration = configuration;
            _logger = NLog.LogManager.GetCurrentClassLogger();

            _logger.Debug("Hello from Tile38 Listener");

            // // Ignore invalid HTTPS cert
            // ServicePointManager.ServerCertificateValidationCallback +=
            //                 (sender, certificate, chain, sslPolicyErrors) => true;
                            
            connection = new HubConnectionBuilder()
                .WithUrl(_configuration.GetConnectionString("HubConnection"), options =>
                    {
                        // https://github.com/aspnet/SignalR/issues/3145
                        options.WebSocketConfiguration = (config) =>
                        {
                            config.RemoteCertificateValidationCallback = ValidateCertificate;
                        };
                        options.HttpMessageHandlerFactory = (handler) =>
                        {
                            if (handler is HttpClientHandler clientHandler)
                            {
                                clientHandler.ServerCertificateCustomValidationCallback = ValidateCertificate;
                            }
                            return handler;
                        };
                    })
                .Build();

            int scanForNewGeoFence_ms = _configuration.GetValue<int>("ScanForNewGeoFences_ms");
            int scanForNewKeys_ms = _configuration.GetValue<int>("ScanForNewKeys_ms");

            scanForNewGeoFencesTimer = new System.Threading.Timer(scanForNewGeoFencesTimerCallback, null,scanForNewGeoFence_ms, scanForNewGeoFence_ms);
            scanForNewKeysTimer = new System.Threading.Timer(scanForNewKeysTimerCallback, null,scanForNewKeys_ms, scanForNewKeys_ms);

            connection.Closed += async (error) =>
            {
                _logger.Error(error);
                await Task.Delay(new Random().Next(0,5) * 1000);
                await connection.StartAsync();
            };      
        }

        public async void ConnectToHub(){
            try{
                await connection.StartAsync();
            } catch(System.Net.Http.HttpRequestException ex){
                _logger.Error("Unable to connect to Hub - please ensure that Viewer container is running.");
                _logger.Error("_configuration.GetConnectionString(\"HubConnection\")");
                _logger.Error(_configuration.GetConnectionString("HubConnection"));
                _logger.Error(ex);
            } catch(Exception ex){
                _logger.Error(ex);
            }
        }

        private void scanForNewGeoFencesTimerCallback(object state)
        {
            SubscribeToGeoFences();
        }

        private void scanForNewKeysTimerCallback(object state)
        {
            SubscribeToEvents(null);
        }

        public List<string> KEYS(string filter){
            try{
                if(redis == null){
                    string tile38Connection = _configuration.GetConnectionString("Tile38Connection");
                    redis = ConnectionMultiplexer.Connect(tile38Connection);
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

                if(redisArrayResult.Count()==0){
                    _logger.Warn($"Tile38 returned empty Keys collection for filter {filter}.");
                }

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
                    _logger.Info($"Connected to Tile38 {tile38Connection}");
                }

                db = redis.GetDatabase();

                _logger.Info("CHANS *");
                var result = db.Execute("CHANS", "*");
                _logger.Info(result.ToString());
                
                // Top level - collection of features
                System.Diagnostics.Debug.Assert(result.Type == ResultType.MultiBulk);
                RedisResult[] topLevelRedisArrayResult = ((RedisResult[])result);

                if(topLevelRedisArrayResult.Count()==0){
                    _logger.Warn("Tile38 returned empty CHANS collection.");
                }
                
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
                            connection.InvokeAsync("emitGeoFence", message.ToString());
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
                        // _logger.Debug($"Emitting Event.... {message.ToString()}");
                        connection.InvokeAsync("emitGeoJSON", message.ToString());
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
                    connection.InvokeAsync("emitGeoFence", message.ToString());
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
