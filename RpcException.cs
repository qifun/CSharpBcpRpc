using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rpc
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
    
}
