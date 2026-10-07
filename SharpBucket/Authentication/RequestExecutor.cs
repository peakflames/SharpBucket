using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using RestSharp;
using RestSharp.Serialization.Json;

namespace SharpBucket.Authentication
{
    internal abstract class RequestExecutor
    {
        private readonly JsonDeserializer jsonDeserializer = new JsonDeserializer();

        /// <summary>
        /// Configure a <see cref="IRestClient"/> instance to be compatible with the style of requests done by the <see cref="RequestExecutor"/>.
        /// </summary>
        /// <param name="client">The client to configure.</param>
        public virtual void ConfigureRestClient(IRestClient client)
        {
            ConfigureHandlers(client);
        }

        private static void ConfigureHandlers(IRestClient client)
        {
            //Fixed bug that prevents RestClient for adding custom headers to the request
            //https://stackoverflow.com/questions/22229393/why-is-restsharp-addheaderaccept-application-json-to-a-list-of-item
            client.ClearHandlers();
            client.AddHandler("application/json", () => new JsonDeserializer());

            client.FollowRedirects = false;
        }

        public string ExecuteRequest(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters)
        {
            var result = Execute(url, method, body, client, requestParameters);
            return result.Content;
        }

        public async Task<string> ExecuteRequestAsync(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters, CancellationToken token)
        {
            var result = await ExecuteAsync(url, method, body, client, requestParameters, token);
            return result.Content;
        }

        public T ExecuteRequest<T>(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters)
            where T : new()
        {
            var result = Execute(url, method, body, client, requestParameters);
            return jsonDeserializer.Deserialize<T>(result);
        }

        public async Task<T> ExecuteRequestAsync<T>(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters, CancellationToken token)
            where T : new()
        {
            var result = await ExecuteAsync(url, method, body, client, requestParameters, token);
            return jsonDeserializer.Deserialize<T>(result);
        }

        public IRestResponse ExecuteRequestNoRedirect(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters)
        {
            return ExecuteNoRedirect(url, method, body, client, requestParameters);
        }

        public Task<IRestResponse> ExecuteRequestNoRedirectAsync(string url, Method method, object body, IRestClient client, IDictionary<string, object> requestParameters, CancellationToken token)
        {
            return ExecuteNoRedirectAsync(url, method, body, client, requestParameters, token);
        }

        private IRestResponse Execute(
            string url,
            Method method,
            object body,
            IRestClient client,
            IDictionary<string, object> requestParameters)
        {
            var request = BuildRestRequest(url, method, body, requestParameters);
            var result = ExecuteRequestWithManualFollowRedirect(request, client);
            ThrowExceptionIfResponseIsInvalid(result);

            return result;
        }

        private async Task<IRestResponse> ExecuteAsync(
            string url,
            Method method,
            object body,
            IRestClient client,
            IDictionary<string, object> requestParameters,
            CancellationToken token)
        {
            var request = BuildRestRequest(url, method, body, requestParameters);
            var result = await ExecuteRequestWithManualFollowRedirectAsync(request, client, token);
            ThrowExceptionIfResponseIsInvalid(result);

            return result;
        }

        private IRestResponse ExecuteNoRedirect(
            string url,
            Method method,
            object body,
            IRestClient client,
            IDictionary<string, object> requestParameters)
        {
            var request = BuildRestRequest(url, method, body, requestParameters);
            var result = client.Execute(request);
            ThrowExceptionIfResponseIsInvalid(result);

            return result;
        }

        private async Task<IRestResponse> ExecuteNoRedirectAsync(
            string url,
            Method method,
            object body,
            IRestClient client,
            IDictionary<string, object> requestParameters,
            CancellationToken token)
        {
            var request = BuildRestRequest(url, method, body, requestParameters);
            var result = await client.ExecuteAsync(request, token);
            ThrowExceptionIfResponseIsInvalid(result);

            return result;
        }

        private void ThrowExceptionIfResponseIsInvalid(IRestResponse response)
        {
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                // There is an issue that prevents the request to complete (timeout, request aborted, ...).
                // Throw the exception prepared by RestSharp
                throw new Exception(response.ErrorMessage, response.ErrorException);
            }

            if ((int)response.StatusCode >= 400)
            {
                // There is an issue which is described in the HTTP response.
                // Build an throw a BitbucketException, since the message should be provided by Bitbucket.
                throw BuildBitbucketException(response);
            }
        }

        protected virtual BitbucketException BuildBitbucketException(IRestResponse response)
        {
            // response.ErrorException is not useful for caller in that case, so it's useless to transmit it as an inner exception
            return new BitbucketException(response.StatusCode, DescribeStatus(response));
        }

        /// <summary>
        /// Describes the HTTP status of a response, without any part of its body,
        /// since the body may come from a server which is not Bitbucket.
        /// </summary>
        protected static string DescribeStatus(IRestResponse response)
        {
            var status = ((int)response.StatusCode).ToString();
            return string.IsNullOrWhiteSpace(response.StatusDescription)
                ? status
                : status + " " + response.StatusDescription;
        }

        internal enum RedirectKind
        {
            SameOrigin,
            CrossOrigin,
            Downgrade,
        }

