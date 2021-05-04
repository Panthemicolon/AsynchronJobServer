using JobServer.Request;
using System;
using System.Collections.Generic;
using System.Threading;

namespace JobServer.Job
{
    public interface IJob
    {
        /// <summary>
        /// Defines, what type of Job this is. This will be matched against Requests to find a fitting Job Plugin
        /// </summary>
        string Type { get; }

        string RequestID { get; set; }

        Dictionary<string, string> RequestData { get; set; }

        IResponse Execute(CancellationToken cancellationToken);

        event Action<IJob, IResponse> IntermediateResponse;
    }
}
