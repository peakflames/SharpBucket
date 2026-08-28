using RestSharp;
using RestSharp.Authenticators;

namespace SharpBucket.Authentication
{
    /// <summary>
    /// This class is used to authenticate with the Bitbucket REST API by using an OAuth 2.0
    /// access token that has already been obtained by other means, for example by an
    /// authorization code grant performed by another component.
    /// </summary>
    /// <remarks>
    /// The caller owns the whole token lifecycle. Unlike <see cref="OAuth2ClientCredentials"/>,
    /// this class never contacts the token endpoint and never refreshes the token on its own.
    /// Build a new instance once a new access token has been obtained.
    /// </remarks>
    public sealed class OAuth2BearerAuthentication : Authenticate
    {
        private const string TokenType = "Bearer";

        public OAuth2BearerAuthentication(string accessToken, string baseUrl)
        {
            Client = new RestClient(baseUrl)
            {
                Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(accessToken, TokenType)
            };
        }
    }
}
