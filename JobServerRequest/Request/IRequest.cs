using System;
using System.Collections.Generic;

namespace JobServer.Request
{
    public interface IRequest
    {
        string Creator { get; }

        DateTime CreationTime { get; }

        string ID { get; }

        string ParentID { get; }

        string Type { get; }

        Dictionary<string, string> Data { get; }
    }
}