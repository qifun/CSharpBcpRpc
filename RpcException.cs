using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rpc
{
    public abstract class RpcException : Exception
    {
        public RpcException(string message, Exception cause)
        {
            this.message = message;
            this.cause = cause;
        }

        private string message = null;
        private Exception cause = null;

        new public string Message { get; set; }
        public Exception Cause { get; set; }
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
