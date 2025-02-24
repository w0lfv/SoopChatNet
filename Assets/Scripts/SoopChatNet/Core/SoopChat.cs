using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SoopChatNet.Data;

namespace SoopChatNet
{
    public class SoopChat : IDisposable
    {
        public bool IsConnected => _ws.State == WebSocketState.Open;

        // Event
        public event Action<string> OnSocketOpened;
        public event Action<string> OnSocketError;
        public event Action<string> OnSocketClosed;

        public event Action<Chat> OnMessageReceived;
        // Concurrent Queue
        private ConcurrentQueue<Chat> _chatQueue = new ConcurrentQueue<Chat>();
        private int _maxQueueSize;

        // CancellationTokenSource
        private CancellationTokenSource _cts;

        // WebSocket
        private readonly ClientWebSocket _ws;

        // Cached Encoder
        private static readonly Encoding _encoding = Encoding.UTF8;

        // Stream Info
        private string _streamerId;
        private ChannelData _channelData;
        private string _password;

        public SoopChat(string streamerId, string password = null, int maxQueueSize = 500)
        {
            _streamerId = streamerId;
            _password = password;
            _maxQueueSize = maxQueueSize;
            _ws = new ClientWebSocket();
            _ws.Options.AddSubProtocol("chat");
        }

        public async void Dispose()
        {
            await CloseAsync();
            _ws.Dispose();
        }

        //////////////////////////////////////////////////////////
        //  Fetch
        //

        #region Fetch

        private async Task<ChannelData> FetchLiveStream()
        {
            // Get Live Stream info using http client
            return await FetchData<ChannelData>(
                $"https://live.sooplive.co.kr/afreeca/player_live_api.php?bjid={_streamerId}",
                new Dictionary<string, string>
                {
                    { "bid", _streamerId },
                    { "type", "live" },
                    { "player_type", "html5" }
                }
            );
        }

        private async Task<T> FetchData<T>(string url, Dictionary<string, string> postData = null)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = null;
                if (postData != null)
                    response = await client.PostAsync(url, new FormUrlEncodedContent(postData));
                else
                    response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
                return default;
            }
        }

        #endregion


        //////////////////////////////////////////////////////////
        //  Websocket Handling
        //

        #region Websocket Handling

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            // Close Existing Connection
            await CloseAsync(cancellationToken);

            try
            {
                // Fetch Live Stream Info
                ChannelData channelData = await FetchLiveStream();

                // Check if the channel is live
                if (channelData.channel.RESULT != 1)
                {
                    OnSocketClosed?.Invoke($"Websocket Closed: {_streamerId} is not live");
                    return;
                }

                _channelData = channelData;

                int port = int.Parse(channelData.channel.CHPT);

                //string uri = $"ws://{channelData.channel.CHDOMAIN}:{port}/Websocket/{_streamerId}"; // TODO : Fallback
                string uri = $"wss://{channelData.channel.CHDOMAIN}:{port + 1}/Websocket/{_streamerId}"; // Sooplive's WSS port is CHPT + 1

                // Connect to WebSocket
                await _ws.ConnectAsync(new Uri(uri), cancellationToken);

                // Send Login Packet
                await SendAsync(ServiceCode.SVC_LOGIN, new string[]
                {
                    _channelData.channel.FTK ?? "",
                    "",
                    ((int)UserStatusFlag1.FOLLOWER).ToString(),
                }, cancellationToken);

                _cts = new CancellationTokenSource();

                _ = Task.Run(ReceiveAsync, _cts.Token);
                _ = Task.Run(KeepAliveAsync, _cts.Token);

                OnSocketOpened?.Invoke($"Connected to {uri}");
            }
            catch (Exception ex)
            {
                OnSocketError?.Invoke($"Error ocurred while opening socket : {ex.Message}");
            }
        }

        public void Update()
        {
            while(_chatQueue.TryDequeue(out Chat chat))
                OnMessageReceived?.Invoke(chat);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", cancellationToken);
                    OnSocketClosed?.Invoke("Socket Closed");
                }
                catch (Exception ex)
                {
                    OnSocketError?.Invoke($"Error ocurred while closing socket : {ex.Message}");
                }
            }

            if (_cts != null)
            { 
                try
                {
                    // Cancel Pending Tasks
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch (Exception ex)
                {
                    OnSocketError?.Invoke($"Error ocurred while closing socket : {ex.Message}");
                }
                finally
                {
                    _cts = null;
                }
            }
        }

        private async void ProcessMessageAsync(ArraySegment<byte> buffer)
        {
            ArraySegment<byte> header = buffer[..14];
            ArraySegment<byte> body = buffer[14..];

            ServiceCode svc = (ServiceCode)int.Parse(_encoding.GetString(header[2..6]));
            ReturnCode retCode = (ReturnCode)int.Parse(_encoding.GetString(header[12..14]));

            if (retCode == ReturnCode.SUCCESS)
            {
                switch (svc)
                {
                    case ServiceCode.SVC_LOGIN:
                        await SendAsync(ServiceCode.SVC_JOINCH, new string[] {
                            _channelData.channel.CHATNO,
                            _channelData.channel.FTK,
                            "0",
                            "",
                            string.IsNullOrEmpty(_password) ? "" : $"pwd\x11{_password}"
                        });
                        break;

                    case ServiceCode.SVC_CHATMESG:
                        while(_chatQueue.Count > _maxQueueSize)
                            _chatQueue.TryDequeue(out _);
                        _chatQueue.Enqueue(new Chat(_encoding.GetString(body).Trim().Split('\f')));
                        break;
                }
            }
        }

        private async Task KeepAliveAsync()
        {
            while (_ws.State == WebSocketState.Open)
            {
                await Task.Delay(30000);
                await SendAsync(ServiceCode.SVC_KEEPALIVE, new string[0] { });
            }
        }

        private async Task ReceiveAsync()
        {
            const int bufferSize = 8192;
            ArraySegment<byte> buff = new(new byte[bufferSize]);

            try
            {
                WebSocketReceiveResult result;
                while (_ws.State == WebSocketState.Open)
                {
                    using MemoryStream stream = new MemoryStream();
                    do
                    {
                        result = await _ws.ReceiveAsync(buff, _cts.Token);
                        stream.Write(buff.Array, buff.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Binary)
                        ProcessMessageAsync(stream.GetBuffer());
                }
            }
            catch (Exception ex)
            {
                OnSocketError?.Invoke(ex.Message);
            }
            finally
            {
                await CloseAsync();
            }
        }

        private async Task SendAsync(ServiceCode svc, string[] data, CancellationToken cancellationToken = default)
        {
            StringBuilder body = new StringBuilder();
            body.Append("\f");
            body.AppendJoin("\f", data);
            body.Append("\f");

            StringBuilder head = new StringBuilder();
            head.Append($"\u001b\t{(int)svc:D4}{body.Length:D6}00");
            head.Append(body);

            await _ws.SendAsync(_encoding.GetBytes(head.ToString()), WebSocketMessageType.Text, true, cancellationToken);
        }

        #endregion

    }
}
