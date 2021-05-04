using JobServer.Job;
using JobServer.Request;
using JobServer.RequestHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleAsyncJobServer.RequestHandler
{
    internal sealed class InternalAsyncRequestHandler : IRequestHandler
    {
        private readonly object joblock = new object();

        public event Action<IRequestHandler, IResponse>? Response;

        private Dictionary<string, Type> JobPlugins { get; set; }

        private CancellationTokenSource CancellationTokenSource { get; set; }

        private List<Task> Jobs { get; set; }

        /// <summary>
        /// The Name of the JobHandler
        /// </summary>
        public string Name => "InternalAsyncJobHandler";

        public int RunningJobs
        {
            get => this.Jobs.Count;
        }

        internal InternalAsyncRequestHandler()
        {
            this.JobPlugins = new Dictionary<string, Type>();
            this.CancellationTokenSource = new CancellationTokenSource();
            this.Jobs = new List<Task>();
        }

        public bool RegisterPlugin(string type, Type job)
        {
            string sType = type;
            Type jobType = job;
            if (string.IsNullOrWhiteSpace(sType))
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (jobType == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            // If there is no Plugin for the declared type in the List, add the new plugin
            if (!this.JobPlugins.Keys.Where(plug => { return plug.ToLower().Trim() == sType.ToLower().Trim(); }).Any())
            {
                this.JobPlugins.Add(sType.ToLower().Trim(), jobType);
                return true;
            }
            return false;
        }

        public bool CanHandle(string requestType)
        {
            string type = requestType.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            return this.JobPlugins.ContainsKey(type);
        }

        public async void HandleAsync(IRequest request)
        {
            IRequest req = request;
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string type = req.Type.ToLower().Trim();
            if (this.JobPlugins.ContainsKey(type))
            {
                Type jobType = this.JobPlugins[type];

                IJob? job = (IJob?)Activator.CreateInstance(jobType);
                if (job != null) // If we successfully got a Job Object, Start the Job
                {
                    job.IntermediateResponse += Job_IntermediateResponse;
                    job.RequestData = req.Data;
                    job.RequestID = req.ID;

                    CancellationToken cancellationToken = this.CancellationTokenSource.Token;
                    Task<IResponse> task = Task.Factory.StartNew((requestJob) =>
                    {
                        IJob? job = requestJob as IJob;
                        if (job == null)
                        {
                            throw new ArgumentNullException(nameof(requestJob));
                        }

                        return job.Execute(cancellationToken); //  It will be the Plugins Responsiblity to be aware of the Cancelation 

                    }, job, cancellationToken);
                    lock (joblock)
                    {
                        this.Jobs.Add(task);
                    }
                    try
                    {
                        IResponse response = await task;
                        // Clean up the Event Listener
                        job.IntermediateResponse -= Job_IntermediateResponse;
                        lock (joblock)
                        {
                            this.Jobs.Remove(task);
                        }
                        task.Dispose();
                        OnResponse(response);
                    }
                    catch (Exception ex)
                    {
                        lock (joblock)
                        {
                            this.Jobs.Remove(task);
                        }
                        task.Dispose();
                        OnResponse(DummyResponse.CreateRequestFailedResponse(req.ID, ex.Message + Environment.NewLine + ex.StackTrace));
                    }
                    job = null;
                }
                else
                {
                    OnResponse(DummyResponse.CreateRequestFailedResponse(req.ID, $"Could not create instance for job of type \"{type}\""));
                }
            }
            else
            {
                OnResponse(DummyResponse.CreateRequestFailedResponse(req.ID, $"No plugin for job of type {type} available"));
            }
        }

        private void Job_IntermediateResponse(IJob sender, IResponse intermediateResponse)
        {
            IResponse response = intermediateResponse;
            if (response == null)
            {
                return;
            }

            if(response.IsFinal) // Final Responses should be returned by IJob.Execute
            {
                return;
            }

            OnResponse(response);
        }

        private void OnResponse(IResponse response)
        {
            IResponse resp = response;
            if (resp == null)
            {
                return;
            }

            this.Response?.Invoke(this, resp);
        }

        public void CancelJobs()
        {
            this.CancellationTokenSource.Cancel();
        }

        /***************************************************
         * 
         * Event Handlers for Job Events   
         * 
         ***************************************************/

    }
}