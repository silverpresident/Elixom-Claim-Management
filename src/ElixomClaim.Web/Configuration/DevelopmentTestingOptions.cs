namespace ElixomClaim.Web.Configuration;

/// <summary>
/// Development-only conveniences. Program.cs additionally requires IHostEnvironment.IsDevelopment()
/// before either option is honoured, so deployment configuration cannot enable them.
/// </summary>
public sealed class DevelopmentTestingOptions
{
    public const string SectionName = "DevelopmentTesting";

    public bool Enabled { get; set; }

    public string DatabaseName { get; set; } = "ElixomClaim-Development";
}
