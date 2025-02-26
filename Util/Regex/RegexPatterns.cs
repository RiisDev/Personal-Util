
// ReSharper disable UnusedMember.Global
// ReSharper disable UseRawString

using System.Text.RegularExpressions;

#pragma warning disable CA2211
#pragma warning disable SYSLIB1045
namespace Script.Util.Regex;

public static class RegexPatterns
{
    public static System.Text.RegularExpressions.Regex JavTitle = new(@"\b[A-Z0-9]{3,6}\-[A-Z0-9]{3,6}\b", RegexOptions.Compiled);
    public static System.Text.RegularExpressions.Regex SizeRegex = new(@"(\d+((\.|,)\d+)?)\s*(MB|GB|KB)<", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static System.Text.RegularExpressions.Regex GetReCaptchaType = new("([api2|enterprise]+)/anchor\\?(.*)", RegexOptions.Compiled);
    public static System.Text.RegularExpressions.Regex GetReCaptchaToken = new("\"recaptcha-token\" value=\"(.*?)\"", RegexOptions.Compiled);
    public static System.Text.RegularExpressions.Regex GetReCaptchaResponse = new("\"rresp\",\"(.*?)\"", RegexOptions.Compiled);
}