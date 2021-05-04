using JobServer.Request;
using System;

namespace JobServer.Connector
{
#nullable enable
    public interface IConnector
    {
        /// <summary>
        /// Information about the Author of the Plugin
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Determines the Type of the Connector. This will be compared to the value in the configuration of the JobServer
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Generic Description Field, that allows to provide some basic description on the connector.
        /// </summary>
        string Description { get; }

        

        /// <summary>
        /// Get the next Request in the Queue
        /// </summary>
        /// <returns>The next Request or null</returns>
        IRequest? GetNextRequest();

        void Respond(IResponse response);

        bool Initialize();
    }
}
