using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorshipPptx;

public static class UploadLogText
{
    public static string Format(string message,DateTimeOffset receivedAt,TimeZoneInfo timeZone)
    {
        return string.Join("\n",(message ?? "").Replace("\r","").Split('\n').Select(line=> {
            var time=receivedAt;
            // Server logs already carry UTC event times. Keep the event time, not a second receipt timestamp.
            var prefix=Regex.Match(line,@"^\[(\d{4}-\d{2}-\d{2}T[^\]]+)\]\s*");
            if(prefix.Success && DateTimeOffset.TryParse(prefix.Groups[1].Value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var serverTime)) {
                time=serverTime; line=line.Substring(prefix.Length);
            }
            return "["+TimeZoneInfo.ConvertTime(time,timeZone).ToString("MM/dd HH:mm:ss",CultureInfo.InvariantCulture)+"] "+line;
        }));
    }
}
