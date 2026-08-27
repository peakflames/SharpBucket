# How to get started

In order to run the SharpBucket's integration tests you should configure your development environment to inject OAuth credentials that will be used to connect to Bitbucket.  
We recommend you to create a separate Bitbucket account that will be used only to run the tests. (log out and go [here](https://bitbucket.org/account/signup/) to create your test account)  
Then in that test account you will need to create an OAuth Consumer. For that, go to "Settings > OAuth > Add consumer"  
Your consumer must have a name, a callback URL, and all permissions (since we should tests all possible API calls). Look at that [screenshot](./Assets/OauthConsumerConfig.png) is you need help.

Now, in the Oauth screen you can click on your OAuth consumer to reveal its key and secret like on that [screenshot](./Assets/OauthConsumerKeyAndSecret.png)

After that create two environment variables for the key and the secret key. They should be named:
- SB_CONSUMER_KEY
- SB_CONSUMER_SECRET_KEY

Also create an environment variable with your user name (the name of your account, not the name of the OAuth consumer)
- SB_ACCOUNT_NAME

Other prerequisites in your Bitbucket test account:
- You should create one team (and by definition be member of it)

# Where to get started

The best way to get started is to implement a part of the API that you need but is not covered yet. 

You can check the issues ([here are some](https://github.com/MitjaBezensek/SharpBucket/labels/easy-fix) that should be easy to fix) or look which api calls [are still missing](https://github.com/MitjaBezensek/SharpBucket/blob/master/Coverage.md). A few topics that still need to be covered:
- certain parts of the V2 api
- support for OAuth2 is quite basic
- improve test coverage
- async calls
- logo!

# Guidelines

Just a few guidelines:
- for simple improvements (like adding a missing api call) just submit a PR 
- for architecture and breaking changes please open an issue first and lets discuss what you have planned
- try to stick to the existing formatting as much as possible
- any reformatting should be done in a separate PR in order to make it easier to review it

# Continuous integration

This fork uses [GitHub Actions](https://github.com/peakflames/SharpBucket/actions) for CI, enabled for pull requests as well as pushes to `develop` and `main`:

[![CI](https://github.com/peakflames/SharpBucket/actions/workflows/ci.yml/badge.svg)](https://github.com/peakflames/SharpBucket/actions/workflows/ci.yml)

The CI workflow builds the library across all target frameworks. It does not run the integration tests, since those require live Bitbucket credentials (see above) and create/delete real repositories — see `CLAUDE.md` for details. Run them locally with your own test account before opening a PR that touches API behavior.
