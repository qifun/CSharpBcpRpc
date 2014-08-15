using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BcpRpc
{
    public abstract class RpcException : Exception
    {
        public RpcException(string message, Exception cause) : base(message, cause)
        {
        }
    }

    public class IllegalRpcData : RpcException
    {
        public IllegalRpcData(string message = null, Exception cause = null)
            : base(message, cause)
        {
        }
    }

    public class UnknowServiceName : RpcException
    {
        public UnknowServiceName(string message = null, Exception cause = null)
            : base(message, cause)
        {
        }
    }

    public class ParseTextException: RpcException
    {
        public ParseTextException(string message = null, Exception cause = null)
            : base(message, cause)
        { 
        }
    }
    
}
