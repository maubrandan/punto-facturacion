using System.Net;
using System.Net.Http.Json;
using POS.Application.Contracts.Platform;
using POS.Domain.Platform;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformOperatorsIntegrationTests
{
    [Fact]
    public async Task Operators_RequiresSuperAdmin()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var opsClient = factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        // Default platform role in tests is Operations.
        var forbidden = await opsClient.GetAsync("/api/platform/operators?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var superClient = CreateSuperAdminClient(factory);
        var ok = await superClient.GetAsync("/api/platform/operators?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Provision_List_Update_Block_Unblock_AndLoginBlocked()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var superClient = CreateSuperAdminClient(factory);

        var email = $"ops-{Guid.NewGuid():N}@test.local";
        var createRes = await superClient.PostAsJsonAsync(
            "/api/platform/operators",
            new ProvisionPlatformOperatorApiRequest
            {
                Email = email,
                Password = "Pass123!",
                FullName = "Operador Support",
                PlatformRole = PlatformRoleNames.Support
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(PlatformRoleNames.Support, created!.Data!.PlatformRole);
        var userId = created.Data.Id;

        var listRes = await superClient.GetAsync($"/api/platform/operators?emailContains={Uri.EscapeDataString(email)}");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorDirectoryPageDto>>();
        Assert.Contains(list!.Data!.Items, x => x.Id == userId);

        var updateRes = await superClient.PatchAsJsonAsync(
            $"/api/platform/operators/{userId}",
            new UpdatePlatformOperatorApiRequest
            {
                FullName = "Operador Ops",
                PlatformRole = PlatformRoleNames.Operations
            });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.Equal(PlatformRoleNames.Operations, updated!.Data!.PlatformRole);
        Assert.Equal("Operador Ops", updated.Data.FullName);

        using var loginClient = factory.CreateClient();
        var loginOk = await loginClient.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { email, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.OK, loginOk.StatusCode);

        var blockRes = await superClient.PostAsJsonAsync(
            $"/api/platform/operators/{userId}/block",
            new PlatformUserActionRequest { Justification = "Bloqueo de prueba operadores" });
        Assert.Equal(HttpStatusCode.OK, blockRes.StatusCode);

        var loginBlocked = await loginClient.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { email, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.Forbidden, loginBlocked.StatusCode);

        var unblockRes = await superClient.PostAsJsonAsync(
            $"/api/platform/operators/{userId}/unblock",
            new PlatformUserActionRequest { Justification = "Desbloqueo de prueba operadores" });
        Assert.Equal(HttpStatusCode.OK, unblockRes.StatusCode);

        var loginAgain = await loginClient.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { email, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.OK, loginAgain.StatusCode);
    }

    [Fact]
    public async Task SelfBlock_And_LastSuperAdmin_AreRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var superClient = CreateSuperAdminClient(factory);

        var email = $"sa-{Guid.NewGuid():N}@test.local";
        var createRes = await superClient.PostAsJsonAsync(
            "/api/platform/operators",
            new ProvisionPlatformOperatorApiRequest
            {
                Email = email,
                Password = "Pass123!",
                FullName = "Solo SuperAdmin",
                PlatformRole = PlatformRoleNames.SuperAdmin
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        var userId = created!.Data!.Id;

        using var asSelf = factory.CreateClient();
        asSelf.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        asSelf.DefaultRequestHeaders.Add("X-Test-PlatformRole", PlatformRoleNames.SuperAdmin);
        asSelf.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var selfBlock = await asSelf.PostAsJsonAsync(
            $"/api/platform/operators/{userId}/block",
            new PlatformUserActionRequest { Justification = "Intento de auto-bloqueo" });
        Assert.Equal(HttpStatusCode.BadRequest, selfBlock.StatusCode);
        var selfBody = await selfBlock.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.Equal("platform.operators.self_block", selfBody?.Error?.Code);

        var selfRole = await asSelf.PatchAsJsonAsync(
            $"/api/platform/operators/{userId}",
            new UpdatePlatformOperatorApiRequest
            {
                FullName = "Solo SuperAdmin",
                PlatformRole = PlatformRoleNames.Operations
            });
        Assert.Equal(HttpStatusCode.BadRequest, selfRole.StatusCode);
        var selfRoleBody = await selfRole.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.Equal("platform.operators.self_role_change", selfRoleBody?.Error?.Code);

        // Actor distinto (header userId distinto) intenta bloquear al único SuperAdmin activo.
        var lastBlock = await superClient.PostAsJsonAsync(
            $"/api/platform/operators/{userId}/block",
            new PlatformUserActionRequest { Justification = "Bloqueo del último SuperAdmin" });
        Assert.Equal(HttpStatusCode.BadRequest, lastBlock.StatusCode);
        var lastBody = await lastBlock.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.Equal("platform.operators.last_super_admin", lastBody?.Error?.Code);

        var lastDemote = await superClient.PatchAsJsonAsync(
            $"/api/platform/operators/{userId}",
            new UpdatePlatformOperatorApiRequest
            {
                FullName = "Solo SuperAdmin",
                PlatformRole = PlatformRoleNames.Operations
            });
        Assert.Equal(HttpStatusCode.BadRequest, lastDemote.StatusCode);
        var demoteBody = await lastDemote.Content.ReadFromJsonAsync<ApiResponse<PlatformOperatorSummaryDto>>();
        Assert.Equal("platform.operators.last_super_admin", demoteBody?.Error?.Code);
    }

    private static HttpClient CreateSuperAdminClient(TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        client.DefaultRequestHeaders.Add("X-Test-PlatformRole", PlatformRoleNames.SuperAdmin);
        client.DefaultRequestHeaders.Add("X-Test-UserId", "test-super-admin-actor");
        return client;
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }

        public ApiError? Error { get; init; }
    }

    private sealed class ApiError
    {
        public string? Code { get; init; }

        public string? Message { get; init; }
    }
}
