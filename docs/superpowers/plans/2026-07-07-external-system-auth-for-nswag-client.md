# ExternalSystem Auth for a Generated NSwag Client — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a reusable "ExternalSystem self-authentication" primitive in `Cauca.ApiClient`, then create a new NSwag-generated client for the external API `Cauca.SurviRaoTransfer.Api` that authenticates itself (ExternalSystem login + token refresh) with zero effort from consumers.

**Architecture:** `Cauca.ApiClient` gains a `DelegatingHandler` (`CaucaExternalSystemAuthHandler`) that reuses the existing internal `FluentRefreshTokenHandler` (same login/refresh/re-login logic as `FluentBaseSecureService`) to attach a Bearer token and transparently refresh/re-login on the `Token-Expired` / `Refresh-Token-Expired` / `Token-Invalid` 401 headers. A public DI extension `AddCaucaExternalSystemAuth(...)` wires the handler onto any typed `HttpClient` pipeline. In the `transfert-api` repo, a new generated client package (`Cauca.SurviRaoTransfer.ExternalClient`) references `Cauca.ApiClient` and ships a one-line registration `AddSurviRaoTransferExternalClient(baseUrl, apiKey)`; CI generates it from the external API's OpenAPI document, mirroring the existing internal-client machinery.

**Tech Stack:** .NET 10, C#, `HttpClient` + `IHttpClientFactory`, Polly, NSwag (local dotnet tool), NUnit + FluentAssertions, GitLab CI, Wolverine.HTTP + Swashbuckle (external API).

## Two repositories

This plan spans two working copies. Every task states which repo it lands in.

- **`Cauca.ApiClient`** — `C:\dev\cauca-apiclient` (branch `DroppingFlurl`). Tasks A1–A4.
- **`transfert-api`** — `C:\dev\transfert-api\Cauca.SurviRaoTransfer` (repo root `C:\dev\transfert-api`). Tasks B1–B8.

**Sequencing:** Part A must be completed, version-bumped, and published to the NuGet feed **before** Part B can pin the new `Cauca.ApiClient` version. Part B tasks that only create files/scripts (B1–B6) can be authored in parallel, but the generated client cannot build/pack until the new `Cauca.ApiClient` is available.

## Global Constraints

- **English-only identifiers**; no user-facing hardcoded strings.
- **Zero compiler warnings** — the build must stay clean.
- **Preserve existing behavior:** `FluentBaseService`, `FluentBaseSecureService`, and `FluentRefreshTokenHandler` public/observable behavior must not change. All existing tests must still pass.
- **Test framework:** NUnit (`[TestFixture]`, `[Test]`, `[SetUp]`) + FluentAssertions (`.Should()`), matching `Cauca.ApiClient.Tests`.
- **Minimal public surface:** only `AddCaucaExternalSystemAuth` (and any type its signature requires) becomes public. The handler stays `internal` (tests see it via the existing `InternalsVisibleTo("Cauca.ApiClient.Tests")`).
- **Token handling:** never persisted. Token lives in-memory for the handler instance's lifetime only; refresh/re-login happen on the ExternalSystem 401 headers.
- **Do not modify** the internal client (`Cauca.SurviRaoTransfer.Api.Internal`, `nswag.internal-client.json`, `generated/Cauca.SurviRaoTransfer.InternalClient`, `scripts/*internal*`) or the hand-written `Cauca.SurviRaoTransfer.Client`.
- **Header contract (verbatim, already in `RestResponseExtensions`):** `AccessTokenExpired = "Token-Expired"`, `RefreshTokenExpired = "Refresh-Token-Expired"`, `RefreshTokenInvalid = "Token-Invalid"`.

## Open decisions (defaults chosen — override before execution if needed)

1. **New client package name:** `Cauca.SurviRaoTransfer.ExternalClient` (parallels `InternalClient`; `Cauca.SurviRaoTransfer.Client` is taken by the hand-written client). NSwag class `SurviRaoTransferExternalClient`, interface `ISurviRaoTransferExternalClient`, namespace `Cauca.SurviRaoTransfer.ExternalClient`.
2. **`Cauca.ApiClient` version to publish/pin:** current published is `4.1.0-beta2`. This feature ships as **`4.1.0-beta3`** (bump in Task A4). Part B pins that exact version.
3. **API prefix / base URL:** the hand-written `ImportClient` uses `apiPrefix = "api"`; login therefore resolves to `{baseUrl}/api/Authentication/logonforexternalsystem`. The generated client's registration defaults `apiPrefix = "api"`. Task B7 verifies this against the exported OpenAPI document (server URL + paths) and adjusts `baseUrl`/`apiPrefix` if the doc already embeds `/api`.

---

# Part A — `Cauca.ApiClient` reusable self-auth surface

**Repo:** `C:\dev\cauca-apiclient`

## Reference facts (already verified in the codebase)

- `FluentRefreshTokenHandler` (`Cauca.ApiClient\Services\FluentRefreshTokenHandler.cs`) is `internal sealed` with constructor:
  `FluentRefreshTokenHandler(IConfiguration configuration, AccessInformation accessInformation, IAsyncPolicy policy, Func<HttpClient> client = null, string apiPrefix = null)` and methods `Task Login(CancellationToken)` and `Task RefreshToken(CancellationToken)`. `RefreshToken` already auto-re-logs-in when the refresh token is expired/invalid.
- `AccessInformation` (public, in `Cauca.ApiClient\Services\BaseSecureService.cs`): `{ string AuthorizationType; string AccessToken; string RefreshToken; }` (defaults `""`).
- `FluentResponseExtensions` (`internal static`, `Cauca.ApiClient\Services\FluentResponseExtensions.cs`) already provides, on `HttpResponseMessage`: `IsUnauthorized()`, `AccessTokenIsExpired()`, `RefreshTokenIsExpired()`, `RefreshTokenIsInvalid()`.
- `FluentRetryPolicyBuilder` (`Cauca.ApiClient\Services\FluentRetryPolicyBuilder.cs`) implements `IRetryPolicyBuilder.BuildRetryPolicy(int) : IAsyncPolicy`.
- Test transport: `Cauca.ApiClient.Tests\Helpers\TestHttpMessageHandler.cs` — `EnqueueJsonResponse<T>(body, statusCode, params (string,string)[] headers)`, `EnqueueTimeout()`, `.Requests` (`RecordedRequest` with `.RequestUri`, `.Method`, `.Body`, `.HasHeader(name,value)`), and `CreateClientFactory() : Func<HttpClient>` (wraps a single shared handler → one ordered request log).
- Existing secure-flow tests to mirror for style: `Cauca.ApiClient.Tests\Services\BaseSecureClientTests.cs`.

