using Newtonsoft.Json;
using SoopChatNet.Data;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoopChatNet
{
    public class SoopChat : IDisposable, IAsyncDisposable
    {
        // Packet header: ESC(1) + TAB(1) + ServiceCode(4) + BodyLen(6) + RetCode(2) = 14 bytes
        private const int HeaderSize = 14;
        private const int SvcCodeOffset = 2;
        private const int SvcCodeLength = 4;
        private const int BodyLenOffset = 6;
        private const int BodyLenLength = 6;
        private const int RetCodeOffset = 12;
        private const int RetCodeLength = 2;
        private const char BodyDelimiter = '\f';
        private const int KeepAliveIntervalMs = 30000;
        private const int ReceiveBufferSize = 8192;
        private const int BallonFlushTimeoutMs = 1000;

        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
        private static readonly Encoding _encoding = Encoding.UTF8;

        // Events
        public event Action<string> OnSocketOpened;
        public event Action<string> OnSocketError;
        public event Action<string> OnSocketClosed;
        public event Action<Chat> OnMessageReceived;
        public event Action<Ballon> OnBalloonReceived;
        public event Action<Kick> OnKickReceived;
        public event Action<Notice> OnNoticeReceived;
        public event Action<Subscription> OnSubscriptionReceived;

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        // Queues
        private readonly ConcurrentQueue<Chat> _chatQueue = new ConcurrentQueue<Chat>();
        private readonly ConcurrentQueue<Ballon> _ballonQueue = new ConcurrentQueue<Ballon>();
        private readonly ConcurrentQueue<Kick> _kickQueue = new ConcurrentQueue<Kick>();
        private readonly ConcurrentQueue<Notice> _noticeQueue = new ConcurrentQueue<Notice>();
        private readonly ConcurrentQueue<Subscription> _subscriptionQueue = new ConcurrentQueue<Subscription>();
        private readonly int _maxQueueSize;

        // WebSocket & lifecycle
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private Task _keepAliveTask;

        // Stream info
        private readonly string _streamerId;
        private readonly string _password;
        private ChannelData _channelData;

        // Ballon merging state
        private Ballon _recentBallon;
        private CancellationTokenSource _ballonFlushCts;
        private readonly object _ballonLock = new object();

        public SoopChat(string streamerId, string password = null, int maxQueueSize = 500)
        {
            _streamerId = streamerId ?? throw new ArgumentNullException(nameof(streamerId));
            _password = password;
            _maxQueueSize = maxQueueSize;
        }

        public void Dispose()
        {
            // fire-and-forget sync path; prefer DisposeAsync for ordered shutdown
            _ = DisposeAsync().AsTask();
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            _ws?.Dispose();
            _ws = null;
        }

        //////////////////////////////////////////////////////////
        //  Fetch
        //

        #region Fetch

        private Task<ChannelData> FetchLiveStream()
        {
            return FetchData<ChannelData>(
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
            using HttpResponseMessage response = postData != null
                ? await _httpClient.PostAsync(url, new FormUrlEncodedContent(postData))
                : await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return default;

            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }

        #endregion


        //////////////////////////////////////////////////////////
        //  Websocket Handling
        //

        #region Websocket Handling

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            await CloseAsync(cancellationToken);

            try
            {
                ChannelData channelData = await FetchLiveStream();

                if (channelData?.channel == null)
                {
                    OnSocketError?.Invoke("Failed to fetch channel data");
                    return;
                }

                if (channelData.channel.RESULT != 1)
                {
                    OnSocketClosed?.Invoke($"Websocket Closed: {_streamerId} is not live");
                    return;
                }

                _channelData = channelData;

                if (!int.TryParse(channelData.channel.CHPT, out int port))
                {
                    OnSocketError?.Invoke($"Invalid CHPT: {channelData.channel.CHPT}");
                    return;
                }

                // Sooplive's WSS port is CHPT + 1
                string uri = $"wss://{channelData.channel.CHDOMAIN}:{port + 1}/Websocket/{_streamerId}";

                // ClientWebSocket is not reusable after Close — create a fresh instance each Open
                _ws?.Dispose();
                ClientWebSocket ws = new ClientWebSocket();
                ws.Options.AddSubProtocol("chat");
                _ws = ws;

                await ws.ConnectAsync(new Uri(uri), cancellationToken);

                _cts = new CancellationTokenSource();
                CancellationToken ct = _cts.Token;

                await SendAsync(ws, ServiceCode.SVC_LOGIN, new string[]
                {
                    _channelData.channel.FTK ?? "",
                    "",
                    ((int)UserStatusFlag1.FOLLOWER).ToString(),
                }, ct);

                _receiveTask = Task.Run(() => ReceiveAsync(ws, ct));
                _keepAliveTask = Task.Run(() => KeepAliveAsync(ws, ct));

                OnSocketOpened?.Invoke($"Connected to {uri}");
            }
            catch (Exception ex)
            {
                OnSocketError?.Invoke($"Error occurred while opening socket : {ex.Message}");
            }
        }

        public void Update()
        {
            while (_ballonQueue.TryDequeue(out Ballon ballon))
            {
                OnBalloonReceived?.Invoke(ballon);
                Ballon.Release(ballon);
            }

            while (_chatQueue.TryDequeue(out Chat chat))
            {
                OnMessageReceived?.Invoke(chat);
                Chat.Release(chat);
            }

            while (_kickQueue.TryDequeue(out Kick kick))
            {
                OnKickReceived?.Invoke(kick);
                Kick.Release(kick);
            }

            while (_noticeQueue.TryDequeue(out Notice notice))
            {
                OnNoticeReceived?.Invoke(notice);
                Notice.Release(notice);
            }

            while (_subscriptionQueue.TryDequeue(out Subscription sub))
            {
                OnSubscriptionReceived?.Invoke(sub);
                Subscription.Release(sub);
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            ClientWebSocket ws = _ws;
            bool didClose = false;

            // Send close frame first so the receive loop observes it and exits naturally.
            // Cancelling _cts before this would abort the socket (State → Aborted) and the
            // close handshake would be skipped silently.
            if (ws != null && ws.State == WebSocketState.Open)
            {
                // We intended to close an open socket — emit Closed regardless of
                // whether CloseOutputAsync succeeds (the socket won't be Open any more either way)
                didClose = true;
                try
                {
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close", cancellationToken);
                }
                catch (Exception ex)
                {
                    OnSocketError?.Invoke($"Error occurred while closing socket : {ex.Message}");
                }
            }

            // Backstop cancel in case the server never replies with a close frame
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }

            // Wait for background tasks to finish cleanly
            Task[] tasks = new[] { _receiveTask, _keepAliveTask }.Where(t => t != null).ToArray();
            _receiveTask = null;
            _keepAliveTask = null;
            if (tasks.Length > 0)
            {
                try { await Task.WhenAll(tasks); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { OnSocketError?.Invoke($"Background task error : {ex.Message}"); }
            }

            if (_cts != null)
            {
                try { _cts.Dispose(); }
                catch (ObjectDisposedException) { }
                _cts = null;
            }

            // Emit closed only when we actually initiated the close (skip no-op calls
            // against an already-closed socket or a socket that never connected)
            if (didClose)
            {
                OnSocketClosed?.Invoke("Socket Closed");
            }

            // Clean up pending ballon state
            lock (_ballonLock)
            {
                try { _ballonFlushCts?.Cancel(); }
                catch (ObjectDisposedException) { }
                _ballonFlushCts?.Dispose();
                _ballonFlushCts = null;

                if (_recentBallon != null)
                {
                    Ballon.Release(_recentBallon);
                    _recentBallon = null;
                }
            }

            // Drain queues so pooled objects are returned (Update() won't run after close)
            while (_ballonQueue.TryDequeue(out Ballon pendingBallon))
                Ballon.Release(pendingBallon);
            while (_chatQueue.TryDequeue(out Chat pendingChat))
                Chat.Release(pendingChat);
            while (_kickQueue.TryDequeue(out Kick pendingKick))
                Kick.Release(pendingKick);
            while (_noticeQueue.TryDequeue(out Notice pendingNotice))
                Notice.Release(pendingNotice);
            while (_subscriptionQueue.TryDequeue(out Subscription pendingSub))
                Subscription.Release(pendingSub);
        }

        private async Task FlushBallonAfterDelayAsync(Ballon target, CancellationToken ct)
        {
            try
            {
                await Task.Delay(BallonFlushTimeoutMs, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }

            lock (_ballonLock)
            {
                // Emit message-less ballon only if it's still the pending one
                if (_recentBallon == target)
                {
                    _ballonQueue.Enqueue(_recentBallon);
                    _recentBallon = null;
                    _ballonFlushCts?.Dispose();
                    _ballonFlushCts = null;
                }
            }
        }

        private async Task KeepAliveAsync(ClientWebSocket ws, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    await Task.Delay(KeepAliveIntervalMs, ct);
                    await SendAsync(ws, ServiceCode.SVC_KEEPALIVE, Array.Empty<string>(), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnSocketError?.Invoke($"KeepAlive error: {ex.Message}");
            }
        }

        private async Task ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] buffer = _bytePool.Rent(ReceiveBufferSize);
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, ReceiveBufferSize);
            using MemoryStream stream = new MemoryStream(ReceiveBufferSize);

            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    stream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(segment, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        stream.Write(segment.Array, segment.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    // Sooplive may deliver multiple packets concatenated in a single frame
                    ProcessMessage(ws, stream.GetBuffer().AsSpan(0, (int)stream.Length));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnSocketError?.Invoke(ex.Message);
            }
            finally
            {
                _bytePool.Return(buffer);
            }
        }

        private async Task SendAsync(ClientWebSocket ws, ServiceCode svc, string[] data, CancellationToken ct = default)
        {
            // body: leading \f + each field followed by \f
            int bodyLen = 1;
            for (int i = 0; i < data.Length; i++)
                bodyLen += _encoding.GetByteCount(data[i]) + 1;

            byte[] buffer = _bytePool.Rent(HeaderSize + bodyLen);
            try
            {
                int offset = 0;

                // header: ESC TAB svc(4 digits) bodyLen(6 digits) retCode(2 digits '00')
                offset += _encoding.GetBytes($"\t{(int)svc:D4}{bodyLen:D6}00", buffer.AsSpan(offset));

                buffer[offset++] = (byte)BodyDelimiter;
                for (int i = 0; i < data.Length; i++)
                {
                    offset += _encoding.GetBytes(data[i], buffer.AsSpan(offset));
                    buffer[offset++] = (byte)BodyDelimiter;
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(buffer, 0, offset),
                    WebSocketMessageType.Text,
                    true,
                    ct
                );
            }
            finally
            {
                _bytePool.Return(buffer);
            }
        }

        private void ProcessMessage(ClientWebSocket ws, ReadOnlySpan<byte> buffer)
        {
            // A single WebSocket frame can contain multiple concatenated packets
            while (buffer.Length >= HeaderSize)
            {
                ReadOnlySpan<byte> header = buffer[..HeaderSize];

                if (!Utf8Parser.TryParse(header.Slice(SvcCodeOffset, SvcCodeLength), out int svcRaw, out _) ||
                    !Utf8Parser.TryParse(header.Slice(BodyLenOffset, BodyLenLength), out int bodyLen, out _) ||
                    !Utf8Parser.TryParse(header.Slice(RetCodeOffset, RetCodeLength), out int retRaw, out _))
                {
                    OnSocketError?.Invoke("Malformed packet header");
                    return;
                }

                int totalLen = HeaderSize + bodyLen;
                if (buffer.Length < totalLen)
                {
                    OnSocketError?.Invoke($"Truncated packet: expected {totalLen}, got {buffer.Length}");
                    return;
                }

                DispatchPacket(ws, (ServiceCode)svcRaw, (ReturnCode)retRaw, buffer.Slice(HeaderSize, bodyLen));
                buffer = buffer[totalLen..];
            }
        }

        private void DispatchPacket(ClientWebSocket ws, ServiceCode svc, ReturnCode retCode, ReadOnlySpan<byte> body)
        {
            if (retCode != ReturnCode.SUCCESS)
            {
                OnSocketError?.Invoke($"Service {svc} returned {retCode}");
                return;
            }

            switch (svc)
            {
                case ServiceCode.SVC_LOGIN:
                    _ = SendAsync(ws, ServiceCode.SVC_JOINCH, new string[] {
                        _channelData.channel.CHATNO,
                        _channelData.channel.FTK,
                        "0",
                        "",
                        string.IsNullOrEmpty(_password) ? "" : $"pwd\x11{_password}"
                    });
                    break;

                case ServiceCode.SVC_CHATMESG:
                    try { HandleChatMessage(body); }
                    catch (Exception ex) { OnSocketError?.Invoke($"Chat parse failed: {ex.Message}"); }
                    break;

                case ServiceCode.SVC_SENDBALLOON:
                    try { HandleBallon(body); }
                    catch (Exception ex) { OnSocketError?.Invoke($"Ballon parse failed: {ex.Message}"); }
                    break;

                case ServiceCode.SVC_KICK:
                    try { HandleKick(body); }
                    catch (Exception ex) { OnSocketError?.Invoke($"Kick parse failed: {ex.Message}"); }
                    break;

                case ServiceCode.SVC_NOTICE:
                    try { HandleNotice(body); }
                    catch (Exception ex) { OnSocketError?.Invoke($"Notice parse failed: {ex.Message}"); }
                    break;

                case ServiceCode.SVC_SENDSUBSCRIPTION:
                    try { HandleSubscription(body); }
                    catch (Exception ex) { OnSocketError?.Invoke($"Subscription parse failed: {ex.Message}"); }
                    break;

                case ServiceCode.SVC_CHUSER:
                case ServiceCode.SVC_CHUSER_EXTEND:
                    break;

                default:
                    break;
            }
        }

        private void HandleChatMessage(ReadOnlySpan<byte> body)
        {
            while (_chatQueue.Count > _maxQueueSize && _chatQueue.TryDequeue(out Chat dropped))
                Chat.Release(dropped);

            Chat chat = Chat.Rent(_encoding.GetString(body).AsSpan());

            Ballon matched = null;
            Ballon unmatchedFlush = null;
            lock (_ballonLock)
            {
                if (_recentBallon != null)
                {
                    if (_recentBallon.sender == chat.sender)
                        matched = _recentBallon;
                    else
                        // 다른 sender의 채팅이 먼저 도착 → ballon은 메시지 없음으로 확정
                        unmatchedFlush = _recentBallon;

                    _recentBallon = null;
                    _ballonFlushCts?.Cancel();
                    _ballonFlushCts?.Dispose();
                    _ballonFlushCts = null;
                }
            }

            if (unmatchedFlush != null)
                _ballonQueue.Enqueue(unmatchedFlush);

            if (matched != null)
            {
                matched.message = chat.message;
                _ballonQueue.Enqueue(matched);
                Chat.Release(chat);
            }
            else
                _chatQueue.Enqueue(chat);
        }

        private void HandleKick(ReadOnlySpan<byte> body)
        {
            while (_kickQueue.Count > _maxQueueSize && _kickQueue.TryDequeue(out Kick dropped))
                Kick.Release(dropped);
            _kickQueue.Enqueue(Kick.Rent(_encoding.GetString(body).AsSpan()));
        }

        private void HandleNotice(ReadOnlySpan<byte> body)
        {
            while (_noticeQueue.Count > _maxQueueSize && _noticeQueue.TryDequeue(out Notice dropped))
                Notice.Release(dropped);
            _noticeQueue.Enqueue(Notice.Rent(_encoding.GetString(body).AsSpan()));
        }

        private void HandleSubscription(ReadOnlySpan<byte> body)
        {
            while (_subscriptionQueue.Count > _maxQueueSize && _subscriptionQueue.TryDequeue(out Subscription dropped))
                Subscription.Release(dropped);
            _subscriptionQueue.Enqueue(Subscription.Rent(_encoding.GetString(body).AsSpan()));
        }

        private void HandleBallon(ReadOnlySpan<byte> body)
        {
            Ballon newBallon = Ballon.Rent(_encoding.GetString(body).AsSpan());
            CancellationToken flushToken;
            lock (_ballonLock)
            {
                // 이전 별풍선이 pending이면 메시지 없이 flush (새 것이 덮어쓰기 전에)
                if (_recentBallon != null)
                {
                    _ballonFlushCts?.Cancel();
                    _ballonFlushCts?.Dispose();
                    _ballonQueue.Enqueue(_recentBallon);
                }
                _recentBallon = newBallon;
                _ballonFlushCts = new CancellationTokenSource();
                flushToken = _ballonFlushCts.Token;
            }
            _ = FlushBallonAfterDelayAsync(newBallon, flushToken);
        }

        #endregion

    }
}
