# SharpBucket (peakflames fork)

Fork of [MitjaBezensek/SharpBucket](https://github.com/MitjaBezensek/SharpBucket), a .NET wrapper for the Bitbucket REST API. Published to NuGet as `Peakflames.SharpBucket`. Branch model and release process are documented in `CONTRIBUTING.md` — read it before touching `main`, `develop`, or `.github/workflows/`.

## Build & Test

```
dotnet restore SharpBucket/SharpBucket.csproj
dotnet build SharpBucket/SharpBucket.csproj -c Release
```

Do not run `dotnet build SharpBucket.sln` on Linux — `SharpBucketCli.csproj` is an old-style .NET Framework 4.8 project and fails outside Windows. Build the library csproj directly.

`SharpBucketTests` requires live Bitbucket credentials (`SB_CONSUMER_KEY`, `SB_CONSUMER_SECRET_KEY`, `SB_ACCOUNT_NAME`, `SB_ACCOUNT_EMAIL`, `SB_ACCOUNT_PASSWORD`) and creates/deletes real Bitbucket repositories via `SampleRepositories` — see `Contribution.md` for setup. **Never commit these as literal values anywhere in the repo; they exist only as environment variables.**

## Version Management

Version lives in `SharpBucket/SharpBucket.csproj` → `<Version>`. Semver:

- **Patch** — bug fixes, minor improvements
- **Minor** — new features, backward compatible
- **Major** — breaking changes

Releases are performed via the **Release to NuGet** GitHub Actions workflow — see `CONTRIBUTING.md` for full instructions. Don't bump `<Version>` by hand outside that workflow.

## CHANGELOG.md Format

```markdown
## X.Y.Z

- **Breaking Change**: Description (if applicable)
- Feature: Description
- Fix: Description
```

No `v` prefix on the heading, no dates, no `## Unreleased` section, newest entry first.

## Change Checklist

1. Implement the change
2. Update tests
3. Add a `CHANGELOG.md` entry under the current (unreleased) version if one doesn't exist yet
4. Build to verify: `dotnet build SharpBucket/SharpBucket.csproj -c Release`
