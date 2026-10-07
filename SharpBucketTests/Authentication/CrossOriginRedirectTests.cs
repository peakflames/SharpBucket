using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpBucket;
using SharpBucket.Authentication;
using SharpBucket.V2;
using Shouldly;

namespace SharpBucketTests.Authentication
{
    /// <summary>
    /// Tests of the redirect handling of <see cref="RequestExecutor"/>.
    /// Loopback sockets play the role of the Bitbucket API and of the host it redirects to, so that the
    /// requests really put on the wire can be asserted without any credential nor any access to Bitbucket.
    /// </summary>
    [TestFixture]
    public class CrossOriginRedirectTests
    {
        private const string PresignedQuery = "X-Amz-Credential=AKIA%2F20261007%2Fus-east-1&X-Amz-Signature=abc";

        [Test]
        public void CrossOriginRedirect_Bearer_ShouldNotSendTheAuthorizationHeaderToTheOtherOrigin()
        {
            AssertCrossOriginRedirectDoesNotForwardAuthorization(
                sharpBucket => sharpBucket.OAuth2BearerToken("a-fake-access-token"));
        }

        [Test]
        public void CrossOriginRedirect_Basic_ShouldNotSendTheAuthorizationHeaderToTheOtherOrigin()
        {
            AssertCrossOriginRedirectDoesNotForwardAuthorization(
                sharpBucket => sharpBucket.BasicAuthentication("a-user", "a-password"));
        }

        // OAuth2ClientCredentials is not covered here: OAuth2TokenProviderTests only talks to the real
        // Bitbucket token endpoint, so there is no reusable fake token endpoint to build on. The redirect logic
        // is independent of the authenticator, which is covered through Bearer and Basic.

