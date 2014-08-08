using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.qifun.jsonStream.rpc;

namespace Rpc
{
    public class RpcDelegate
    {
        public delegate Service RpcFactoryCallback<Session, Service>(Session session);

        public delegate IJsonService IncomingViewCallback<Service>(Service service);

        public delegate IJsonService IncomingProxyCallback<Session>(Session session);

        public delegate Service OutgoingCallback<Service>(IJsonService jsonService);
    }
}
