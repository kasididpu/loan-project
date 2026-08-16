namespace LoanProject.Api.Security;

/// <summary>
/// Masks sensitive values for API responses and logs: all but the last four
/// characters become bullets. Keeping the tail lets support confirm "the account
/// ending 5678" without ever exposing the full number. Decryption still happens
/// upstream — masking is a display concern, applied after the value is read back.
/// </summary>
public static class SensitiveDataMasker
{
    private const int VisibleTail = 4;

    public static string? MaskTail(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= VisibleTail
            ? new string('•', value.Length)
            : new string('•', value.Length - VisibleTail) + value[^VisibleTail..];
    }
}