---

### Task A1: `CaucaExternalSystemAuthHandler` — attach token + login on first call

**Files:**
- Create: `Cauca.ApiClient\Services\CaucaExternalSystemAuthHandler.cs`
- Test: `Cauca.ApiClient.Tests\Services\CaucaExternalSystemAuthHandlerTests.cs`

**Interfaces:**
- Consumes: `IConfiguration`, `AccessInformation`, `FluentRefreshTokenHandler`, `FluentResponseExtensions`, `IAsyncPolicy` (Polly).
- Produces: `internal sealed class CaucaExternalSystemAuthHandler : DelegatingHandler` with constructor
  `CaucaExternalSystemAuthHandler(IConfiguration configuration, string apiPrefix, Func<HttpClient> authClientFactory, IAsyncPolicy policy)`.

- [ ] **Step 1: Write the failing test** (login-on-first-call + Authorization header)

Create `Cauca.ApiClient.Tests\Services\CaucaExternalSystemAuthHandlerTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class CaucaExternalSystemAuthHandlerTests
{
    private MockConfiguration configuration;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/",
            UserId = "the-api-key",
            UseExternalSystemLogin = true
        };
    }

    private HttpClient CreateClient(TestHttpMessageHandler transport)
    {
        var handler = new CaucaExternalSystemAuthHandler(
            configuration,
            apiPrefix: null,
            authClientFactory: transport.CreateClientFactory(),
            policy: Policy.NoOpAsync())
        {
            InnerHandler = transport
        };
        return new HttpClient(handler) { BaseAddress = new System.Uri("http://test/") };
    }

    [Test]
    public async Task WhenNoToken_LogsInWithApiKeyThenAttachesBearerToken()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(2);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[0].Body.Should().Contain("the-api-key");
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[1].HasHeader("Authorization", "Bearer AccessToken").Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthHandlerTests`
Expected: FAIL — `CaucaExternalSystemAuthHandler` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `Cauca.ApiClient\Services\CaucaExternalSystemAuthHandler.cs`:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Polly;

namespace Cauca.ApiClient.Services;

internal sealed class CaucaExternalSystemAuthHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly string _apiPrefix;
    private readonly Func<HttpClient> _authClientFactory;
    private readonly IAsyncPolicy _policy;
    private readonly AccessInformation _accessInformation = new();
    private readonly SemaphoreSlim _loginGate = new(1, 1);

    public CaucaExternalSystemAuthHandler(IConfiguration configuration, string apiPrefix, Func<HttpClient> authClientFactory, IAsyncPolicy policy)
    {
        _configuration = configuration;
        _apiPrefix = apiPrefix;
        _authClientFactory = authClientFactory;
        _policy = policy;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync(cancellationToken);
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        SetAuthorizationHeader(request);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessInformation.AccessToken))
            return;

        await _loginGate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(_accessInformation.AccessToken))
                await CreateRefreshTokenHandler().Login(cancellationToken);
        }
        finally
        {
            _loginGate.Release();
        }
    }

    private void SetAuthorizationHeader(HttpRequestMessage request)
    {
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", $"{_accessInformation.AuthorizationType} {_accessInformation.AccessToken}");
    }

    private FluentRefreshTokenHandler CreateRefreshTokenHandler()
    {
        return new FluentRefreshTokenHandler(_configuration, _accessInformation, _policy, _authClientFactory, _apiPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _loginGate.Dispose();
        base.Dispose(disposing);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthHandlerTests`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
cd C:/dev/cauca-apiclient
git add Cauca.ApiClient/Services/CaucaExternalSystemAuthHandler.cs Cauca.ApiClient.Tests/Services/CaucaExternalSystemAuthHandlerTests.cs
git commit -m "Add CaucaExternalSystemAuthHandler with login-on-first-call"
```

---

### Task A2: Refresh-and-retry on `Token-Expired`; re-login-and-retry on refresh-token 401

**Files:**
- Modify: `Cauca.ApiClient\Services\CaucaExternalSystemAuthHandler.cs`
- Modify: `Cauca.ApiClient.Tests\Services\CaucaExternalSystemAuthHandlerTests.cs`

**Interfaces:**
- Consumes: `FluentResponseExtensions.IsUnauthorized/AccessTokenIsExpired/RefreshTokenIsExpired/RefreshTokenIsInvalid` (on `HttpResponseMessage`), `FluentRefreshTokenHandler.RefreshToken/Login`.
- Produces: unchanged constructor; `SendAsync` now retries once after refresh/re-login.

- [ ] **Step 1: Write the failing tests** (append two tests to the fixture)

Add to `CaucaExternalSystemAuthHandlerTests.cs`:

```csharp
    [Test]
    public async Task WhenAccessTokenExpired_RefreshesThenRetriesWithNewToken()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.AccessTokenExpired, "True"));
        transport.EnqueueJsonResponse(new TokenRefreshResult { AccessToken = "RefreshedToken" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(4);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[2].RequestUri.Should().Be("http://test/Authentication/refreshforexternalsystem");
        transport.Requests[3].RequestUri.Should().Be("http://test/mock");
        transport.Requests[3].HasHeader("Authorization", "Bearer RefreshedToken").Should().BeTrue();
    }

    [Test]
    public async Task WhenRefreshTokenExpired_LogsBackInThenRetries()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.RefreshTokenExpired, "True"));
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "SecondToken", RefreshToken = "SecondRefresh" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(4);
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[2].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[3].HasHeader("Authorization", "Bearer SecondToken").Should().BeTrue();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthHandlerTests`
Expected: FAIL — the two new tests get `500`/no-retry (handler returns the 401 without retrying). The refresh test sees 2 requests, not 4.

- [ ] **Step 3: Extend the implementation**

In `CaucaExternalSystemAuthHandler.cs`, add `using Cauca.ApiClient.Extensions;` and replace `SendAsync` with:

```csharp
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync(cancellationToken);
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        SetAuthorizationHeader(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsUnauthorized())
            return response;

        if (response.AccessTokenIsExpired())
        {
            response.Dispose();
            await CreateRefreshTokenHandler().RefreshToken(cancellationToken);
            return await ResendAsync(request, cancellationToken);
        }

        if (response.RefreshTokenIsExpired() || response.RefreshTokenIsInvalid())
        {
            response.Dispose();
            await CreateRefreshTokenHandler().Login(cancellationToken);
            return await ResendAsync(request, cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> ResendAsync(HttpRequestMessage originalRequest, CancellationToken cancellationToken)
    {
        using var retry = await CloneAsync(originalRequest);
        SetAuthorizationHeader(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content is not null)
        {
            var buffer = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(buffer);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
cd C:/dev/cauca-apiclient
git add Cauca.ApiClient/Services/CaucaExternalSystemAuthHandler.cs Cauca.ApiClient.Tests/Services/CaucaExternalSystemAuthHandlerTests.cs
git commit -m "Add refresh-and-retry and re-login-and-retry to CaucaExternalSystemAuthHandler"
```

---

### Task A3: `AddCaucaExternalSystemAuth` DI extension

**Files:**
- Modify: `Cauca.ApiClient\Extensions\ServiceCollectionExtensions.cs`
- Test: `Cauca.ApiClient.Tests\Services\CaucaExternalSystemAuthExtensionsTests.cs`

**Interfaces:**
- Consumes: `CaucaExternalSystemAuthHandler`, `FluentRetryPolicyBuilder`, `IHttpClientBuilder`, `IHttpClientFactory`.
- Produces: `public static IHttpClientBuilder AddCaucaExternalSystemAuth(this IHttpClientBuilder builder, IConfiguration configuration, string apiPrefix = null)`.

- [ ] **Step 1: Write the failing test** (end-to-end through a typed client)

Create `Cauca.ApiClient.Tests\Services\CaucaExternalSystemAuthExtensionsTests.cs`:

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class CaucaExternalSystemAuthExtensionsTests
{
    private sealed class ProbeClient(HttpClient httpClient)
    {
        public Task<HttpResponseMessage> CallAsync() => httpClient.GetAsync("mock");
    }

    [Test]
    public async Task AddCaucaExternalSystemAuth_AttachesHandlerThatLogsInAndCallsApi()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse());

        var configuration = new MockConfiguration { ApiBaseUrl = "http://test/", UserId = "the-api-key", UseExternalSystemLogin = true };
        var services = new ServiceCollection();
        services
            .AddHttpClient<ProbeClient>(client => client.BaseAddress = new Uri("http://test/"))
            .ConfigurePrimaryHttpMessageHandler(() => new PassThroughHandler(transport))
            .AddCaucaExternalSystemAuth(configuration);

        using var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<ProbeClient>();

        var response = await probe.CallAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        transport.Requests.Should().HaveCount(2);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[1].HasHeader("Authorization", "Bearer AccessToken").Should().BeTrue();
    }
}
```

Create `Cauca.ApiClient.Tests\Helpers\PassThroughHandler.cs` (routes the primary send into the shared `TestHttpMessageHandler` so business calls and auth calls share one ordered log):

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Tests.Helpers;

public sealed class PassThroughHandler(TestHttpMessageHandler transport) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return transport.CreateClientFactory()().SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthExtensionsTests`
Expected: FAIL — `AddCaucaExternalSystemAuth` does not exist (compile error).

- [ ] **Step 3: Add the extension**

Append to `Cauca.ApiClient\Extensions\ServiceCollectionExtensions.cs` (inside the existing `ServiceCollectionExtensions` class; add `using Cauca.ApiClient.Configuration;`, `using Cauca.ApiClient.Services;`, `using System.Net.Http;`, `using System.Threading;` as needed):

```csharp
    public static IHttpClientBuilder AddCaucaExternalSystemAuth(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        string apiPrefix = null)
    {
        var authClientName = $"CaucaExternalSystemAuth:{builder.Name}";
        builder.Services.AddHttpClient(authClientName, static client => client.Timeout = Timeout.InfiniteTimeSpan);

        return builder.AddHttpMessageHandler(serviceProvider => new CaucaExternalSystemAuthHandler(
            configuration,
            apiPrefix,
            () => serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(authClientName),
            new FluentRetryPolicyBuilder().BuildRetryPolicy(3)));
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Cauca.ApiClient.Tests --filter CaucaExternalSystemAuthExtensionsTests`
Expected: PASS (1 test).

- [ ] **Step 5: Run the full suite (no regressions)**

Run: `dotnet test Cauca.ApiClient.Tests`
Expected: PASS — all pre-existing tests plus the 4 new ones. Report the count.

- [ ] **Step 6: Commit**

```bash
cd C:/dev/cauca-apiclient
git add Cauca.ApiClient/Extensions/ServiceCollectionExtensions.cs Cauca.ApiClient.Tests/Services/CaucaExternalSystemAuthExtensionsTests.cs Cauca.ApiClient.Tests/Helpers/PassThroughHandler.cs
git commit -m "Add AddCaucaExternalSystemAuth DI extension for typed clients"
```

---

### Task A4: Version bump + package build

