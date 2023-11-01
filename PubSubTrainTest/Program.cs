using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using System;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace PubSubTrainTest
{
    class Program
    {
        private TwitchPubSub ? client;
        private ILogger<TwitchPubSub> ? logger;

        static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            using ILoggerFactory consoleLoggerFactory =
                LoggerFactory.Create(builder =>
                    builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFile("log.txt", true)
                    .AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = false;
                        options.TimestampFormat = "HH:mm:ss ";
                    }));

            logger = consoleLoggerFactory.CreateLogger<TwitchPubSub>();
            var logScope = logger.BeginScope("TrainTest");

            try
            {
                logger.LogDebug("On start");

                client = new TwitchPubSub(logger, false);

                client.OnPubSubServiceError += onPubSubServiceError;
                client.OnPubSubServiceConnected += onPubSubServiceConnected;
                client.OnListenResponse += onListenResponse;
                
                client.OnStreamUp += onStreamUp;
                client.OnStreamDown += onStreamDown;

                client.OnFollow += (sender, e) => logger.LogInformation($"Follow: {e.DisplayName}");
                client.OnChannelSubscription += (sender, e) => logger.LogInformation($"Subscription: {e.Subscription.DisplayName}"); //OAuth token required

                client.OnHypeTrain += onHypeTrain;

                string kt = "92469265";
                string chanId = kt;

                client.ListenToVideoPlayback(chanId);
                client.ListenToHypeTrains(chanId);
                //client.ListenToFollows(chanId);

                if (!client.Connect())
                {
                    logger.LogCritical("Failed to connect");
                }
                else
                {
                    logger.LogInformation("Connected");
                }

                Console.ReadKey();
                logger.LogDebug("On exit");
            }

            finally
            {
                logScope?.Dispose();
            }
        }

        private void onHypeTrain(object ? sender, OnHypeTrainArgs e)
        {
            switch(e)
            {
                case OnHypeTrainApproachingArgs approaching:
                    logger?.LogInformation($"Caught: Hype train approaching");
                    break;

                case OnHypeTrainStartArgs begin:
                    logger?.LogInformation($"Caught: Hype train start");
                    break;

                case OnHypeTrainProgressionArgs progress:
                    logger?.LogInformation($"Caught: Hype train progress");
                    break;

                case OnHypeTrainConductorUpdateArgs update:
                    logger?.LogInformation($"Caught: Hype train conductor update");
                    break;

                case OnHypeTrainLevelUpArgs levelUp:
                    logger?.LogInformation($"Caught: Hype train level up");
                    break;

                case OnHypeTrainEndArgs end:
                    logger?.LogInformation($"Caught: Hype train end");
                    break;

                case OnHypeTrainRewardsArgs rewards:
                    logger?.LogInformation($"Caught: Hype train rewards");
                    break;
            }
        }

        private void onPubSubServiceConnected(object ? sender, EventArgs e)
        {
            logger?.LogInformation("On service connected");
            client?.SendTopics(); //Send OAuth here
        }

        private void onPubSubServiceError(object ? sender, OnPubSubServiceErrorArgs e)
        {
            logger?.LogError($"PubSubServiceError: {e.Exception.Message}");
        }

        private void onListenResponse(object ? sender, OnListenResponseArgs e)
        {
            if (!e.Successful) {
                logger?.LogError($"Failed to listen! Response: {e.Response}");
            }
        }

        private void onStreamUp(object ? sender, OnStreamUpArgs e)
        {
            logger?.LogInformation($"Stream just went up! Play delay: {e.PlayDelay}, server time: {e.ServerTime}");
        }

        private void onStreamDown(object ? sender, OnStreamDownArgs e)
        {
            logger?.LogInformation($"Stream just went down! Server time: {e.ServerTime}");
        }
    }
}