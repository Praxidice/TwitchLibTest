using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace PubSubMocker
{
    internal class ClientHandler
    {
        HttpListenerContext _http { get; set; }
        Task? _readTask { get; set; }
        ILogger? _log { get; set; }

        HttpListenerWebSocketContext? context { get; set; }

        object pongResponse = new { type = "PONG" };
        object listenResponse = new { type = "RESPONSE", error = "", nonce = "mock0000" };

        public ClientHandler(HttpListenerContext socket, ILogger? log = null)
        {
            _http = socket;
            _log = log;
        }

        private async Task Read(CancellationToken cancellation)
        {
            try
            {
                context = await _http.AcceptWebSocketAsync(null);
                _log.LogInformation($"Client connected: {_http.Request.RemoteEndPoint}");

                var socket = context.WebSocket;
                var buffer = new byte[2048];

                while (true)
                {
                    var rec = await socket.ReceiveAsync(buffer, cancellation);

                    if (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    if(socket.State == WebSocketState.CloseReceived)
                    {
                        _log.LogInformation($"Client disconnected: {_http.Request.RemoteEndPoint}");
                        break;
                    }
                    else if(socket.State != WebSocketState.Open)
                    {
                        _log.LogError($"Socket in unexpected state '{socket.State}' for client: {_http.Request.RemoteEndPoint}");
                        break;
                    }

                    if (rec.EndOfMessage && rec.Count > 0)
                    {
                        string messageString = Encoding.ASCII.GetString(buffer, 0, rec.Count);
                        _log?.LogTrace($"Received message:\r\n{messageString}");

                        JObject message = JObject.Parse(messageString);

                        switch ((string?)message["type"])
                        {
                            case "PING":
                                await PushMessage(Newtonsoft.Json.JsonConvert.SerializeObject(pongResponse), cancellation);
                                break;
                            case "LISTEN":
                                await PushMessage(Newtonsoft.Json.JsonConvert.SerializeObject(listenResponse), cancellation);
                                break;
                        }
                    }
                }
            }

            catch (OperationCanceledException)
            {
                return;
            }

            catch (Exception e)
            {
                _log?.LogError(e, "Error reading from client");
                return;
            }
        }


        public void StartReading(CancellationToken cancellation)
        {
            _readTask = Task.Run(async () => await Read(cancellation));
        }

        public async Task PushMessage(string messageString, CancellationToken cancellation)
        {
            if(context != null && context.WebSocket.State == WebSocketState.Open)
            {
                _log?.LogTrace($"Sending message: {messageString}");
                await context.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageString)), WebSocketMessageType.Text, true, cancellation);
            }
            else
            {
                _log?.LogTrace($"Can't send message: {messageString}");
            }
        }

        public void Close()
        {
            _http.Response.Close();
        }
    }
}
