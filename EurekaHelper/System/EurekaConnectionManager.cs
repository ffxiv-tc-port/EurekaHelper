using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EurekaHelper.XIV;

namespace EurekaHelper.System
{
    // Talks to a self-hosted EurekaTrackerServer instance (see /server in this repo) instead of
    // ffxiv-eureka.com's Phoenix/Elixir backend. Protocol is a flat JSON object per WebSocket
    // message (no Phoenix Channels envelope, no manual heartbeat - ClientWebSocket already
    // handles WS ping/pong at the OS level). No password: anyone with the share code can join,
    // and "editing" is just a local toggle each client broadcasts as a courtesy signal.
    public class EurekaConnectionManager : IDisposable
    {
        private const string TrackerBaseUrl = "https://ffxiv-eureka.lother.dev";
        private const string TrackerAPIUrl = TrackerBaseUrl + "/api/instances";
        private const string TrackerWebSocketBaseUrl = "wss://ffxiv-eureka.lother.dev/ws";

        private static HttpClient HttpClient = new();
        private ClientWebSocket ClientWebSocket;
        private CancellationTokenSource CancellationTokenSource;

        private bool Connected = false;
        private bool Invalid = false;
        private bool Public = false;
        private bool IsEditingFlag = false;
        private string TrackerId;
        private int Viewers;
        private int Editors;
        private IEurekaTracker Tracker;

        public EurekaConnectionManager()
        {
            ClientWebSocket = new();
            CancellationTokenSource = new();

            TrackerId = String.Empty;
            Viewers = 0;
        }

        public static async Task<EurekaConnectionManager> JoinTracker(string trackerId)
        {
            var connection = new EurekaConnectionManager { TrackerId = trackerId };

            try
            {
                var url = $"{TrackerWebSocketBaseUrl}/{trackerId}";
                await connection.ClientWebSocket.ConnectAsync(new Uri(url), connection.CancellationTokenSource.Token);
                _ = connection.Receive();
                DalamudApi.Log.Information("Successfully connected to tracker websocket");
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Information($"Failed to connect to tracker websocket: {ex.Message}");
                connection.Invalid = true;
            }

            return connection;
        }

        public async Task Receive()
        {
            ArraySegment<byte> buffer = new(new byte[4096]);
            do
            {
                WebSocketReceiveResult result;
                using MemoryStream memoryStream = new();
                try
                {
                    do
                    {
                        result = await ClientWebSocket.ReceiveAsync(buffer, CancellationTokenSource.Token);
                        memoryStream.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);
                }
                catch (Exception)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                memoryStream.Seek(0, SeekOrigin.Begin);
                using StreamReader streamReader = new(memoryStream, Encoding.UTF8);

                string data = await streamReader.ReadToEndAsync();
                JObject message;
                try
                {
                    message = JObject.Parse(data);
                }
                catch (Exception)
                {
                    continue;
                }

                switch ((string)message["type"])
                {
                    case "initial":
                        {
                            int zoneId = (int)message["zoneId"];
                            Tracker = Utils.GetEurekaTracker((ushort)zoneId);

                            Public = (bool)message["public"];
                            Viewers = (int)message["viewers"];
                            Editors = (int)message["editors"];

                            ApplyKillTimes((JObject)message["killTimes"]);

                            Invalid = false;
                            Connected = true;

                            break;
                        }

                    case "kill_times":
                        ApplyKillTimes((JObject)message["killTimes"]);
                        break;

                    case "visibility":
                        Public = (bool)message["public"];
                        break;

                    case "viewers":
                        Viewers = (int)message["count"];
                        break;

                    case "editors":
                        Editors = (int)message["count"];
                        break;

                    case "error":
                        var errorMessage = (string)message["message"];
                        if (errorMessage == "not_found")
                        {
                            DalamudApi.Log.Information("Invalid instance. Closing connection");
                            Invalid = true;
                            await Close();
                        }
                        else
                        {
                            DalamudApi.Log.Information($"Received error from tracker server: {errorMessage}");
                        }
                        break;
                }
            } while (!CancellationTokenSource.Token.IsCancellationRequested);
        }

