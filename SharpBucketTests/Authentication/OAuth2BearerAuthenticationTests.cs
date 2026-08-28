using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpBucket;
using SharpBucket.V2;
using Shouldly;

namespace SharpBucketTests.Authentication
{
    /// <summary>
    /// Tests of <see cref="SharpBucket.Authentication.OAuth2BearerAuthentication"/>.
    /// A loopback socket plays the role of the Bitbucket API so that the headers really put on the
    /// wire can be asserted without requiring any credential nor any access to Bitbucket.
    /// </summary>
    [TestFixture]
    public class OAuth2BearerAuthenticationTests
    {
        [Test]
        public void OAuth2BearerToken_ShouldSendTheAccessTokenInAnAuthorizationBearerHeader()
        {
            var authorizationHeaders = GetAuthorizationHeadersOfOneRequest(
                sharpBucket => sharpBucket.OAuth2BearerToken("a-fake-access-token"));

            authorizationHeaders.Count.ShouldBe(1);
            authorizationHeaders[0].ShouldBe("Authorization: Bearer a-fake-access-token", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Negative control: proves that the assertion above would notice a missing header.
        /// </summary>
        [Test]
        public void NoAuthentication_ShouldNotSendAnyAuthorizationHeader()
        {
            var authorizationHeaders = GetAuthorizationHeadersOfOneRequest(
                sharpBucket => sharpBucket.NoAuthentication());

            authorizationHeaders.ShouldBeEmpty();
        }

        private static System.Collections.Generic.List<string> GetAuthorizationHeadersOfOneRequest(Action<SharpBucketV2> authenticate)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var receivedRequest = Task.Run(() => AcceptOneRequest(listener));

                var sharpBucket = new SharpBucketV2($"http://127.0.0.1:{port}/2.0");
                authenticate(sharpBucket);
                sharpBucket.Get("user");

                receivedRequest.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue("the fake server did not receive any request");

                return receivedRequest.Result
                    .Where(header => header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// Accepts a single connection, returns the received request lines, and answers an empty json document.
        /// </summary>
        private static string[] AcceptOneRequest(TcpListener listener)
        {
            using (var client = listener.AcceptTcpClient())
            using (var stream = client.GetStream())
            {
                var request = new StringBuilder();
                var oneByte = new byte[1];
                while (!EndOfHeaders(request))
                {
                    if (stream.Read(oneByte, 0, 1) == 0) break;
                    request.Append((char)oneByte[0]);
                }

                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
                stream.Write(response, 0, response.Length);
                stream.Flush();

                return request.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static bool EndOfHeaders(StringBuilder request)
        {
            return request.Length >= 4
                   && request[request.Length - 4] == '\r'
                   && request[request.Length - 3] == '\n'
                   && request[request.Length - 2] == '\r'
                   && request[request.Length - 1] == '\n';
        }
    }
}
