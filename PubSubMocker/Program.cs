using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Net.WebSockets;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;

namespace PubSubMocker
{
    internal class Program
    {
        static CancellationTokenSource CancellationToken { get; set; } = new CancellationTokenSource();

        static List<ClientHandler> clients = new List<ClientHandler>();

        static async Task SocketHandler(ILogger log)
        {
            HttpListener server = new HttpListener();
            server.Prefixes.Add("http://127.0.0.1:80/");
            server.Start();
            
            log.LogInformation("Listening on 127.0.0.1:80");

            try
            {
                while (true)
                {
                    var httpContext = await server.GetContextAsync().WaitAsync(CancellationToken.Token);

                    if (!httpContext.Request.IsWebSocketRequest)
                    {
                        httpContext.Response.StatusCode = 400;
                        httpContext.Response.Close();
                        continue;
                    }

                    var client = new ClientHandler(httpContext, log);
                    client.StartReading(CancellationToken.Token);
                    clients.Add(client);
                }
            }

            catch (OperationCanceledException)
            {
                return;
            }

            finally
            {
                foreach (var client in clients)
                {
                    client.Close();
                }

                server.Stop();
            }
        }

        static void Main(string[] args)
        {
            string ChannelId = args.FirstOrDefault("92469265");

            StreamReader reader = new StreamReader("mocks.txt");
            string mocksFile = reader.ReadToEnd();
            reader.Close();
            string[] mocks = mocksFile.Split("\r\n");

            for(int i = 0; i < mocks.Length; i++)
            {
                mocks[i] = mocks[i].Replace("{{ChannelId}}", ChannelId);
            }

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

            var logger = consoleLoggerFactory.CreateLogger<Program>();
            var logScope = logger.BeginScope("ServerMock");

            Console.WriteLine("q: quit");

            Task task = Task.Run(async () => await SocketHandler(logger));

            bool Exit = false;

            var upPush = new
            {
                type = "MESSAGE",
                data = new { 
                    topic = $"video-playback-by-id.{ChannelId}",
                    message = @"{""server_time"":1698591995,""play_delay"":0,""type"":""stream-up""}"
                }
            };

            int mockIndex = 0;

            while (!Exit)
            {
                char ConsoleInput = Console.ReadKey().KeyChar;

                switch(ConsoleInput)
                {
                    case 'q':
                        Exit = true;
                        break;

                    case 'u':
                        foreach (var client in clients)
                        {
                            client.PushMessage(JsonConvert.SerializeObject(upPush), CancellationToken.Token).Wait();
                        }
                        break;

                    case 'p':

                        if(mocks.Length == 0)
                        {
                            Console.WriteLine("No mocks loaded");
                            break;
                        }

                        Console.WriteLine($"Pushing mock {mockIndex + 1} of {mocks.Length}");

                        foreach (var client in clients)
                        {
                            client.PushMessage(mocks[mockIndex], CancellationToken.Token).Wait();
                        }

                        mockIndex++;
                        mockIndex = mockIndex % mocks.Length;

                        break;


                }
            }

            CancellationToken.Cancel();
            task.Wait();
        }
    }
}