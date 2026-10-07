## 0.19.0

- **Breaking Change**: Exception messages for non-Bitbucket error responses now contain the HTTP status instead of the raw response body.
- **Breaking Change**: Files whose names contain `?` or `#` now throw an `ArgumentException` instead of producing a wrong request.
- Security: authentication headers are no longer sent to a different origin when following a redirect. Redirects from HTTPS to HTTP are refused. Path segments containing dot-segments, `?`, `#`, or `\` are rejected.

## 0.18.0

- Added `OAuth2BearerToken(accessToken)` and `OAuth2BearerAuthentication`, to authenticate with an OAuth2 access token that has already been obtained by other means. Unlike `OAuth2ClientCredentials`, the token is never refreshed: the caller owns the token lifecycle.

## 0.17.0

- Fork point: `Peakflames.SharpBucket` published from [peakflames/SharpBucket](https://github.com/peakflames/SharpBucket), a fork of [MitjaBezensek/SharpBucket](https://github.com/MitjaBezensek/SharpBucket) v0.17.0
