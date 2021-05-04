using System;
using System.Reflection;
using System.Runtime.Loader;

namespace SimpleAsyncJobServer
{
    /// <summary>
    /// Based on <see cref="https://docs.microsoft.com/de-de/dotnet/core/tutorials/creating-app-with-plugin-support#load-plugins"/>
    /// </summary>
    class PluginLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver resolver;

        public PluginLoadContext(string pluginPath)
        {
            string path = pluginPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(pluginPath));
            }

            this.resolver = new AssemblyDependencyResolver(path);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = this.resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = this.resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
