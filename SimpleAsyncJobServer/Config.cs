using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleAsyncJobServer
{
    public class Config
    {
        /********************************************************
         * 
         * Plugin Settings
         * 
         *******************************************************/

        /// <summary>
        /// The path to look for connector plugins
        /// </summary>
        public string ConnectorPluginPath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location) ?? string.Empty, "plugins", "connectors");

        /// <summary>
        /// The path to look for jobplugins
        /// </summary>
        public string JobPluginPath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location) ?? string.Empty, "plugins", "jobs");

        /// <summary>
        /// The path to look for request handler plugins
        /// </summary>
        public string HandlerPluginPath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location) ?? string.Empty, "plugins", "handlers");

        /********************************************************
         * 
         * General Settings
         * 
         ********************************************************/
        /// <summary>
        /// The name of the Connector to use
        /// </summary>
        public string ConnectorType { get; set; } = "FileSystem";

        public Dictionary<string, string[]> RequestHandlers { get; set; } = new Dictionary<string, string[]>();

        public string LogPath { get; set; } = String.Empty;

        public MessageLevel LogLevel { get; set; } = MessageLevel.Information;

        /********************************************************
         * 
         * Runtime Settings
         * 
         ********************************************************/
        public int SleepDuration { get; set; } = 1000;

        /// <summary>
        /// How many jobs can be running accross all 
        /// </summary>
        public int MaximumJobCount { get; set; } = int.MaxValue;



        /// <summary>
        /// 
        /// </summary>
        /// <param name="configurationFilePath"></param>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <returns></returns>
        internal static Config Load(string configurationFilePath)
        {
            string configPath = configurationFilePath;
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentNullException(nameof(configurationFilePath));
            }


            Config? config = null;

            using (StreamReader streamReader = new StreamReader(configPath))
            {
                try
                {
                    JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
                    jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                    config = JsonSerializer.Deserialize<Config>(streamReader.ReadToEnd(), jsonSerializerOptions);
                }
                catch (JsonException)
                {
                    streamReader.Close(); // Make sure we close the file, so we don't block it
                    throw; // Forward the execption to the caller
                }
                streamReader.Close();
            }

            if (config == null) // if Deserialization didn't work, use default config. 
            {
                /* We may never get to this, since an error with deserialization may throw an exception */
                config = new Config();
            }

            return config;
        }

        internal void Save(string configurationFilePath)
        {
            string configPath = configurationFilePath;
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentNullException(nameof(configurationFilePath));
            }

            using (StreamWriter streamWriter = new StreamWriter(configPath))
            {
                streamWriter.Write(JsonSerializer.Serialize(this));
                streamWriter.Flush();
                streamWriter.Close();
            }

        }
    }
}
