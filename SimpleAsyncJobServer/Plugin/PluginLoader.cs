using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SimpleAsyncJobServer
{
    public class PluginLoader
    {
        /// <summary>
        /// Loads the given Library and returns any Plugins, that match the Type of T
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <returns></returns>
        public static Assembly? ImportPlugin(string pluginPath)
        {
            string path = pluginPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(pluginPath));
            }

            if (!System.IO.File.Exists(path))
            { return null; }

            PluginLoadContext pluginLoadContext = new PluginLoadContext(path);

            return pluginLoadContext.LoadFromAssemblyName(new AssemblyName(System.IO.Path.GetFileNameWithoutExtension(path)));
        }

        public static List<Assembly> ImportPlugins(string pluginRoot)
        {
            string rootPath = pluginRoot;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentNullException(nameof(pluginRoot));
            }

            if (!System.IO.Directory.Exists(rootPath))
            {
                return new List<Assembly>();
            }


            List<Assembly> assemblies = new List<Assembly>();
            foreach (string plugindir in Directory.GetDirectories(rootPath))
            {
                // Get the DLLs in this directory
                string[] files = System.IO.Directory.GetFiles(plugindir, "*.dll");
                foreach (string file in files)
                {
                    Assembly? assembly = ImportPlugin(file);
                    if (assembly != null)
                    {
                        assemblies.Add(assembly);
                    }
                }
            }
            return assemblies;
        }

        public static List<T> CreatePluginInstance<T>(Assembly assembly)
        {
            List<T> plugins = new List<T>();
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(T).IsAssignableFrom(type))
                {
                    T plugin = (T)Activator.CreateInstance(type);
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
            }

            return plugins;
        }

        public static List<T> CreatePluginInstances<T>(List<Assembly> assemblies)
        {
            List<Assembly> assembliesList = assemblies;
            if (assembliesList == null || assembliesList.Count == 0)
            {
                return new List<T>();
            }

            List<T> plugins = new List<T>();
            foreach (Assembly assembly in assembliesList)
            {
                List<T> importedPlugins = CreatePluginInstance<T>(assembly);
                plugins.AddRange(importedPlugins);
            }
            return plugins;
        }

        public static List<Type> LoadPluginTypes<T>(Assembly assembly)
        {
            List<Type> plugins = new List<Type>();
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(T).IsAssignableFrom(type))
                {
                    plugins.Add(type);
                }
            }

            return plugins;
        }

        public static List<Type> LoadPluginTypes<T>(List<Assembly> assemblies)
        {
            List<Assembly> pluginAssemblies = assemblies;
            if (pluginAssemblies == null || !pluginAssemblies.Any())
            {
                return new List<Type>();
            }

            List<Type> plugins = new List<Type>();

            foreach (Assembly assembly in pluginAssemblies)
            {
                plugins.AddRange(LoadPluginTypes<T>(assembly));
            }

            return plugins;
        }

        public static List<T> LoadPlugin<T>(string filepath)
        {
            string path = filepath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(filepath));
            }

            Assembly? assembly = ImportPlugin(path);
            if (assembly != null)
            {
                return CreatePluginInstance<T>(assembly);
            }
            else
            {
                return new List<T>();
            }

        }

        public static List<T> LoadPlugins<T>(string pluginRoot)
        {
            string rootPath = pluginRoot;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentNullException(nameof(pluginRoot));
            }

            return CreatePluginInstances<T>(ImportPlugins(rootPath));
        }
    }
}
