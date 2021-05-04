using JobServer.Request;
using JobServer.RequestHandler;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SimpleAsyncJobServer.RequestHandler
{
    /// <summary>
    /// A Dummy Request Handler, responding to any given Request with a "Type Not Supported" Response
    /// </summary>
    internal sealed class DummyAsyncRequestHandler : IRequestHandler
    {
        public string Name => "DummyAsyncRequestHandler";

        public int RunningJobs => 0;

        public event Action<IRequestHandler, IResponse>? Response;

        public void CancelJobs()
        {
        }

        public bool CanHandle(string type)
        {
            return true;
        }

        public async void HandleAsync(IRequest request)
        {
            IRequest notSupportedRequest = request;
            if (notSupportedRequest == null)
                return;

            Response?.Invoke(this, DummyResponse.CreateRequestFailedResponse(notSupportedRequest.ID, $"Requests of type \"{notSupportedRequest.Type}\" are not supported"));

            // Fake Async action to satisfy Compiler
            await Task.CompletedTask;
        }

        public bool RegisterPlugin(string type, Type job)
        {
            return true;
        }
    }
}
