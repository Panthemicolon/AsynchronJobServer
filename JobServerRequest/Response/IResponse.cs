using System;
using System.Collections.Generic;

namespace JobServer.Request
{
    public interface IResponse
    {
        string RequestID { get; }

        DateTime CreationTime { get; }

        RequestState State { get; }

        /// <summary>
        /// Indicates if the Response is the Final Response for the Corresponding Request
        /// </summary>
        bool IsFinal { get; }

        IResponseData Data { get; }
    }
}