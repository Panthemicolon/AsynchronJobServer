using JobServer.Request;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleAsyncJobServer
{
    public class DummyResponse : IResponse
    {
        public string RequestID { get; private set; }

        public DateTime CreationTime { get; private set; }

        public RequestState State { get; private set; }

        public bool IsFinal { get; private set; }

        public IResponseData Data { get; private set; }

        private DummyResponse(string requestID)
        {
            this.Data = new JsonResponseData();
            this.CreationTime = DateTime.UtcNow;
            this.RequestID = requestID;
            this.IsFinal = true;
            this.State = RequestState.Unknown;
        }

        internal static IResponse CreateRequestFailedResponse(string requestID, string v)
        {
            DummyResponse response = new DummyResponse(requestID);
            response.State = RequestState.Failed;
            JsonResponseData responseData = (JsonResponseData)response.Data;
            responseData.AddValue("Error", v);
            response.Data = responseData;
            return response;
        }
    }
}
