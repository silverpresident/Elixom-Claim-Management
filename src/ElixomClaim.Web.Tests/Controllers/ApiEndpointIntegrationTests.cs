using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Json;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers.Api;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Web.Tests.Controllers;

public class ApiEndpointIntegrationTests
{
    [Fact]
    public async Task ClaimsApi_RequiresApiScope_AndReturnsOwnedClaims()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        using var host = await CreateHostAsync(databaseName);
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "api-user@example.test", NormalizedEmail = "API-USER@EXAMPLE.TEST", FullName = "API User", Role = UserRole.User, IsActive = true });
            db.Claims.Add(new ElixomClaim.Lib.Entities.Claim { ClaimantUserId = userId, Title = "Owned", Description = "Owned claim", Amount = 10m, Status = ClaimStatus.Draft, PaymentStatus = ClaimPaymentStatus.Unpaid, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/claims")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Scope", "mcp:access");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/claims")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Scope");
        client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var response = await client.GetAsync("/api/v1/claims?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Owned", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClaimsApi_Create_RequiresAndPersistsIdempotencyKey()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "create-api@example.test", NormalizedEmail = "CREATE-API@EXAMPLE.TEST", FullName = "Create API", Role = UserRole.User, IsActive = true });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var body = JsonContent.Create(new { title = "Idempotent", description = "Created once", amount = 10m, dateOfJob = (DateTime?)null });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/claims", body)).StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "claim-create-1");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/v1/claims", JsonContent.Create(new { title = "Idempotent", description = "Created once", amount = 10m, dateOfJob = (DateTime?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/claims", JsonContent.Create(new { title = "Idempotent", description = "Created once", amount = 10m, dateOfJob = (DateTime?)null }))).StatusCode);
        using var verifyScope = host.Services.CreateScope();
        Assert.Single(await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Claims.ToListAsync());
    }

    [Theory]
    [InlineData("/api/v1/claims?page=0&pageSize=25")]
    [InlineData("/api/v1/claims?page=1&pageSize=101")]
    public async Task ClaimsApi_RejectsOutOfRangePagination(string path)
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "pagination@example.test", NormalizedEmail = "PAGINATION@EXAMPLE.TEST", FullName = "Pagination", Role = UserRole.User, IsActive = true });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CollectionsApi_EnforcesTellerRoleAfterApiScope()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "not-teller@example.test", NormalizedEmail = "NOT-TELLER@EXAMPLE.TEST", FullName = "Not Teller", Role = UserRole.User, IsActive = true });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/collections")).StatusCode);
    }

    [Fact]
    public async Task JobPaymentsApi_RestrictsUserToOwnedPayments()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(
                new User { Id = ownerId, Email = "payment-owner@example.test", NormalizedEmail = "PAYMENT-OWNER@EXAMPLE.TEST", FullName = "Payment Owner", Role = UserRole.User, IsActive = true },
                new User { Id = otherId, Email = "other-user@example.test", NormalizedEmail = "OTHER-USER@EXAMPLE.TEST", FullName = "Other User", Role = UserRole.User, IsActive = true });
            db.JobPayments.Add(new JobPayment { Id = jobId, PayeeUserId = ownerId, Title = "Owned payment", JobTotal = 42m, TotalPaid = 42m, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", otherId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/job-payments/{jobId}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", ownerId.ToString());
        var response = await client.GetAsync($"/api/v1/job-payments/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"totalPaid\":42", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmailTemplatesApi_RejectsUnapprovedTemplateBeforeAnyQueueing()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "template-user@example.test", NormalizedEmail = "TEMPLATE-USER@EXAMPLE.TEST", FullName = "Template User", Role = UserRole.Administrator, IsActive = true });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var response = await client.PostAsync("/api/v1/email-templates/queue", JsonContent.Create(new { templateType = "FreeForm", entityId = Guid.NewGuid(), idempotencyKey = "not-allowed" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var verifyScope = host.Services.CreateScope();
        Assert.Empty(await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailOutboxItems.ToListAsync());
    }

    [Fact]
    public async Task EmailTemplatesApi_PreviewsOnlyApprovedRedactedCollectionData()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var tellerId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var collectionClient = new CollectionClient { Id = Guid.NewGuid(), Name = "Preview Client" };
            db.Users.Add(new User { Id = tellerId, Email = "teller@example.test", NormalizedEmail = "TELLER@EXAMPLE.TEST", FullName = "Teller", Role = UserRole.Teller, IsActive = true });
            db.CollectionClients.Add(collectionClient);
            db.CollectionTransactions.Add(new CollectionTransaction
            {
                Id = collectionId, CollectionClientId = collectionClient.Id, TellerUserId = tellerId,
                PayorName = "Private Payor", PayorEmail = "private.payor@example.test", PayorTelephone = "876-555-0100",
                Purpose = "Collection", Amount = 20m, PaymentDateUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", tellerId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var response = await client.PostAsync("/api/v1/email-templates/preview", JsonContent.Create(new { templateType = "CollectionReceipt", entityId = collectionId }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("p***********r@example.test", json);
        Assert.DoesNotContain("private.payor@example.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("876-555-0100", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Payor", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyMcpControllerRoute_IsNotMappedAsAnApiFallback()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/mcp/claims")).StatusCode);
    }

    [Fact]
    public async Task PayrollApi_RunRequiresIdempotencyKey()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "accountant@example.test", NormalizedEmail = "ACCOUNTANT@EXAMPLE.TEST", FullName = "Accountant", Role = UserRole.Accountant, IsActive = true });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var response = await client.PostAsync("/api/v1/payroll/run", JsonContent.Create(new { salaryDefinitionId = Guid.NewGuid(), asOfDate = new DateOnly(2026, 9, 8) }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OperationsApi_RestrictsStatusToRequestingActor()
    {
        using var host = await CreateHostAsync(Guid.NewGuid().ToString("N"));
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        const string operationKey = "outbox-wakeup:owner:status-test";
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(
                new User { Id = ownerId, Email = "operation-owner@example.test", NormalizedEmail = "OPERATION-OWNER@EXAMPLE.TEST", FullName = "Operation Owner", Role = UserRole.Administrator, IsActive = true },
                new User { Id = otherId, Email = "operation-other@example.test", NormalizedEmail = "OPERATION-OTHER@EXAMPLE.TEST", FullName = "Operation Other", Role = UserRole.Administrator, IsActive = true });
            db.OperationRecords.Add(new OperationRecord { IdempotencyKey = operationKey, OperationType = "OutboxWakeUp", ActorUserId = ownerId.ToString(), Status = "Completed", Details = "Accepted", ExecutedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", otherId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/operations/{operationKey}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", ownerId.ToString());
        var response = await client.GetAsync($"/api/v1/operations/{operationKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("OutboxWakeUp", await response.Content.ReadAsStringAsync());
    }

    private static async Task<IHost> CreateHostAsync(string databaseName) => await new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer().ConfigureServices(services =>
    {
        services.AddLogging(); services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.Configure<ElixomClaim.Lib.Configuration.NotificationOptions>(options => options.SystemCopyAddress = "ops@example.test");
        services.AddSingleton<ElixomClaim.Lib.Services.ISystemClock, ElixomClaim.Lib.Services.SystemClock>(); services.AddSingleton<ISalaryRecurrencePlanner, SalaryRecurrencePlanner>(); services.AddScoped<IAuditService, AuditService>(); services.AddScoped<IClaimService, ClaimService>(); services.AddScoped<ICollectionService, CollectionService>(); services.AddScoped<IJobPaymentService, JobPaymentService>(); services.AddScoped<ISalaryPayrollService, SalaryPayrollService>(); services.AddScoped<IApprovedEmailPreviewService, ApprovedEmailPreviewService>(); services.AddScoped<IOperationRecordService, OperationRecordService>(); services.AddScoped<IApprovedOperationService, ApprovedOperationService>(); services.AddScoped<IActorResolver, ActorResolver>(); services.AddHttpContextAccessor();
        services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        services.AddAuthorization(options => options.AddPolicy("ApiAccess", policy => { policy.AddAuthenticationSchemes("Test"); policy.RequireAuthenticatedUser(); policy.RequireAssertion(context => context.User.HasClaim("scope", "api:access")); }));
        services.AddControllers().AddApplicationPart(typeof(ClaimsApiController).Assembly);
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).StartAsync();

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id = Request.Headers["X-Test-User"].FirstOrDefault(); if (!Guid.TryParse(id, out _)) return Task.FromResult(AuthenticateResult.NoResult());
            var scope = Request.Headers["X-Test-Scope"].FirstOrDefault() ?? string.Empty;
            var identity = new ClaimsIdentity([new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, id!), new System.Security.Claims.Claim("scope", scope)], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
