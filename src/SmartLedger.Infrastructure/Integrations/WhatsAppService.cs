using System.Diagnostics;
namespace SmartLedger.Infrastructure.Integrations;
public sealed class WhatsAppService
{
    public string BuildUrl(string phone,string template,IReadOnlyDictionary<string,string> values){var message=template;foreach(var p in values)message=message.Replace(p.Key,p.Value,StringComparison.Ordinal);var digits=new string(phone.Where(char.IsDigit).ToArray());return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";}
    public bool OpenAfterCommit(bool committed,string phone,string template,IReadOnlyDictionary<string,string> values){if(!committed)return false;Process.Start(new ProcessStartInfo(BuildUrl(phone,template,values)){UseShellExecute=true});return true;}
}
