using System.Security.Cryptography;
using System.Text;
namespace SmartLedger.Infrastructure.Security;
public sealed class HardwareIdentifierService
{
    public static string GetHardwareId(){var raw=$"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.OSVersion.VersionString}";return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));}
    public bool Matches(string expected)=>CryptographicOperations.FixedTimeEquals(Convert.FromHexString(GetHardwareId()),Convert.FromHexString(expected));
}