**Files:**
- Modify: `Cauca.ApiClient\Cauca.ApiClient.csproj:11-13` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`) and `:15` (`<PackageReleaseNotes>`)

**Interfaces:**
- Produces: `Cauca.ApiClient` NuGet package version `4.1.0-beta3` containing `AddCaucaExternalSystemAuth`.

- [ ] **Step 1: Bump the version**

Edit `Cauca.ApiClient\Cauca.ApiClient.csproj`:
- `<Version>4.1.0-beta2</Version>` → `<Version>4.1.0-beta3</Version>`
- `<PackageReleaseNotes>Fluent API.</PackageReleaseNotes>` → `<PackageReleaseNotes>Fluent API. ExternalSystem self-auth handler for generated clients.</PackageReleaseNotes>`

(Leave `<AssemblyVersion>`/`<FileVersion>` at `4.1.0.0` — they only carry major.minor.patch and are already correct.)

- [ ] **Step 2: Build the package**

Run: `dotnet build Cauca.ApiClient/Cauca.ApiClient.csproj --configuration Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, and a `Cauca.ApiClient.4.1.0-beta3.nupkg` under `Cauca.ApiClient/bin/Release/` (`GeneratePackageOnBuild` is true).

- [ ] **Step 3: Commit**

```bash
cd C:/dev/cauca-apiclient
git add Cauca.ApiClient/Cauca.ApiClient.csproj
git commit -m "Bump Cauca.ApiClient to 4.1.0-beta3 for ExternalSystem self-auth"
```

- [ ] **Step 4: Publish (human/CI action — not automated by this plan)**

Publish `4.1.0-beta3` to the NuGet feed the `transfert-api` repo restores from. Part B depends on this being available. **Do not** self-serve credentials; the maintainer runs the existing publish pipeline for `Cauca.ApiClient`.

---

# Part B — New generated external client (`transfert-api`)

**Repo:** `C:\dev\transfert-api` (project dir `C:\dev\transfert-api\Cauca.SurviRaoTransfer`)

## Reference facts (already verified)

- Internal machinery to mirror: `nswag.internal-client.json`, `scripts/generate-internal-openapi.sh`, `scripts/internal-api-has-changed.sh`, generated project `generated/Cauca.SurviRaoTransfer.InternalClient/{csproj,README.md}`, and CI job `publish-internal-api-client` in `.gitlab-ci.yml`.
- The internal API exposes OpenAPI export via a `--export-openapi <path>` argument handled in `Cauca.SurviRaoTransfer.Api.Internal/Program.cs` (`TryGetOpenApiExportPath` + a minimal Wolverine+Swashbuckle app). **The external API `Cauca.SurviRaoTransfer.Api/Program.cs` does NOT have this yet** — Task B1 adds it.
- The external API already implements ExternalSystem login server-side (`Cause.SecurityManagement.Wolverine.ExternalSystem`, `options.AddExternalSystemEndpoints()`, `AddDualExternalSystemAuthentication`), and uses Swashbuckle (`AddSwaggerDocumentation<GeneralHeadersFilter>()`).
- The hand-written client shows the exact auth config expected: `ImportClientConfiguration : IConfiguration` with `UseExternalSystemLogin = true`, `apiPrefix = "api"`, timeout 100s.
- Solution file: `Cauca.SurviRaoTransfer.slnx`. Generated client projects are built directly by path in CI (not part of the solution); their generated `.cs` is produced at CI time.

---

### Task B1: Add `--export-openapi` to the external API

