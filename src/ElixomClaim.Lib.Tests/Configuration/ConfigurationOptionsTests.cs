using System.ComponentModel.DataAnnotations;
using ElixomClaim.Lib.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Lib.Tests.Configuration;

public class ConfigurationOptionsTests
{
    [Fact]
    public void DatabaseOptions_ToRedactedString_MasksPasswordAndCredentials()
    {
        var options = new DatabaseOptions
        {
            ClaimDatabase = "Server=tcp:sql.example.com;Database=ElixomClaimDb;User ID=adminUser;Password=SuperSecret123;Encrypt=True;"
        };

        var redacted = options.ToRedactedString();

        Assert.DoesNotContain("SuperSecret123", redacted);
        Assert.DoesNotContain("adminUser", redacted);
        Assert.Contains("Password=***REDACTED***", redacted);
        Assert.Contains("User ID=***REDACTED***", redacted);
        Assert.Contains("Server=tcp:sql.example.com", redacted);
    }

    [Fact]
    public void GoogleAuthOptions_ToRedactedString_MasksClientSecret()
    {
        var options = new GoogleAuthOptions
        {
            ClientId = "test-client-id.apps.googleusercontent.com",
            ClientSecret = "secret-key-12345"
        };

        var redacted = options.ToRedactedString();

        Assert.Contains("test-client-id.apps.googleusercontent.com", redacted);
        Assert.DoesNotContain("secret-key-12345", redacted);
        Assert.Contains("***REDACTED***", redacted);
    }

    [Fact]
    public void NotificationOptions_ToRedactedString_ContainsExpectedDetails()
    {
        var options = new NotificationOptions
        {
            Provider = "Smtp",
            FromAddress = "no-reply@elixom.com",
            SystemCopyAddress = "ops@elixom.com"
        };

        var redacted = options.ToRedactedString();

        Assert.Contains("Smtp", redacted);
        Assert.Contains("no-reply@elixom.com", redacted);
        Assert.Contains("ops@elixom.com", redacted);
    }

    [Fact]
    public void OAuthOptions_ToRedactedString_ContainsLifetimes()
    {
        var options = new OAuthOptions
        {
            Issuer = "ElixomClaim.OAuth",
            AccessTokenLifetimeSeconds = 1800,
            RefreshTokenLifetimeSeconds = 86400
        };

        var redacted = options.ToRedactedString();

        Assert.Contains("ElixomClaim.OAuth", redacted);
        Assert.Contains("1800", redacted);
        Assert.Contains("86400", redacted);
    }

    [Fact]
    public void Options_ValidationFails_WhenRequiredFieldsAreMissing()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:ClaimDatabase", "" },
            { "Authentication:DefaultAdminEmail", "not-an-email" },
            { "Authentication:Google:ClientId", "" },
            { "Authentication:Google:ClientSecret", "" },
            { "Notifications:Provider", "InvalidProvider" },
            { "Notifications:FromAddress", "invalid-email" },
            { "Notifications:SystemCopyAddress", "invalid-email" },
            { "OAuth:Issuer", "" },
            { "OAuth:AccessTokenLifetimeSeconds", "5" } // Below 60 min
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddClaimLibraryServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value);

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value);

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<GoogleAuthOptions>>().Value);

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value);

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<OAuthOptions>>().Value);
    }

    [Fact]
    public void Options_ValidationSucceeds_WhenValidConfigurationIsProvided()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:ClaimDatabase", "Server=localhost;Database=TestDb;" },
            { "Authentication:DefaultAdminEmail", "admin@elixom.com" },
            { "Authentication:Google:ClientId", "valid-client-id" },
            { "Authentication:Google:ClientSecret", "valid-client-secret" },
            { "Notifications:Provider", "Acs" },
            { "Notifications:FromAddress", "no-reply@elixom.com" },
            { "Notifications:SystemCopyAddress", "ops@elixom.com" },
            { "OAuth:Issuer", "ElixomClaim.OAuth" },
            { "OAuth:AccessTokenLifetimeSeconds", "3600" },
            { "OAuth:RefreshTokenLifetimeSeconds", "1209600" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddClaimLibraryServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var authOptions = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var googleOptions = serviceProvider.GetRequiredService<IOptions<GoogleAuthOptions>>().Value;
        var notificationOptions = serviceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value;
        var oauthOptions = serviceProvider.GetRequiredService<IOptions<OAuthOptions>>().Value;

        Assert.Equal("Server=localhost;Database=TestDb;", dbOptions.ClaimDatabase);
        Assert.Equal("admin@elixom.com", authOptions.DefaultAdminEmail);
        Assert.Equal("valid-client-id", googleOptions.ClientId);
        Assert.Equal("Acs", notificationOptions.Provider);
        Assert.Equal("ElixomClaim.OAuth", oauthOptions.Issuer);
    }
}
