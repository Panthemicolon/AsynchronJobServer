using System;

namespace SimpleAsyncJobServer
{
#nullable enable
    public class RequestTypeNotSupportedException : Exception
    {
        public string RequestID { get; protected set; }

        public string RequestType { get; protected set; }

        public RequestTypeNotSupportedException(string? requestID, string? requestType) : this(requestID, requestType, null)
        { }

        public RequestTypeNotSupportedException(string? requestID, string? requestType, string? message) : base(message)
        {
            this.RequestID = string.IsNullOrWhiteSpace(requestID) ? "N/A" : requestID;
            this.RequestType = string.IsNullOrWhiteSpace(requestType) ? "N/A" : requestType;
        }
    }
}
