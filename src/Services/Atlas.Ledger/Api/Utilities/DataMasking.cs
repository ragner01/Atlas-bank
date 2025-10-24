using System.Text.RegularExpressions;

namespace Atlas.Ledger.Api.Utilities;

public static class DataMasking
{
    private static readonly Regex AccountIdPattern = new(@"acc_[a-zA-Z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex TenantIdPattern = new(@"tnt_[a-zA-Z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(@"""amount"":\s*\d+", RegexOptions.Compiled);
    private static readonly Regex MinorPattern = new(@"""minor"":\s*\d+", RegexOptions.Compiled);

    public static string MaskSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var masked = input;
        
        // Mask account IDs
        masked = AccountIdPattern.Replace(masked, "acc_***");
        
        // Mask tenant IDs
        masked = TenantIdPattern.Replace(masked, "tnt_***");
        
        // Mask amounts (keep structure but hide values)
        masked = AmountPattern.Replace(masked, "\"amount\": ***");
        masked = MinorPattern.Replace(masked, "\"minor\": ***");
        
        return masked;
    }

    public static string MaskAccountId(string accountId)
    {
        if (string.IsNullOrEmpty(accountId) || accountId.Length < 8)
            return "***";
            
        return accountId[..4] + "***" + accountId[^4..];
    }

    public static string MaskTenantId(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId) || tenantId.Length < 8)
            return "***";
            
        return tenantId[..4] + "***" + tenantId[^4..];
    }
}
