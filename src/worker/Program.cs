using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace worker
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
            string environmentName = Environment.GetEnvironmentVariable("EnvironmentName");
            if(environmentName == null || environmentName.Length==0){
                environmentName = "";
            }
            environmentName = environmentName.ToLower();

            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                .AddJsonFile($"appsettings.secret.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            //configure NLog          
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties =true });
            Console.WriteLine($"ConfigureNLog - NLogConfigurationFilePath: {Program.Configuration["NLogConfigurationFilePath"]}.");

            NLog.LogManager.LoadConfiguration(Program.Configuration["NLogConfigurationFilePath"]);
            NLog.LogManager.GetCurrentClassLogger().Info("tile38-listener starting up...");  

            Tile38Listener listener = new Tile38Listener(Configuration);

            listener.SubscribeToGeoFences();
            // listener.SubscribeToEvents();
            
            // // Go to sleep so worker can carry on doing it's thing.
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