        private void ApplyKillTimes(JObject killTimes)
        {
            if (Tracker == null || killTimes == null)
                return;

            Dictionary<ushort, long> keyValuePairs = new();
            foreach (var kv in killTimes)
                keyValuePairs.Add(ushort.Parse(kv.Key), (long)kv.Value);

            Tracker.SetPopTimes(keyValuePairs);
        }

        public async Task Send(JObject payload) =>
            await ClientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload.ToString())), WebSocketMessageType.Text, true, CancellationTokenSource.Token);

        // No password to check - this just toggles the local "I'm editing" courtesy flag and
        // lets everyone else in the room see it (see EditorsUpdate on the server).
        public async Task SetEditing(bool editing)
        {
            IsEditingFlag = editing;
            if (Connected)
                await Send(JObject.Parse($@"{{ ""type"": ""set_editing"", ""editing"": {(editing ? "true" : "false")} }}"));
        }

        public async Task SetTrackerVisiblity(int dataCenterId = -1)
        {
            await Send(JObject.Parse($@"{{ ""type"": ""set_visibility"", ""dataCenterId"": {(dataCenterId == -1 ? "null" : dataCenterId)} }}"));
        }

        public async Task SetPopTime(ushort trackerId, long killTime)
        {
            await Send(JObject.Parse($@"{{ ""type"": ""set_kill_time"", ""monsterId"": {trackerId}, ""time"": {killTime} }}"));
        }

        public async Task Reset(ushort trackerId)
        {
            await Send(JObject.Parse($@"{{ ""type"": ""reset_kill"", ""monsterId"": {trackerId} }}"));
        }

        public async Task ResetAll()
        {
            await Send(JObject.Parse(@"{ ""type"": ""reset_all"" }"));
        }

        public async Task Close()
        {
            Connected = false;

            try
            {
                if (ClientWebSocket.State == WebSocketState.Open)
                    await ClientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
            }
            catch (Exception)
            {
                // already closed/aborted, nothing to do
            }

            CancellationTokenSource.Cancel();
            DalamudApi.Log.Information("Successfully closed the socket connection");

            Public = false;
            TrackerId = String.Empty;
            Tracker = null;
        }

        public static async Task<string> CreateTracker(int zoneId)
        {
            string jsonContent = JObject.Parse($@"{{ ""zoneId"": {zoneId} }}").ToString();

            var httpResponseMessage = await HttpClient.PostAsync(
                TrackerAPIUrl,
                new StringContent(jsonContent, Encoding.UTF8, "application/json"));

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                string response = await httpResponseMessage.Content.ReadAsStringAsync();
                var json = JObject.Parse(response);
                return (string)json["id"];
            }

            return String.Empty;
        }

        public static async Task<string> ExportTracker(string oldTrackerId)
        {
            string jsonContent = JObject.Parse($@"{{ ""zoneId"": 0, ""copyFrom"": {JToken.FromObject(oldTrackerId)} }}").ToString();

            var httpResponseMessage = await HttpClient.PostAsync(
                TrackerAPIUrl,
                new StringContent(jsonContent, Encoding.UTF8, "application/json"));

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var response = await httpResponseMessage.Content.ReadAsStringAsync();
                var json = JObject.Parse(response);
                return (string)json["id"];
            }

            return String.Empty;
        }

        public static async Task<List<string>> GetPublicTrackers(int zoneId, int dataCenterId)
        {
            var httpResponseMessage = await HttpClient.GetAsync($"{TrackerAPIUrl}?zoneId={zoneId}&dataCenterId={dataCenterId}");
            if (!httpResponseMessage.IsSuccessStatusCode)
                return new List<string>();

            var response = await httpResponseMessage.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);
            return json["ids"]?.ToObject<List<string>>() ?? new List<string>();
        }

        public bool IsConnected() => this.Connected;

        public int GetViewers() => this.Viewers;

        public int GetEditors() => this.Editors;

        public string GetTrackerId() => this.TrackerId;

        public bool IsInvalid() => this.Invalid;

        public bool CanModify() => this.IsEditingFlag;

        public bool IsPublic() => this.Public;

        public IEurekaTracker GetTracker() => this.Tracker;

        public async void Dispose()
        {
            if (this.IsConnected())
                await Close();
        }
    }
}
