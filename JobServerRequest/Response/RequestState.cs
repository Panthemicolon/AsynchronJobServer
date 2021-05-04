namespace JobServer.Request
{
    public enum RequestState
    {
        Unknown, // We don't know the state, likely because there is no request
        Created, // The Request was created and is waiting to be accepted
        Pending, // The Request was loaded by the Sever but hasn't completed Execution
        Finished, // The Request was handled completely
        Failed // Something failed during handling of the Request
    }
}