using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using haxe.lang;
using com.qifun.jsonStream.io;

namespace Rpc
{
    public abstract class TextSession : RpcSession
    {
        protected override IList<ArraySegment<byte>> ToByteBuffer(com.qifun.jsonStream.JsonStream js)
        {
            var output = new ArraySegmentOutput();
            PrettyTextPrinter.print(output, js, Null<int>._ofDynamic(0));
            return output.Buffers;
        }

        protected override com.qifun.jsonStream.JsonStream ToJsonStream(IList<ArraySegment<byte>> buffers)
        {
            return TextParser.parseInput(new ArraySegmentInput(buffers));
        }
    }
}
