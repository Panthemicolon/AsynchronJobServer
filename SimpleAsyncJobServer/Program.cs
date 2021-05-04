using JobServer.Connector;
using JobServer.Job;
using JobServer.RequestHandler;
using SimpleAsyncJobServer.RequestHandler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SimpleAsyncJobServer
{
    class Program
    {
        private const int ERR_SUCCESS = 0x00000000;
        private const int ERR_UNKNOWN = 0x7FFFFFFF; // Int32.MaxValue;

        // Configuration Errors 0x0000000X
        private const int ERR_CONFIGFILE_MISSING = 0x00000001;
        private const int ERR_CONFIG_INVALID = 0x00000002;

        // Connector Errors 0x000000X0
        private const int ERR_LOADING_CONNECTORS_FAILED = 0x00000010;
        private const int ERR_NO_CONNECTOR = 0x00000020;

        // JobHandler Errors = 0x00000X00
        private const int ERR_LOADING_HANDLERS_FAILED = 0x00000100;
        private const int ERR_NO_HANDLER = 0x00000200;

        // Job Errors = 0x0000X000
        private const int ERR_LOADING_JOBS_FAILED = 0x00001000;
        private const int ERR_NO_JOB = 0x00002000;

        private const string JOBSERVER_NAME = "JobServer";

        private static MessageLevel MaxLogLevel = MessageLevel.Warning;
        private static AsyncJobServer? JobServer { get; set; }

        private static List<TextWriter> LogTargets { get; set; } = new List<TextWriter>();
        private static string ConfigurationFilePath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location) ?? string.Empty, "config", "config.json");

        static int Main(string[] args)
        {
            // Register an Event that allows us to be notified, when the Process is terminated
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            // Also listen to the Console canceling
            Console.CancelKeyPress += Console_CancelKeyPress;
            LogTargets.Add(Console.Out);

            // Check Arguments if we got provided with a custom Configuration file, otherwise, use the default configuration file
            if (args.Length == 2 && args.First().ToLower() == "-config")
            {
                string confPath = args[1];
                ConfigurationFilePath = confPath;
            }
            // Read Configuration File
            Config config = new Config();
            if (File.Exists(ConfigurationFilePath))
            {
                try
                {
                    config = Config.Load(ConfigurationFilePath);
                }
                catch (JsonException je)
                {
                    // Log error
                    OnMessage(MessageLevel.Critical, "Could not load configuration file '" + ConfigurationFilePath + "': " + je.Message);
                    // Exit
                    Environment.Exit(ERR_CONFIG_INVALID);
                }
            }
            else
            {
                OnMessage(MessageLevel.Critical, "Could not find configuration file '" + ConfigurationFilePath);
                Environment.Exit(ERR_CONFIGFILE_MISSING);
            }

            if (!string.IsNullOrWhiteSpace(config.LogPath))
            {
                LogTargets.Add(new StreamWriter(Path.Combine(config.LogPath, "asyncjobserver.log"), true, System.Text.Encoding.UTF8));
            }
            MaxLogLevel = config.LogLevel;

            IConnector? connector = null;
            try
            {
                List<IConnector> connectors = PluginLoader.LoadPlugins<IConnector>(config.ConnectorPluginPath);
                // Depending on the Configuration, get the Connector Plugin
                connector = connectors.Where(con => (con.Name.ToLower() == config.ConnectorType.ToLower())).FirstOrDefault();

                // If we can't find a connector matching the configuration
                if (connector == null)
                {
                    // Write Error Entry to log
                    OnMessage(MessageLevel.Critical, "Error: Could not find Connector '" + config.ConnectorType + "'");
                    // Exit the Program
                    Environment.Exit(ERR_NO_CONNECTOR);
                }

                connectors.Clear();
            }
            catch (Exception ex)
            {
                OnMessage(MessageLevel.Critical, "Error Loading Connectors: " + ex.Message);
                Environment.Exit(ERR_LOADING_CONNECTORS_FAILED);
            }

            // Initialize the Job Server
            JobServer = new AsyncJobServer(connector);
            JobServer.Message += JobServer_Message;

            List<IJob>? jobs = null;
            try
            {
                // Get the available Plugins and register them to the Server
                jobs = PluginLoader.LoadPlugins<IJob>(config.JobPluginPath);
            }
            catch (Exception ex)
            {
                OnMessage(MessageLevel.Critical, "Error loading job plugins: " + ex.Message);
                Environment.Exit(ERR_LOADING_JOBS_FAILED);
            }

            if (jobs == null || jobs.Count == 0)
            {

                OnMessage(MessageLevel.Critical, "No job plugins available");
                Environment.Exit(ERR_NO_JOB);
            }

            // Get the JobHandler
            bool hasHandler = false;
            if (config.RequestHandlers == null || config.RequestHandlers.Count == 0)
            {
                IRequestHandler requestHandler = new InternalAsyncRequestHandler();
                foreach (IJob job in jobs)
                {
                    requestHandler.RegisterPlugin(job.Type, job.GetType());
                }
                JobServer.RegisterRequestHandler(requestHandler);
                hasHandler = true;
            }
            else
            {
                try
                {
                    List<IRequestHandler> requestHandlerPlugins = PluginLoader.LoadPlugins<IRequestHandler>(config.HandlerPluginPath);

                    foreach (string handler in config.RequestHandlers.Keys)
                    {
                        string handlername = handler.ToLower().Trim();
                        IRequestHandler? requestHandler = null;
                        if (string.IsNullOrWhiteSpace(handlername) || handlername == "default")
                        {
                            requestHandler = new InternalAsyncRequestHandler();
                        }
                        else
                        {
                            IRequestHandler rHandler = requestHandlerPlugins.Where((rh) => { return rh.Name.ToLower().Trim() == handlername; }).FirstOrDefault();
                            if (rHandler == null)
                            {
                                OnMessage(MessageLevel.Error, $"Could not find plugin for request handler {handler}");
                                continue;
                            }
                            else
                            {
                                // Create a new Instance of the JobHandler, so in case we have multible of the same type with different Jobs
                                requestHandler = (IRequestHandler?)Activator.CreateInstance(rHandler.GetType());
                            }
                        }

                        if (requestHandler == null)
                        {
                            OnMessage(MessageLevel.Error, $"Could not find plugin for request handler {handler}");
                            continue;
                        }

                        foreach (string plugin in config.RequestHandlers[handler])
                        {
                            IJob? job = jobs.Where((j) => { return j.Type.Trim().ToLower() == plugin.ToLower().Trim(); }).FirstOrDefault();
                            if (job == null)
                            {
                                OnMessage(MessageLevel.Warning, $"Could not find job plugin {plugin} for request handler {handler}");
                                continue;
                            }

                            requestHandler.RegisterPlugin(job.Type, job.GetType());
                        }

                        JobServer.RegisterRequestHandler(requestHandler);
                        hasHandler = true;
                    }
                    requestHandlerPlugins.Clear();
                }
                catch (ArgumentNullException anex)
                {
                    OnMessage(MessageLevel.Critical, "Error Loading JobHandler: " + anex.Message);
                    Environment.Exit(ERR_LOADING_HANDLERS_FAILED);
                }
            }

            if (!hasHandler)
            {
                OnMessage(MessageLevel.Critical, "Error: Could not load any of the configured request handlers");
                Environment.Exit(ERR_NO_HANDLER);
            }

            // Add the Dummy handler after all others to Handle Requests our other RequestHandlers don't support
            JobServer.RegisterRequestHandler(new DummyAsyncRequestHandler());


            // Start the Server
            JobServer.Run(config);

            if (!string.IsNullOrWhiteSpace(config.LogPath))
            {
                foreach (TextWriter tw in LogTargets)
                {
                    tw.Close();
                }
            }
            // Gracefully Exit
            return ERR_SUCCESS;
        }

        private static void OnMessage(MessageLevel level, string message)
        {
            if (level > MaxLogLevel)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            Log($"{DateTime.UtcNow} [{level}]: {message}");
        }

        private static void Log(string message)
        {
            string? value = message;
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            foreach (TextWriter textWriter in LogTargets)
            {
                WriteLogMessage(textWriter, value);
            }
        }

        private static void WriteLogMessage(TextWriter textWriter, string message)
        {
            TextWriter writer = textWriter;
            if (writer == null)
            {
                return;
            }

            string? value = message;
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
                        
            writer.WriteLine(value);
            writer.Flush();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // If the application exits, notify the server to stop and do it's cleanup
            // Set the Cancel property to true to prevent the process from terminating.
            e.Cancel = true;
            if (JobServer != null)
            {
                JobServer.Stop();
            }
        }

        private static void JobServer_Message(object? sender, MessageLevel level, string message)
        {          
            OnMessage(level, message);
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            // If the application exits, notify the server to stop and do it's cleanup
            if (JobServer != null)
            {
                JobServer.Stop();
            }
        }

        // TODO: Add Logging
    }
}