using System; 
using System.Reflection; 

namespace Ogur.Sentinel.Abstractions;

public interface IVersionHelper
{ 
    string GetVersion(Assembly assembly);
    string GetShortVersion(Assembly assembly);
    string GetBuildTime(Assembly assembly);
}