**Files:**
- Modify: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\Cauca.SurviRaoTransfer.Api\Program.cs`

**Interfaces:**
- Produces: `dotnet Cauca.SurviRaoTransfer.Api.dll --export-openapi <path>` writes the external API's OpenAPI v3 JSON and exits 0, without needing a database.

- [ ] **Step 1: Add the export entrypoint**

In `Cauca.SurviRaoTransfer.Api\Program.cs`, add the same helper the internal API uses at the bottom (before the `namespace` block):

```csharp
static bool TryGetOpenApiExportPath(string[] args, out string path)
{
    const string option = "--export-openapi";

    var optionIndex = Array.IndexOf(args, option);
    if (optionIndex >= 0 && optionIndex + 1 < args.Length)
    {
        path = args[optionIndex + 1];

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        return true;
    }

    path = string.Empty;
    return false;
}
```

Then, immediately after `var builder = WebApplication.CreateBuilder(args);` (line ~22), insert the export short-circuit that builds a minimal app (no DB, no auth middleware — just enough for Swashbuckle to produce the document):

```csharp
    if (TryGetOpenApiExportPath(args, out var openApiExportPath))
    {
        builder.Host.UseWolverine(options =>
        {
            options.CodeGeneration.TypeLoadMode = JasperFx.CodeGeneration.TypeLoadMode.Static;
            options.Discovery.DisableConventionalDiscovery();
            options.Discovery.IncludeAssembly(typeof(Cauca.SurviRaoTransfer.Api.Program).Assembly);
            options.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;
            options.UseFluentValidation();
            options.AddExternalSystemEndpoints();
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerDocumentation<GeneralHeadersFilter>();
        builder.Services.AddWolverineHttp();

        var exportApp = builder.Build();
        exportApp.MapWolverineEndpoints();

        await using var stream = File.Create(openApiExportPath);
        var swaggerProvider = exportApp.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>();
        var swagger = swaggerProvider.GetSwagger("v1");
        await swagger.SerializeAsJsonAsync(stream, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
        return 0;
    }
```

> Note for the implementer: match the internal `Program.cs` imports/usings — `using Microsoft.OpenApi;`, `using Swashbuckle.AspNetCore.Swagger;`, and the JasperFx codegen usings — rather than the fully-qualified names above, if that reads cleaner alongside the existing file. Keep the build warning-free. If `AddExternalSystemEndpoints()` pulls auth services that fail to resolve during a minimal build, drop that single line — the client only needs the business endpoints; login is handled by `Cauca.ApiClient`, not by a generated method.

- [ ] **Step 2: Build the external API**

Run:
```bash
cd C:/dev/transfert-api/Cauca.SurviRaoTransfer
dotnet build Cauca.SurviRaoTransfer.Api/Cauca.SurviRaoTransfer.Api.csproj --configuration Release
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Verify the export runs and produces JSON**

Run:
```bash
cd C:/dev/transfert-api/Cauca.SurviRaoTransfer/Cauca.SurviRaoTransfer.Api
dotnet bin/Release/net10.0/Cauca.SurviRaoTransfer.Api.dll --export-openapi ../artifacts/external-api.openapi.json
```
Expected: exit 0; `Cauca.SurviRaoTransfer/artifacts/external-api.openapi.json` exists and contains `"openapi": "3.0..."` and the external business paths (e.g. `/api/Imports`). Keep this file for Task B7's URL verification.

- [ ] **Step 4: Commit**

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/Cauca.SurviRaoTransfer.Api/Program.cs
git commit -m "SRT: add --export-openapi entrypoint to external API"
```

---

### Task B2: OpenAPI export + change-detection scripts

**Files:**
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\scripts\generate-external-openapi.sh`
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\scripts\external-api-has-changed.sh`

**Interfaces:**
- Produces: `generate-external-openapi.sh [output] [project_dir]` and `external-api-has-changed.sh [current_openapi]`, mirroring the internal scripts but targeting `Cauca.SurviRaoTransfer.Api` and `artifacts/external-api.openapi.json`.

- [ ] **Step 1: Create `generate-external-openapi.sh`**

```bash
#!/usr/bin/env bash
set -euo pipefail

output_path="${1:-artifacts/external-api.openapi.json}"
project_dir="${2:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
timeout_seconds="${OPENAPI_EXPORT_TIMEOUT_SECONDS:-120}"
api_project_dir="$project_dir/Cauca.SurviRaoTransfer.Api"
api_dll="$api_project_dir/bin/Release/net10.0/Cauca.SurviRaoTransfer.Api.dll"

if [[ ! -f "$api_dll" ]]; then
    echo "External API assembly not found. Build it first: $api_dll" >&2
    exit 2
fi

mkdir -p "$(dirname "$output_path")"

(
    cd "$api_project_dir"
    dotnet "$api_dll" --export-openapi "$output_path"
) &
export_pid=$!

for ((elapsed = 0; elapsed < timeout_seconds; elapsed++)); do
    if ! kill -0 "$export_pid" >/dev/null 2>&1; then
        wait "$export_pid"
        echo "Generated external API OpenAPI document: $output_path"
        exit 0
    fi
    sleep 1
done

kill "$export_pid" >/dev/null 2>&1 || true
wait "$export_pid" >/dev/null 2>&1 || true
echo "Timed out after ${timeout_seconds}s while exporting the external API OpenAPI document." >&2
exit 1
```

- [ ] **Step 2: Create `external-api-has-changed.sh`**

```bash
#!/usr/bin/env bash
set -euo pipefail

current_openapi="${1:-artifacts/external-api.openapi.json}"

if [[ ! -f "$current_openapi" ]]; then
    echo "Current OpenAPI document not found: $current_openapi" >&2
    exit 2
fi

before_sha="${CI_COMMIT_BEFORE_SHA:-}"
if [[ -z "$before_sha" || "$before_sha" =~ ^0+$ ]]; then
    echo "No previous commit available; treating the external API contract as changed."
    exit 0
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/.." && pwd)"
root_dir="$(git -C "$project_dir" rev-parse --show-toplevel)"
project_relative_path="${project_dir#"$root_dir"/}"
previous_dir="$(mktemp -d "${TMPDIR:-/tmp}/survirao-previous.XXXXXX")"
previous_openapi="$(mktemp "${TMPDIR:-/tmp}/survirao-previous-openapi.XXXXXX.json")"

cleanup() {
    git -C "$root_dir" worktree remove --force "$previous_dir" >/dev/null 2>&1 || true
    rm -f "$previous_openapi"
}
trap cleanup EXIT

git -C "$root_dir" worktree add --detach "$previous_dir" "$before_sha" >/dev/null

if [[ "$project_relative_path" == "$project_dir" ]]; then
    previous_project_dir="$previous_dir"
else
    previous_project_dir="$previous_dir/$project_relative_path"
fi

dotnet restore "$previous_project_dir/Cauca.SurviRaoTransfer.slnx"
dotnet build "$previous_project_dir/Cauca.SurviRaoTransfer.Api/Cauca.SurviRaoTransfer.Api.csproj" --configuration Release --no-restore
"$script_dir/generate-external-openapi.sh" "$previous_openapi" "$previous_project_dir"

if cmp -s "$current_openapi" "$previous_openapi"; then
    echo "External API OpenAPI contract did not change."
    exit 1
fi

echo "External API OpenAPI contract changed."
exit 0
```

- [ ] **Step 3: Mark executable + smoke-test the generator**

Run:
```bash
cd C:/dev/transfert-api/Cauca.SurviRaoTransfer
git update-index --chmod=+x scripts/generate-external-openapi.sh scripts/external-api-has-changed.sh 2>/dev/null || chmod +x scripts/*.sh
bash scripts/generate-external-openapi.sh artifacts/external-api.openapi.json
```
Expected: `Generated external API OpenAPI document: artifacts/external-api.openapi.json` and the file exists. (Requires the Release build from Task B1.)

- [ ] **Step 4: Commit**

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/scripts/generate-external-openapi.sh Cauca.SurviRaoTransfer/scripts/external-api-has-changed.sh
git commit -m "SRT: add external API OpenAPI export and change-detection scripts"
```

---

### Task B3: NSwag config for the external client

**Files:**
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\nswag.external-client.json`

**Interfaces:**
- Produces: an NSwag configuration that generates `SurviRaoTransferExternalClient` (interface `ISurviRaoTransferExternalClient`) into `generated/Cauca.SurviRaoTransfer.ExternalClient/SurviRaoTransferExternalClient.cs` from `artifacts/external-api.openapi.json`, with `injectHttpClient: true`, `disposeHttpClient: false`, `generateBaseUrlProperty: true`.

- [ ] **Step 1: Create the config** (mirror `nswag.internal-client.json`, changing only the external-specific values)

```json
{
  "runtime": "Net100",
  "defaultVariables": "Version=0.0.0",
  "documentGenerator": {
    "fromDocument": {
      "url": "artifacts/external-api.openapi.json",
      "output": null
    }
  },
  "codeGenerators": {
    "openApiToCSharpClient": {
      "clientBaseClass": null,
      "configurationClass": null,
      "generateClientClasses": true,
      "generateClientInterfaces": true,
      "clientBaseInterface": null,
      "injectHttpClient": true,
      "disposeHttpClient": false,
      "protectedMethods": [],
      "generateExceptionClasses": true,
      "exceptionClass": "ApiException",
      "wrapDtoExceptions": true,
      "useHttpClientCreationMethod": false,
      "httpClientType": "System.Net.Http.HttpClient",
      "useHttpRequestMessageCreationMethod": false,
      "useBaseUrl": true,
      "generateBaseUrlProperty": true,
      "generateSyncMethods": false,
      "generatePrepareRequestAndProcessResponseAsAsyncMethods": false,
      "exposeJsonSerializerSettings": false,
      "clientClassAccessModifier": "public",
      "typeAccessModifier": "public",
      "generateContractsOutput": false,
      "className": "SurviRaoTransferExternalClient",
      "namespace": "Cauca.SurviRaoTransfer.ExternalClient",
      "requiredPropertiesMustBeDefined": true,
      "dateType": "System.DateOnly",
      "dateTimeType": "System.DateTimeOffset",
      "timeType": "System.TimeOnly",
      "timeSpanType": "System.TimeSpan",
      "arrayType": "System.Collections.Generic.ICollection",
      "arrayInstanceType": "System.Collections.ObjectModel.Collection",
      "dictionaryType": "System.Collections.Generic.IDictionary",
      "dictionaryInstanceType": "System.Collections.Generic.Dictionary",
      "arrayBaseType": "System.Collections.ObjectModel.Collection",
      "dictionaryBaseType": "System.Collections.Generic.Dictionary",
      "classStyle": "Poco",
      "jsonLibrary": "SystemTextJson",
      "generateDefaultValues": true,
      "generateDataAnnotations": true,
      "generateNullableReferenceTypes": true,
      "generateOptionalPropertiesAsNullable": true,
      "generateOptionalParameters": true,
      "handleReferences": false,
      "generateDtoTypes": true,
      "generateJsonMethods": false,
      "output": "generated/Cauca.SurviRaoTransfer.ExternalClient/SurviRaoTransferExternalClient.cs"
    }
  }
}
```

- [ ] **Step 2: Generate the client locally to verify the config**

Run:
```bash
cd C:/dev/transfert-api/Cauca.SurviRaoTransfer
dotnet tool restore
dotnet nswag run nswag.external-client.json /variables:Version=0.0.0
```
Expected: `generated/Cauca.SurviRaoTransfer.ExternalClient/SurviRaoTransferExternalClient.cs` is created and contains `public partial class SurviRaoTransferExternalClient` and `public partial interface ISurviRaoTransferExternalClient` with constructor `SurviRaoTransferExternalClient(System.Net.Http.HttpClient httpClient)` and a `BaseUrl` property.

- [ ] **Step 3: Commit** (config only — the generated `.cs` is produced by CI and should follow the internal client's gitignore convention; verify whether `generated/**/*.cs` is ignored and do NOT commit the generated file if so)

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/nswag.external-client.json
git commit -m "SRT: add NSwag config for external API client"
```

---

### Task B4: Generated client project (csproj + README) referencing `Cauca.ApiClient`

**Files:**
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\generated\Cauca.SurviRaoTransfer.ExternalClient\Cauca.SurviRaoTransfer.ExternalClient.csproj`
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\generated\Cauca.SurviRaoTransfer.ExternalClient\README.md`

**Interfaces:**
- Produces: a packable project that references `Cauca.ApiClient 4.1.0-beta3` and compiles both the generated `.cs` and the hand-written registration (Task B5).

- [ ] **Step 1: Create the csproj** (mirror the internal client csproj + the `Cauca.ApiClient` reference)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>Cauca.SurviRaoTransfer.ExternalClient</PackageId>
    <Title>SurviRao Transfer External API Client</Title>
    <Description>Generated .NET client for the SurviRao Transfer external API, with built-in ExternalSystem authentication.</Description>
    <Authors>CAUCA</Authors>
    <RepositoryUrl>https://gitlab.cauca.ca/cauca/survi-rao/transfert-api</RepositoryUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Cauca.ApiClient" Version="4.1.0-beta3" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the README**

```markdown
# SurviRao Transfer External API Client

Generated .NET client for the SurviRao Transfer external API.

Authentication (ExternalSystem API-key login + automatic token refresh) is built in.
Register it with your base URL and API key:

    services.AddSurviRaoTransferExternalClient("https://transfer.example.cauca.ca", apiKey);

Then inject `ISurviRaoTransferExternalClient`. Login and token refresh happen automatically.

The package is produced from the external API OpenAPI document during CI when the contract changes.
```

- [ ] **Step 3: Commit**

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/generated/Cauca.SurviRaoTransfer.ExternalClient/Cauca.SurviRaoTransfer.ExternalClient.csproj Cauca.SurviRaoTransfer/generated/Cauca.SurviRaoTransfer.ExternalClient/README.md
git commit -m "SRT: add external client project referencing Cauca.ApiClient"
```

---

### Task B5: `AddSurviRaoTransferExternalClient` registration (self-authenticating)

**Files:**
- Create: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\generated\Cauca.SurviRaoTransfer.ExternalClient\ExternalClientRegistration.cs`

**Interfaces:**
- Consumes: `AddCaucaExternalSystemAuth` (from `Cauca.ApiClient 4.1.0-beta3`), the generated `SurviRaoTransferExternalClient` / `ISurviRaoTransferExternalClient`.
- Produces: `public static IServiceCollection AddSurviRaoTransferExternalClient(this IServiceCollection services, string baseUrl, string apiKey, string apiPrefix = "api")`.

- [ ] **Step 1: Create the hand-written registration** (NSwag never overwrites this file — it only rewrites `SurviRaoTransferExternalClient.cs`)

```csharp
using System;
using System.Net.Http;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Cauca.SurviRaoTransfer.ExternalClient;

public static class ExternalClientRegistration
{
    public static IServiceCollection AddSurviRaoTransferExternalClient(
        this IServiceCollection services,
        string baseUrl,
        string apiKey,
        string apiPrefix = "api")
    {
        var configuration = new ExternalClientConfiguration
        {
            ApiBaseUrl = baseUrl,
            ApiBaseUrlForAuthentication = baseUrl,
            UserId = apiKey,
            UseExternalSystemLogin = true
        };

        const string httpClientName = "SurviRaoTransferExternalClient";
        services
            .AddHttpClient(httpClientName)
            .AddCaucaExternalSystemAuth(configuration, apiPrefix);

        services.AddTransient<ISurviRaoTransferExternalClient>(serviceProvider =>
        {
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName);
            return new SurviRaoTransferExternalClient(httpClient) { BaseUrl = baseUrl };
        });

        return services;
    }

    private sealed class ExternalClientConfiguration : IConfiguration
    {
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string ApiBaseUrlForAuthentication { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseExternalSystemLogin { get; set; } = true;
        public int RequestTimeoutInSeconds { get; set; } = 100;
    }
}
```

> Implementer note: confirm the generated `BaseUrl` property name and the interface/class names match the NSwag output from Task B3 (`SurviRaoTransferExternalClient`, `ISurviRaoTransferExternalClient`, `public string BaseUrl { get; set; }`). If NSwag emits a differently-cased `BaseUrl`, match it exactly.

- [ ] **Step 2: Build the generated project** (requires Task A4's `4.1.0-beta3` restorable, the generated `.cs` from B3 present, and B4's csproj)

Run:
```bash
cd C:/dev/transfert-api/Cauca.SurviRaoTransfer
dotnet nswag run nswag.external-client.json /variables:Version=0.0.0
dotnet build generated/Cauca.SurviRaoTransfer.ExternalClient/Cauca.SurviRaoTransfer.ExternalClient.csproj --configuration Release
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If `Cauca.ApiClient 4.1.0-beta3` is not yet on the feed, this step is blocked until Task A4 Step 4 completes.

- [ ] **Step 3: Commit**

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/generated/Cauca.SurviRaoTransfer.ExternalClient/ExternalClientRegistration.cs
git commit -m "SRT: add self-authenticating registration for external client"
```

---

### Task B6: CI publish job for the external client

**Files:**
- Modify: `C:\dev\transfert-api\.gitlab-ci.yml` (add `publish-external-api-client`, mirroring `publish-internal-api-client`)

**Interfaces:**
- Produces: a `deploy`-stage job that, on the default branch, builds the external API, exports its OpenAPI, skips when unchanged, generates + packs + pushes `Cauca.SurviRaoTransfer.ExternalClient`.

- [ ] **Step 1: Add the job** (copy `publish-internal-api-client`, swap internal→external tokens)

```yaml
publish-external-api-client:
    stage: deploy
    variables:
        GIT_DEPTH: "0"
        NUGET_AUTH_TOKEN: ${CI_JOB_TOKEN}
        ConnectionStrings__LegacyContext: "Host=localhost;Port=5432;Database=survirao_legacy;Username=postgres;Password=postgres"
        ConnectionStrings__ImportContext: "Host=localhost;Port=5432;Database=survirao_buildings;Username=postgres;Password=postgres"
    extends:
    - .exec-on-kubernetes
    needs: ["set-version"]
    image: registry.cauca.ca:443/cauca/cicd/test-dotnet:10
    rules:
    - if: '$CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH'
    script:
    - cd ${CI_PROJECT_DIR}/Cauca.SurviRaoTransfer/
    - dotnet tool restore
    - dotnet restore
    - dotnet build Cauca.SurviRaoTransfer.Api/Cauca.SurviRaoTransfer.Api.csproj --configuration Release --no-restore
    - scripts/generate-external-openapi.sh artifacts/external-api.openapi.json
    - scripts/external-api-has-changed.sh artifacts/external-api.openapi.json || status=$?
    - if [ "${status:-0}" = "1" ]; then exit 0; fi
    - if [ "${status:-0}" != "0" ]; then exit "$status"; fi
    - dotnet nswag run nswag.external-client.json /variables:Version=$VERSION
    - dotnet pack generated/Cauca.SurviRaoTransfer.ExternalClient/Cauca.SurviRaoTransfer.ExternalClient.csproj --configuration Release -p:PackageVersion=$VERSION
    - dotnet nuget push generated/Cauca.SurviRaoTransfer.ExternalClient/bin/Release/*.nupkg --source "$CI_API_V4_URL/projects/$CI_PROJECT_ID/packages/nuget/index.json" --api-key "$CI_JOB_TOKEN" --skip-duplicate
    artifacts:
        when: always
        expire_in: 1 week
        paths:
        - Cauca.SurviRaoTransfer/artifacts/external-api.openapi.json
        - Cauca.SurviRaoTransfer/generated/Cauca.SurviRaoTransfer.ExternalClient/bin/Release/*.nupkg
```

> Note: the external API's required connection strings (`LegacyContext`, and `ImportContext`/`CadContext`) differ from the internal API's. The `--export-openapi` path in Task B1 builds a minimal app that must not require a live DB; the `ConnectionStrings__*` variables above are a safety net matching the internal job's shape. Verify against `Cauca.SurviRaoTransfer.Api/Program.cs` connection-string requirements during Task B7 and trim/adjust to what the export path actually needs.

- [ ] **Step 2: Validate YAML**

Run: `cd C:/dev/transfert-api && git diff --stat .gitlab-ci.yml` and confirm the job block is well-formed (2-space indentation, valid keys). If `yamllint` is available: `yamllint .gitlab-ci.yml`.

- [ ] **Step 3: Commit**

```bash
cd C:/dev/transfert-api
git add .gitlab-ci.yml
git commit -m "SRT: publish external API client package in CI"
```

---

### Task B7: End-to-end verification (URLs + self-auth against the API test host)

**Files:**
- Reference (read): the exported `Cauca.SurviRaoTransfer/artifacts/external-api.openapi.json`
- Optional test: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\Cauca.SurviRaoTransfer.Client.IntegrationTests\` (existing project that already exercises ExternalSystem auth against the API test host)

**Interfaces:**
- Consumes: everything from B1–B6 plus the published `Cauca.ApiClient 4.1.0-beta3`.

- [ ] **Step 1: Verify URL composition from the OpenAPI doc**

Open `artifacts/external-api.openapi.json`. Inspect `servers[].url` and a sample `paths` key:
- If paths already include `/api/...` (e.g. `/api/Imports`) and the server URL is the host root, then in the registration `baseUrl` = host root and `apiPrefix = "api"` (default) is correct — login resolves to `{host}/api/Authentication/logonforexternalsystem`.
- If paths are `/Imports` (no `/api`) and `servers[].url` embeds `/api`, set `baseUrl` = host root **and** keep `apiPrefix = "api"` so login still resolves under `/api`; confirm the generated client's `BaseUrl` includes `/api` for business calls.
Document the correct `baseUrl`/`apiPrefix` combination in the README (Task B4) if it deviates from the default.

- [ ] **Step 2: (Recommended) Add an integration smoke test** using the real API test host

The `Cauca.SurviRaoTransfer.Client.IntegrationTests` project already spins up the external API (`DualAuthApiWebApplicationFactory` / `ApiWebApplicationFactory`) and authenticates an external system. Add a test that:
1. Registers the generated client: `services.AddSurviRaoTransferExternalClient(factory.BaseAddress.ToString(), knownApiKey)` (reuse the same test API key the existing `ImportClient` tests use).
2. Resolves `ISurviRaoTransferExternalClient` and calls a known GET endpoint.
3. Asserts a successful (authenticated) response and that no `Authorization`/login was set up by the test itself.

> This test can only compile once the generated `.cs` exists locally (run `dotnet nswag run nswag.external-client.json` first). Because the generated file is produced at CI time and is not committed, keep this test **guarded/optional** (e.g. in a separate, locally-run fixture) OR gate it behind CI ordering where generation precedes test compilation. If keeping it in-repo would break a clean checkout build, document the manual run steps in the test class remarks instead of committing a non-compiling test.

- [ ] **Step 3: Manual end-to-end smoke (if no automated integration test is added)**

With `4.1.0-beta3` restored and the client generated locally, in a scratch console app:
```csharp
var services = new ServiceCollection();
services.AddSurviRaoTransferExternalClient("https://<external-api-host>", "<test-api-key>");
using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ISurviRaoTransferExternalClient>();
var result = await client./* some GET */Async();
```
Expected: the call succeeds; a network trace shows a `POST /api/Authentication/logonforexternalsystem` before the business call, and the business call carries `Authorization: Bearer <token>`.

- [ ] **Step 4: Report results**

Report: OpenAPI URL shape found, the `baseUrl`/`apiPrefix` chosen, and the outcome of the integration/smoke verification (pass/fail with evidence). Do not mark Part B complete on optimistic assumptions — only on observed success.

---

### Task B8: Documentation touch-up

**Files:**
- Modify: `C:\dev\transfert-api\Cauca.SurviRaoTransfer\generated\Cauca.SurviRaoTransfer.ExternalClient\README.md` (if Task B7 revealed a non-default `baseUrl`/`apiPrefix`)
- Optionally modify: repo-level docs that list published packages, if such a list exists.

- [ ] **Step 1:** Reconcile the README usage snippet with the verified `baseUrl`/`apiPrefix` from Task B7.
- [ ] **Step 2: Commit**

```bash
cd C:/dev/transfert-api
git add Cauca.SurviRaoTransfer/generated/Cauca.SurviRaoTransfer.ExternalClient/README.md
git commit -m "SRT: document external client base URL and API key usage"
```

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-07-07-external-system-auth-for-nswag-client-design.md`):
- Reusable `Cauca.ApiClient` surface → Tasks A1–A3 (`CaucaExternalSystemAuthHandler` + `AddCaucaExternalSystemAuth`). **Refinement vs spec:** the plan reuses the existing internal `FluentRefreshTokenHandler` directly instead of extracting a new `ExternalSystemAuthenticator` — same login/refresh/re-login logic, lower risk, no change to existing secure clients. Behavior parity preserved.
- Self-authenticating generated client with one-line consumer registration → Tasks B3–B5 (`AddSurviRaoTransferExternalClient`).
- Behavior identical to `Cauca.ApiClient` → guaranteed by reusing `FluentRefreshTokenHandler` + `FluentResponseExtensions` (same header constants).
- Token lazy/in-memory/refresh-on-401, never persisted → Task A1/A2 (`AccessInformation` per handler instance, `SemaphoreSlim` single-flight).
- Error handling via existing exception semantics → inherited from `FluentRefreshTokenHandler` (login → `InvalidCredentialException`/`NoResponseApiException`/`InternalErrorApiException`); non-401 downstream responses pass through untouched (A2 returns `response` when not unauthorized).
- Testing (login, refresh+retry, re-login+retry, body preserved, correct paths, non-401 passthrough, no regressions) → A1/A2/A3 tests. Body-preserved is covered by `CloneAsync` + the POST-bearing extension test path; **added coverage note:** the A2 tests use GET; if the reviewer wants explicit body-preservation coverage, add a POST variant asserting `Requests[3].Body` equals the original — cheap to add in A2.
- **Scope change vs spec:** target is the **external** API client, not the internal one (per user direction). Prerequisite (external API implements ExternalSystem login) is **confirmed** in code (`Cause.SecurityManagement.Wolverine.ExternalSystem`).
- Prerequisite the spec flagged (`--export-openapi` availability) → the external API lacked it; Task B1 adds it.

**Placeholder scan:** no "TBD"/"handle errors appropriately"; every code step shows full code. Remaining judgement calls are explicitly flagged as verification steps (B1 `AddExternalSystemEndpoints` fallback, B7 URL shape, B6 connection-string trim), not silent gaps.

**Type consistency:** `CaucaExternalSystemAuthHandler(IConfiguration, string apiPrefix, Func<HttpClient>, IAsyncPolicy)` is used identically in A1/A2 tests and the A3 extension. `AddCaucaExternalSystemAuth(IHttpClientBuilder, IConfiguration, string)` matches its call in B5. Generated names `SurviRaoTransferExternalClient` / `ISurviRaoTransferExternalClient` / `BaseUrl` are consistent across B3/B4/B5, with an implementer note to confirm against actual NSwag output.
