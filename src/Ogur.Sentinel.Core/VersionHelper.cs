using System.Reflection;
using Ogur.Sentinel.Abstractions;

namespace Ogur.Sentinel.Core;

public class VersionHelper : IVersionHelper
{
    public string GetVersion(Assembly assembly)
    {
        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
    
    public string GetShortVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";
    }
    
    public string GetBuildTime(Assembly assembly)
    {
        try
        {
            var attrs = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var buildTime = attrs.FirstOrDefault(a => a.Key == "BuildTime");
        
            if (buildTime?.Value != null)
            {
                // Format: yyyyMMdd-HHmmss
                var value = buildTime.Value;
                if (value.Length == 15 && value.Contains('-'))
                {
                    var parts = value.Split('-');
                    var date = parts[0]; // yyyyMMdd
                    var time = parts[1]; // HHmmss
                
                    return $"{date.Substring(0,4)}-{date.Substring(4,2)}-{date.Substring(6,2)} {time.Substring(0,2)}:{time.Substring(2,2)}:{time.Substring(4,2)}";
                }
                return buildTime.Value;
            }
        
            // Fallback: DLL time
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return new FileInfo(location).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch
        {
            // ignore
        }
    
        return "unknown";
    }
}