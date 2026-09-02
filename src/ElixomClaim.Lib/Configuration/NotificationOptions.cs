using System.ComponentModel.DataAnnotations;

namespace ElixomClaim.Lib.Configuration;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    [Required(ErrorMessage = "Notification Provider is required.")]
    [RegularExpression("^(Smtp|Acs|Disabled)$", ErrorMessage = "Provider must be 'Smtp', 'Acs', or 'Disabled'.")]
    public string Provider { get; set; } = "Disabled";

    [Required(ErrorMessage = "FromAddress is required.")]
    [EmailAddress(ErrorMessage = "FromAddress must be a valid email address.")]
    public string FromAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "SystemCopyAddress is required.")]
    [EmailAddress(ErrorMessage = "SystemCopyAddress must be a valid email address.")]
    public string SystemCopyAddress { get; set; } = string.Empty;

    public string ToRedactedString()
    {
        return $"Provider: '{Provider}', FromAddress: '{FromAddress}', SystemCopyAddress: '{SystemCopyAddress}'";
    }
}
