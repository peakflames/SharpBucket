# Contributing to peakflames/SharpBucket

This is a [peakflames](https://github.com/peakflames) fork of [MitjaBezensek/SharpBucket](https://github.com/MitjaBezensek/SharpBucket), published to NuGet as [`Peakflames.SharpBucket`](https://www.nuget.org/packages/Peakflames.SharpBucket/). See `Contribution.md` for API coverage and test-credential setup shared with upstream.

## Team Structure

| Role | Group | Capabilities |
|---|---|---|
| Maintainer | `team-sixseven` | Review and merge PRs, trigger releases via GitHub Actions |
| Admin | Repository admins | All of the above plus branch protection and secret management |

---

## Branch Strategy

| Branch | Purpose |
|---|---|
| `master` | **Frozen mirror of `upstream/master`.** Never receives fork commits directly. Exists so changes can still be sent back to upstream as a PR. |
| `develop` | Active development — all fork PRs target this branch |
| `main` | Release-only — always reflects the latest published NuGet package |

`feature/<slug>` and `hotfix/<slug>` branches off `develop` are the normal way to work.

### A note on review enforcement

Branch protection on `main` and `develop` does **not** technically require a PR or a review before merging (`allow_force_pushes` and `allow_deletions` are disabled; `required_pull_request_reviews` is not set). This is a deliberate, documented gap, not an oversight: GitHub's classic branch-protection API silently drops `github-actions` from `bypass_pull_request_allowances` (confirmed — team-based bypass entries are accepted and echoed back by the API; app-based entries for `github-actions` are not, on this repo). That bypass is what `release.yml` needs to push and merge directly to protected branches without a human in the loop. Rather than route around it with an admin PAT, review is enforced by convention:

- **All PRs target `develop`, and should get one approving review from `team-sixseven`** before merging, even though GitHub won't block the merge if one isn't there.
- **Never commit directly to `main`.** It only moves via the release workflow's `develop → main` merge.
- **Never commit directly to `master`.** It only moves via `git merge --ff-only upstream/master` (see Upstream Sync, below).

If `github-actions` bypass is ever configured by hand through the repo Settings UI (Branches → Edit rule → Allow specified actors to bypass required pull requests → add "GitHub Actions"), `required_approving_review_count: 1` and `require_code_owner_reviews: true` can be turned on to make this enforced rather than conventional. A `.github/CODEOWNERS` file (`* @peakflames/team-sixseven`) already exists so that flag would take effect immediately.

### Making Changes

1. Create a feature branch from `develop`: `git checkout -b feature/my-change develop`
2. `gh repo set-default peakflames/SharpBucket` once, locally — on a fork, `gh pr create` and the GitHub UI otherwise default the PR base to the *upstream* repo (`MitjaBezensek/SharpBucket`), not this one.
3. Open a PR targeting `develop`.

### Upstream Sync

To pull in upstream changes:

```bash
git fetch upstream
git checkout master
git merge --ff-only upstream/master
git push origin master
git checkout develop
git merge master
```

To send a change back upstream, branch off `master` (not `develop`) and open a PR against `MitjaBezensek/SharpBucket`.

---

## Release Process

Releases are self-service via GitHub Actions.

### Pre-Release Checklist

- [ ] All intended changes are merged to `develop`
- [ ] `develop` builds cleanly locally: `dotnet build SharpBucket/SharpBucket.csproj -c Release`
- [ ] You know the release version (`new_version`) and a one-line changelog summary
- [ ] You know the next development version (`next_dev_version`, usually the next patch)

### Triggering a Release

1. Go to the repository on GitHub
2. Navigate to **Actions → Release to NuGet**
3. Click **Run workflow**
4. Fill in the three inputs:

| Input | Description | Example |
|---|---|---|
| `new_version` | Version to release | `0.17.1` |
| `changelog_entry` | One-line release summary | `Fix: correct pagination in RepositoriesEndPoint` |
| `next_dev_version` | Next development version | `0.17.2` |

5. Click **Run workflow** and monitor the Actions tab

### What the Workflow Does

1. Updates `<Version>` in `SharpBucket/SharpBucket.csproj` on `develop`
2. Prepends a `## X.Y.Z` changelog entry to `CHANGELOG.md` on `develop`
3. Runs `dotnet build` — aborts on failure
4. Commits and pushes the release changes to `develop`
5. Merges `develop → main` (no fast-forward)
6. Creates and pushes tag `vX.Y.Z`
7. Bumps `develop` to `next_dev_version` with a `- TBD` changelog placeholder
8. Packs the NuGet package (`.nupkg` and `.snupkg`) and publishes both to NuGet.org
9. Creates a GitHub Release

The workflow is idempotent — if it fails partway through and is re-run with the same inputs, it safely skips steps that already completed.

### Verifying a Release

After the workflow completes:

- Tag `vX.Y.Z` is visible under [Releases](https://github.com/peakflames/SharpBucket/releases)
- `develop` is bumped to `next_dev_version` with a `- TBD` placeholder in `CHANGELOG.md`
- Package appears on NuGet.org within a few minutes: `https://www.nuget.org/packages/Peakflames.SharpBucket/X.Y.Z`

---

## Admin Reference

### Required Repository Secret

| Secret | Purpose |
|---|---|
| `NUGET_API_KEY` | API key for publishing to NuGet.org — must have push access to `Peakflames.SharpBucket` (glob scope `Peakflames.*` covers first publish, since the package doesn't exist yet) |

### Branch Protection (as applied)

Applied to both `main` and `develop`:

```json
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": false
}
```

Re-apply with:

```bash
gh api repos/peakflames/SharpBucket/branches/main/protection \
  --method PUT \
  --header "Accept: application/vnd.github+json" \
  --input - <<'EOF'
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": false
}
EOF
```

(repeat for `branches/develop/protection`)
