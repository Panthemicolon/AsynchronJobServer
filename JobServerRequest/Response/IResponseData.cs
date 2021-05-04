using System;
using System.Collections.Generic;
using System.Text;

namespace JobServer.Request
{
    public interface IResponseData
    {
        public string Serialize();
    }
}
