namespace SmartLedger.Core.Services;

public static class RecoveryPasswordPolicy
{
    public static string? Validate(string newPassword, string confirmation)
    {
        if (newPassword.Length < 8) return "كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل.";
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal)) return "تأكيد كلمة المرور غير مطابق.";
        return null;
    }
}
