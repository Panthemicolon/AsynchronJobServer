using JobServer.Request;
using System;
namespace JobServer.RequestHandler
{
    public interface IRequestHandler
    {
        /// <summary>
        /// The Name of the RequestHandler
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The Number of Jobs handled by this Instance that are currently running 
        /// </summary>
        int RunningJobs { get; }

        /// <summary>
        /// Is triggered when an <see cref="IJob"/> responds to a request
        /// </summary>
        event Action<IRequestHandler, IResponse> Response;

        /// <summary>
        /// Registers a Plugin with the <see cref="IRequestHandler"/>
        /// </summary>
        /// <param name="type">The Type of the Plugin. This should be matched against <see cref="IRequest.Type"/> to find a matching plugin to answer the request</param>
        /// <param name="job">The actual Type of the Plugin.</param>
        /// <returns></returns>
        bool RegisterPlugin(string type, Type job);

        /// <summary>
        /// Hand the Request to the <see cref="IRequestHandler"/>
        /// </summary>
        /// <param name="request"></param>
        void HandleAsync(IRequest request);

        /// <summary>
        /// Cancel all <see cref="IJob"/> handled by the <see cref="IRequestHandler"/>
        /// </summary>
        void CancelJobs();

        /// <summary>
        /// Verifies, if the JobHandler can Handle Jobs of the given type
        /// </summary>
        /// <param name="type">The type of Request to be handled</param>
        /// <returns>true if the type of request can be handled</returns>
        bool CanHandle(string type);
    }
}
#nullable disable