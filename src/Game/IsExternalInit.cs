// Compatibility shim for C# init-only properties when targeting .NET Framework 4.8.
// The type is supplied by newer runtimes but is only a compiler marker.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