        /// <summary>
        /// Compares the origin (scheme, host and port) of a redirect target with the origin of the client base url.
        /// </summary>
        internal static RedirectKind ClassifyRedirect(Uri baseUrl, Uri target)
        {
            var sameScheme = string.Equals(baseUrl.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase);
            var sameHost = string.Equals(baseUrl.Host, target.Host, StringComparison.OrdinalIgnoreCase);
            // Uri.Port is normalized, so an explicit default port equals an implicit one
            if (sameScheme && sameHost && baseUrl.Port == target.Port)
            {
                return RedirectKind.SameOrigin;
            }

            if (string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectKind.Downgrade;
            }

            return RedirectKind.CrossOrigin;
        }

        private IRestRequest BuildRestRequest(string url, Method method, object body, IDictionary<string, object> requestParameters)
        {
            var request = new RestRequest(url, method);
            if (requestParameters != null)
            {
                foreach (var requestParameter in requestParameters)
                {
                    var key = requestParameter.Key;
                    var val = requestParameter.Value;

                    if (val is IEnumerable enumerable && !(enumerable is string))
                    {
                        foreach (var item in enumerable)
                            request.AddParameter(key, item);
                    }
                    else
                        request.AddParameter(key, val);
                }
            }

            if (method == Method.PUT || method == Method.POST)
            {
                AddBody(request, body);
            }

            return request;
        }

        protected abstract void AddBody(IRestRequest request, object body);

        private static IRestResponse ExecuteRequestWithManualFollowRedirect(IRestRequest request, IRestClient client)
        {
            var result = client.Execute(request);
            if (result.StatusCode == HttpStatusCode.Redirect)
            {
                request = BuildRedirectedRestRequest(request, client, result, out var redirectClient);
                result = redirectClient.Execute(request);
            }

            return result;
        }

        private static async Task<IRestResponse> ExecuteRequestWithManualFollowRedirectAsync(IRestRequest request, IRestClient client, CancellationToken token)
        {
            var result = await client.ExecuteAsync(request, token);
            if (result.StatusCode == HttpStatusCode.Redirect)
            {
                request = BuildRedirectedRestRequest(request, client, result, out var redirectClient);
                result = await redirectClient.ExecuteAsync(request, token);
            }

            return result;
        }

        /// <summary>
        /// Builds the request that follows a redirect, and selects the client that must execute it.
        /// The authenticated client is only used when the redirect stays on the same origin.
        /// </summary>
        private static IRestRequest BuildRedirectedRestRequest(IRestRequest request, IRestClient client,
            IRestResponse result, out IRestClient redirectClient)
        {
            var target = GetRedirectTarget(result, client.BaseUrl);

            switch (ClassifyRedirect(client.BaseUrl, target))
            {
                case RedirectKind.SameOrigin:
                    redirectClient = client;
                    return BuildSameOriginRestRequest(request, StripBaseUrl(target.OriginalString, client.BaseUrl.ToString()));
                case RedirectKind.CrossOrigin:
                    // The authenticator of the client must not be applied to another origin, so use a client without it.
                    // Such a redirect typically is authenticated by its own query string (a presigned url),
                    // which must reach the target unchanged: use the url as is, instead of parsing and encoding its query again.
                    redirectClient = CreateAnonymousRedirectClient(client);
                    return new RestRequest(target.OriginalString, request.Method);
                default:
                    throw new BitbucketException(HttpStatusCode.Redirect, "Refused to follow a redirect from HTTPS to HTTP.");
            }
        }

        private static IRestRequest BuildSameOriginRestRequest(IRestRequest request, string redirectUrl)
        {
            NameValueCollection queryValues;
            var queryIndex = redirectUrl.IndexOf('?');
            if (queryIndex >= 0)
            {
                queryValues = HttpUtility.ParseQueryString(redirectUrl.Substring(queryIndex + 1));
                redirectUrl = redirectUrl.Substring(0, queryIndex);
            }
            else
            {
                queryValues = new NameValueCollection();
            }

            request = new RestRequest(redirectUrl, request.Method);
            foreach (var queryKey in queryValues.AllKeys)
            {
                request.AddQueryParameter(queryKey, queryValues[queryKey]);
            }

            return request;
        }

        private static IRestClient CreateAnonymousRedirectClient(IRestClient original)
        {
            // Only copy the settings that are about reaching the network, never the authenticator,
            // the default parameters or the cookies of the original client.
            var client = new RestClient
            {
                Timeout = original.Timeout,
                ReadWriteTimeout = original.ReadWriteTimeout,
                Proxy = original.Proxy,
                UserAgent = original.UserAgent,
            };
            ConfigureHandlers(client);
            return client;
        }

        private static Uri GetRedirectTarget(IRestResponse result, Uri requestBaseUrl)
        {
            var location = result.Headers.Where(header => header.Name == "Location").Select(header => header.Value).First().ToString();
            return new Uri(requestBaseUrl, location);
        }

        private static string StripBaseUrl(string redirectUrl, string requestBaseUrl)
        {
            if (redirectUrl.StartsWith(requestBaseUrl))
            {
                redirectUrl = redirectUrl.Remove(0, requestBaseUrl.Length);
            }
            return redirectUrl;
        }
    }
}