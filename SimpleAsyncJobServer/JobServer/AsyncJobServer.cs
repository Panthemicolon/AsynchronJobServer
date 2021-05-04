using JobServer.Connector;
using JobServer.Request;
using JobServer.RequestHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleAsyncJobServer
{
    public class AsyncJobServer
    {
        private bool IsRunning { get; set; }

        private IConnector Connector { get; set; }

        private List<IRequestHandler> JobHandlers { get; set; }

        private CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Notification from the JobServer
        /// </summary>
        public event Action<object, MessageLevel, string>? Message;

        public AsyncJobServer(IConnector connector)
        {
            this.Connector = connector;
            if (this.Connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }
                       
            this.JobHandlers = new List<IRequestHandler>();
            this.CancellationTokenSource = new CancellationTokenSource();
        }

        /**************************************************************
         * 
         * Server Logic
         * 
         *************************************************************/

        public void RegisterRequestHandler(IRequestHandler jobHandler)
        {
            IRequestHandler handler = jobHandler;
            if (handler == null)
            {
                return;
            }
            handler.Response += Handler_IntermediateJobResponse;
            OnMessage(MessageLevel.Verbose, "Job Handler " + jobHandler.Name + " registered");
            this.JobHandlers.Add(handler);
        }

        public void DeregisterJobHandler(IRequestHandler jobHandler)
        {
            IRequestHandler handler = jobHandler;
            if (handler == null)
            {
                return;
            }
            handler.Response -= Handler_IntermediateJobResponse;
            if (handler.RunningJobs > 0)
            {
                handler.CancelJobs();
            }

            this.JobHandlers.Remove(handler);
            OnMessage(MessageLevel.Verbose, "Job Handler " + jobHandler.Name + " deregistered");
        }

        /// <summary>
        /// Run the Server Loop asynchron
        /// </summary>
        /// <exception cref="RequestTypeNotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns></returns>
        public async Task RunAsync(Config config)
        {
            this.IsRunning = true;
            Config runConfig = config;
            if (runConfig == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            OnMessage(MessageLevel.Information, "Starting server...");
            OnMessage(MessageLevel.Verbose, $"Initializing connector {this.Connector.Name}...");
            try
            {
                if (this.Connector.Initialize())
                    OnMessage(MessageLevel.Verbose, $"Connector {this.Connector.Name} initialized successfully");
                else
                {
                    OnMessage(MessageLevel.Critical, $"Connector {this.Connector.Name} failed to initialize");
                    goto shutdown;
                }
            }
            catch (Exception ex)
            {
                OnMessage(MessageLevel.Critical, $"Connector {this.Connector.Name} failed to initialize for reason: {ex.Message}");
                goto shutdown;
            }

            if (this.JobHandlers.Count > 0)
            {
                OnMessage(MessageLevel.Verbose, $"Number of available job handlers: {this.JobHandlers.Count}");
            }
            else
            {
                OnMessage(MessageLevel.Critical, $"No job handlers registered");
                goto shutdown;
            }

            this.CancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = this.CancellationTokenSource.Token;

            OnMessage(MessageLevel.Information, "Server started successfully. Waiting for requests...");
            await ServerLoop(cancellationToken, runConfig);
        shutdown:
            this.IsRunning = false;
            OnMessage(MessageLevel.Information, "shutting down server...");
            await this.Shutdown(true);
            this.CancellationTokenSource?.Dispose();
            OnMessage(MessageLevel.Information, "Server stopped");
        }

        /// <summary>
        /// Starts the Job Server and waits indefinitely for it to stop
        /// </summary>
        public void Run(Config config)
        {
            try
            {
                Task t = this.RunAsync(config);
                t.Wait();
            }
            catch (Exception ex)
            {
                OnMessage(MessageLevel.Debug, $"Caught unhandled {ex.GetType()} exception server execution");
                OnMessage(MessageLevel.Critical, ex.Message);
            }
        }

        public void Stop()
        {
            if (!this.IsRunning || this.CancellationTokenSource.IsCancellationRequested)
                return;

            OnMessage(MessageLevel.Information, "Signaling server to stop");
            this.CancellationTokenSource?.Cancel();
        }

        private async Task Shutdown(bool wait)
        {
            foreach (IRequestHandler jobHandler in this.JobHandlers)
            {
                if (jobHandler.RunningJobs > 0)
                {
                    OnMessage(MessageLevel.Verbose, $"{jobHandler.Name} has {jobHandler.RunningJobs} running jobs");
                    OnMessage(MessageLevel.Verbose, $"Signaling {jobHandler.Name} to cancel or finish running jobs");
                    jobHandler.CancelJobs();
                }
                if (wait)
                {
                    OnMessage(MessageLevel.Verbose, $"Waiting for {jobHandler.Name} to cancel or finish running jobs...");
                    while (jobHandler.RunningJobs > 0)
                    {
                        await Task.Delay(100);
                    }
                    OnMessage(MessageLevel.Verbose, $"All jobs for {jobHandler.Name} canceled or finished");
                }
            }
        }

        /// <summary>
        /// Get new <see cref="IRequest"/> from the <see cref="IConnector"/> and hand it to the <see cref="IRequestHandler"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <exception cref="RequestTypeNotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns></returns>
        private async Task ServerLoop(CancellationToken? token, Config? config)
        {
            if (!token.HasValue)
            {
                throw new ArgumentNullException(nameof(token));
            }

            Config? serverConfig = config;
            if (serverConfig == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            CancellationToken cancellationToken = token.Value;
            if (cancellationToken.IsCancellationRequested)
                return;

            IRequest? request = null;
            IRequestHandler? jobHandler = null;

            // We need to allow at least one job, or the whole server won't ever do anything
            int maxJobs = serverConfig.MaximumJobCount > 0 ? serverConfig.MaximumJobCount : 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                //If already to many Jobs are running, just skipp this cycle      
                int jobcount = 0;
                while ((jobcount = this.JobHandlers.Sum((jh) => { return jh.RunningJobs; })) < maxJobs && !cancellationToken.IsCancellationRequested)
                {
                    OnMessage(MessageLevel.Debug, $"Currently {jobcount} job {(jobcount != 1 ? "s are" : " is")} running");
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    try
                    {
                        OnMessage(MessageLevel.Verbose, $"Fetching new request from {this.Connector.Name}");
                        request = this.Connector.GetNextRequest();
                    }
                    catch (Exception ex)
                    {
                        OnMessage(MessageLevel.Error, $"Failed to get new request from {this.Connector.Name}: {ex.Message}");
                        break; // break out of this loop, since we didn't get a request
                    }

                    if (request == null)
                    {
                        OnMessage(MessageLevel.Verbose, "No requests pending");
                        break;
                    }

                    OnMessage(MessageLevel.Information, $"Received new request with ID {request.ID}");

                    jobHandler = this.GetJobHandler(request.Type);

                    if (jobHandler == null) // If no handler can handle this type, we haven't been provided proper Handlers. Throw exception
                    {
                        throw new RequestTypeNotSupportedException(request.ID, request.Type, $"No Handler found for request of type \"{request.Type}\"");
                    }
                    else // We have a handler
                    {
                        OnMessage(MessageLevel.Information, $"Request {request.ID} accepted by {jobHandler.Name}");
                        jobHandler.HandleAsync(request);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if(jobcount > serverConfig.MaximumJobCount)
                {
                    OnMessage(MessageLevel.Debug, $"Limit of {serverConfig.MaximumJobCount} concurrent jobs reached");
                }

                try
                {
                    if (serverConfig.SleepDuration > 0)
                    {
                        OnMessage(MessageLevel.Debug, $"Sleeping for {serverConfig.SleepDuration / 1000.0} seconds");
                        Task t = Task.Delay(serverConfig.SleepDuration, cancellationToken);
                        await t;
                        t.Dispose();
                    }
                }
                catch
                {
                    // If the Delay was canceled, we can just exit
                    break;
                }
            }
        }


        /**************************************************************
         * 
         * Request Handling
         * 
         *************************************************************/
        /// <summary>
        /// Get the First Handler able to Handle <paramref name="jobType"/>
        /// <para></para>
        /// 
        /// </summary>
        /// <param name="jobType">The Type of job to be handled</param>
        /// <returns>A <see cref="IRequestHandler"/> able to Handle <paramref name="jobType"/>. </returns>
        private IRequestHandler? GetJobHandler(string jobType)
        {
            string type = jobType;
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            IRequestHandler? jobHandler = null;
            foreach (IRequestHandler handler in this.JobHandlers)
            {
                if (handler.CanHandle(type))
                {
                    jobHandler = handler;
                    break;
                }
            }
            return jobHandler;
        }

        private void RespondToRequest(IResponse response)
        {
            IResponse resp = response;
            if (resp == null)
            {
                return;
            }

            this.Connector.Respond(resp);
        }

        /**************************************************************
         * 
         * IJobHandler Event Handlers
         * 
         *************************************************************/
        private void Handler_IntermediateJobResponse(IRequestHandler sender, IResponse jobResponse)
        {
            IResponse response = jobResponse;
            if (response == null)
            {
                return;
            }

            try
            {
                OnMessage(MessageLevel.Information, $"Responding to request {response.RequestID ?? "<N/A>"}");
                RespondToRequest(response);
                if (response.IsFinal)
                {
                    OnMessage(MessageLevel.Information, $"Request {response.RequestID ?? "N/A"} finished with state: {response.State}");
                }
                else
                {
                    OnMessage(MessageLevel.Verbose, $"Request {response.RequestID ?? "<N/A>"} state is: {response.State}");
                }
            }
            catch (Exception ex)
            {
                OnMessage(MessageLevel.Error, $"Failed to respond to request {response.RequestID ?? "<N/A>"}: {ex.Message}");
            }
        }

        /**************************************************************
         * 
         * Server Messaging
         * 
         *************************************************************/
        private void OnMessage(MessageLevel level, string message)
        {
            this.Message?.Invoke(this, level, message);
        }
    }
}
