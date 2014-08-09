using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using haxe.io;
using haxe.lang;

namespace BcpRpc
{
    internal class ArraySegmentOutput : Output
    {
        private static int PageSize = 128;

        public IList<ArraySegment<byte>> Buffers = new ArraySegment<byte>[]{};

        public override void writeByte(int c)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, PageSize);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if(last.Count > 0)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, PageSize);
                    Buffers.Add(current);
                }
            }
            current.Array[current.Offset] = Convert.ToByte(c);
            var newLast = new ArraySegment<byte>(current.Array, current.Offset + 1, current.Count - 1);
            Buffers.RemoveAt(Buffers.Count);
            Buffers.Add(newLast);
        }

        public override int writeBytes(Bytes s, int pos, int len)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, PageSize);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if (last.Count > 0)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, PageSize);
                    Buffers.Add(current);
                }
            }
            if (len < current.Count)
            {
                System.Array.Copy(s.getData(), pos, current.Array, current.Offset, len);
                current = new ArraySegment<byte>(current.Array, current.Offset + len, current.Count - len);
                Buffers.RemoveAt(Buffers.Count);
                Buffers.Add(current);
                return len;
            }
            else
            {
                int result = current.Count;
                byte[] bytes = current.ToArray();
                System.Array.Copy(s.getData(), pos, current.Array, current.Offset, result);
                current = new ArraySegment<byte>(current.Array, current.Offset + result, current.Count - result);
                Buffers.RemoveAt(Buffers.Count);
                Buffers.Add(current);
                return result;
            }
        }

    }
}