        [Test]
        public async Task CrossOriginRedirect_Async_ShouldNotSendTheAuthorizationHeaderToTheOtherOrigin()
        {
            using (var otherOrigin = new LoopbackServer((index, request) => LoopbackServer.Ok()))
            using (var bitbucket = new LoopbackServer(
                (index, request) => LoopbackServer.Redirect($"http://127.0.0.1:{otherOrigin.Port}/obj?{PresignedQuery}")))
            {
                var sharpBucket = new SharpBucketV2($"http://127.0.0.1:{bitbucket.Port}/2.0");
                sharpBucket.OAuth2BearerToken("a-fake-access-token");

                await sharpBucket.GetAsync("user", CancellationToken.None);

                otherOrigin.Requests.Count.ShouldBe(1);
                otherOrigin.Requests[0].Headers
                    .Where(header => header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    .ShouldBeEmpty("credentials must not be sent to a different origin");
                otherOrigin.Requests[0].RequestLine.ShouldBe($"GET /obj?{PresignedQuery} HTTP/1.1");
            }
        }

        [Test]
        public async Task SameOriginRedirect_ShouldKeepSendingTheAuthorizationHeader()
        {
            using (var server = new LoopbackServer(
                (index, request) => index == 0
                    ? LoopbackServer.Redirect($"http://{HostOf(request)}/2.0/moved")
                    : LoopbackServer.Ok()))
            {
                var sharpBucket = new SharpBucketV2($"http://127.0.0.1:{server.Port}/2.0");
                sharpBucket.OAuth2BearerToken("a-fake-access-token");

                await Task.Run(() => sharpBucket.Get("user"));

                server.Requests.Count.ShouldBe(2);
                server.Requests[1].RequestLine.ShouldStartWith("GET /2.0/moved");
                server.Requests[1].Headers
                    .ShouldContain(header => header.StartsWith("Authorization: Bearer a-fake-access-token", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Test]
        public void ErrorWithANonJsonBody_ShouldNotLeakTheBodyInTheExceptionMessage()
        {
            const string xmlBody = "<Error><Code>InvalidArgument</Code><Message>Unsupported Authorization Type</Message>"
                                   + "<ArgumentValue>Bearer abc123</ArgumentValue></Error>";
            using (var server = new LoopbackServer((index, request) => LoopbackServer.Response(400, "Bad Request", "application/xml", xmlBody)))
            {
                var sharpBucket = new SharpBucketV2($"http://127.0.0.1:{server.Port}/2.0");
                sharpBucket.OAuth2BearerToken("abc123");

                var exception = Should.Throw<BitbucketException>(() => sharpBucket.Get("user"));

                exception.Message.ShouldBe("400 Bad Request");
                exception.Message.ShouldNotContain("Bearer");
                exception.Message.ShouldNotContain("abc123");
            }
        }

        [Test]
        public void RedirectFromAnHttpsOriginToHttp_ShouldBeRefused()
        {
            // No TLS server is available offline, so the downgrade is covered by the ClassifyRedirect cases below.
            RequestExecutor.ClassifyRedirect(new Uri("https://api.example.com/2.0/"), new Uri("http://api.example.com/2.0/x"))
                .ShouldBe(RequestExecutor.RedirectKind.Downgrade);
        }

        [TestCase("http://a.example.com/2.0/", "http://a.example.com/other", "SameOrigin", TestName = "Same origin")]
        [TestCase("http://a.example.com:8080/2.0/", "http://a.example.com:9090/x", "CrossOrigin", TestName = "Different port")]
        [TestCase("http://a.example.com/2.0/", "http://b.example.com/x", "CrossOrigin", TestName = "Different host")]
        [TestCase("http://a.example.com/2.0/", "http://A.EXAMPLE.com/x", "SameOrigin", TestName = "Host case difference")]
        [TestCase("https://a.example.com/2.0/", "http://a.example.com/x", "Downgrade", TestName = "Https to http")]
        [TestCase("http://a.example.com/2.0/", "https://a.example.com/x", "CrossOrigin", TestName = "Http to https")]
        [TestCase("https://a.example.com/2.0/", "https://a.example.com:443/x", "SameOrigin", TestName = "Explicit default https port")]
        [TestCase("http://a.example.com:80/2.0/", "http://a.example.com/x", "SameOrigin", TestName = "Explicit default http port on the base url")]
        public void ClassifyRedirect_ShouldCompareSchemeHostAndPort(string baseUrl, string target, string expected)
        {
            RequestExecutor.ClassifyRedirect(new Uri(baseUrl), new Uri(target)).ToString().ShouldBe(expected);
        }

        private static void AssertCrossOriginRedirectDoesNotForwardAuthorization(Action<SharpBucketV2> authenticate)
        {
            using (var otherOrigin = new LoopbackServer((index, request) => LoopbackServer.Ok()))
            using (var bitbucket = new LoopbackServer(
                (index, request) => LoopbackServer.Redirect($"http://127.0.0.1:{otherOrigin.Port}/obj?{PresignedQuery}")))
            {
                var sharpBucket = new SharpBucketV2($"http://127.0.0.1:{bitbucket.Port}/2.0");
                authenticate(sharpBucket);

                sharpBucket.Get("user");

                bitbucket.Requests.Count.ShouldBe(1);
                bitbucket.Requests[0].Headers.ShouldContain(header => header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase));

                otherOrigin.Requests.Count.ShouldBe(1);
                otherOrigin.Requests[0].Headers
                    .Where(header => header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    .ShouldBeEmpty("credentials must not be sent to a different origin");
                otherOrigin.Requests[0].RequestLine.ShouldBe($"GET /obj?{PresignedQuery} HTTP/1.1", "a presigned query must arrive byte-for-byte");
            }
        }

        private static string HostOf(CapturedRequest request)
        {
            return request.Headers.First(header => header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)).Substring("Host:".Length).Trim();
        }

        private class CapturedRequest
        {
            public string RequestLine { get; set; }
            public List<string> Headers { get; set; }
        }

        /// <summary>
        /// Minimal loopback http server: answers each connection, one after the other, with a scripted response.
        /// </summary>
        private sealed class LoopbackServer : IDisposable
        {
            private readonly TcpListener listener;
            private readonly Func<int, CapturedRequest, string> script;
            private readonly Task acceptLoop;
            private readonly object sync = new object();
            private readonly List<CapturedRequest> requests = new List<CapturedRequest>();

            public LoopbackServer(Func<int, CapturedRequest, string> script)
            {
                this.script = script;
                this.listener = new TcpListener(IPAddress.Loopback, 0);
                this.listener.Start();
                this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
                this.acceptLoop = Task.Run(() => this.AcceptLoop());
            }

            public int Port { get; }

            public IReadOnlyList<CapturedRequest> Requests
            {
                get
                {
                    lock (this.sync) return this.requests.ToList();
                }
            }

            public static string Ok() => Response(200, "OK", "application/json", "{}");

            public static string Redirect(string location) =>
                "HTTP/1.1 302 Found\r\nLocation: " + location + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

            public static string Response(int status, string description, string contentType, string body) =>
                $"HTTP/1.1 {status} {description}\r\nContent-Type: {contentType}\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";

            public void Dispose()
            {
                this.listener.Stop();
                try { this.acceptLoop.Wait(TimeSpan.FromSeconds(5)); }
                catch (AggregateException) { /* the listener was stopped while accepting */ }
            }

            private void AcceptLoop()
            {
                var index = 0;
                while (true)
                {
                    TcpClient client;
                    try { client = this.listener.AcceptTcpClient(); }
                    catch (SocketException) { return; }
                    catch (InvalidOperationException) { return; }

                    using (client)
                    using (var stream = client.GetStream())
                    {
                        var raw = new StringBuilder();
                        var oneByte = new byte[1];
                        while (!raw.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                        {
                            if (stream.Read(oneByte, 0, 1) == 0) break;
                            raw.Append((char)oneByte[0]);
                        }

                        var lines = raw.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        var request = new CapturedRequest { RequestLine = lines.FirstOrDefault(), Headers = lines.Skip(1).ToList() };
                        lock (this.sync) this.requests.Add(request);

                        var response = Encoding.UTF8.GetBytes(this.script(index++, request));
                        stream.Write(response, 0, response.Length);
                        stream.Flush();
                    }
                }
            }
        }
    }
}
