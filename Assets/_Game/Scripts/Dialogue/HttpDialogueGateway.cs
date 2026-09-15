using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FinalDefense.Contracts;

namespace FinalDefense.Dialogue
{
    // Server gateway protocol, NOT a provider API or a bundled credential.
    // SSE data: {"delta":"..."} followed by data: {"result":{DialogueTurnResult}}.
    // Backend owns provider/model, credentials, budget and semantic structured output validation.
    public sealed class HttpDialogueGateway : IDialogueService, IDisposable
    {
        [Serializable] private sealed class Packet { public string delta; public DialogueTurnResult result; }
        private readonly HttpClient client; private readonly Uri endpoint; private readonly IDataCodec codec; private readonly Func<string> sessionToken;
        public HttpDialogueGateway(Uri endpoint, IDataCodec codec, Func<string> sessionToken = null)
            : this(endpoint, codec, new HttpClientHandler { AllowAutoRedirect = false }, sessionToken) { }
        internal HttpDialogueGateway(Uri endpoint, IDataCodec codec, HttpMessageHandler transport, Func<string> sessionToken = null)
        {
            if (endpoint == null || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo)) throw new ArgumentException("Gateway requires HTTPS without embedded credentials");
            this.endpoint = endpoint; this.codec = codec; this.sessionToken = sessionToken;
            client = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
        }
        public async Task<DialogueTurnResult> SendAsync(DialogueTurnRequest request, Action<string> onChunk, CancellationToken cancellation)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                string token = sessionToken?.Invoke(); if (!string.IsNullOrEmpty(token)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                message.Content = new StringContent(codec.Encode(request), Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentType?.MediaType != "text/event-stream") throw new InvalidDataException("Expected event stream");
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (cancellation.Register(() => stream.Dispose()))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        int total = 0; var eventText = new StringBuilder(); var streamed = new StringBuilder();
                        while (true)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            var line = await ReadBoundedLine(reader, cancellation); if (line == null) break;
                            total += line.Length; if (total > 32768) throw new InvalidDataException("Response budget exceeded");
                            if (line.StartsWith("data:", StringComparison.Ordinal)) eventText.Append(line.Substring(5).TrimStart()).Append('\n');
                            else if (line.Length == 0 && eventText.Length > 0)
                            {
                                var packet = codec.Decode<Packet>(eventText.ToString()); eventText.Clear();
                                if (packet == null) throw new InvalidDataException("Invalid stream packet");
                                if (packet.delta != null)
                                {
                                    streamed.Append(packet.delta);
                                    if (new System.Globalization.StringInfo(streamed.ToString()).LengthInTextElements > 120) throw new InvalidDataException("Response too long");
                                    onChunk?.Invoke(packet.delta);
                                }
                                if (packet.result != null)
                                {
                                    if (streamed.Length > 0 && streamed.ToString() != packet.result.text) throw new InvalidDataException("Stream/final mismatch");
                                    return packet.result;
                                }
                            }
                        }
                    }
                }
            }
            throw new InvalidDataException("Stream ended without final result");
        }
        private static async Task<string> ReadBoundedLine(StreamReader reader, CancellationToken token)
        {
            var line = new StringBuilder(); var c = new char[1];
            while (await reader.ReadAsync(c, 0, 1) > 0)
            {
                token.ThrowIfCancellationRequested(); if (c[0] == '\n') return line.ToString().TrimEnd('\r');
                line.Append(c[0]); if (line.Length > 8192) throw new InvalidDataException("Stream line too long");
            }
            return line.Length == 0 ? null : line.ToString();
        }
        public void Dispose() => client.Dispose();
    }
}
