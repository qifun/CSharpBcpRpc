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

        public IList<ArraySegment<byte>> Buffers = new System.Collections.Generic.List<ArraySegment<byte>>();

        public override void writeByte(int c)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if(last.Count < PageSize)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                    Buffers.Add(current);
                }
            }
            current.Array[current.Count] = Convert.ToByte(c);
            var newLast = new ArraySegment<byte>(current.Array, 0, current.Count + 1);
            Buffers.RemoveAt(Buffers.Count - 1);
            Buffers.Add(newLast);
        }

        public override int writeBytes(Bytes s, int pos, int len)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if (last.Count < PageSize)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                    Buffers.Add(current);
                }
            }
            if (len <= PageSize - current.Count)
            {
                System.Array.Copy(s.getData(), pos, current.Array, current.Count, len);
                current = new ArraySegment<byte>(current.Array, 0, current.Count + len);
                Buffers.RemoveAt(Buffers.Count - 1);
                Buffers.Add(current);
                return len;
            }
            else
            {
                int result = PageSize - current.Count;
                byte[] bytes = current.ToArray();
                System.Array.Copy(s.getData(), pos, current.Array, current.Count, result);
                current = new ArraySegment<byte>(current.Array, 0, current.Count + result);
                Buffers.RemoveAt(Buffers.Count - 1);
                Buffers.Add(current);
                return result;
            }
        }

    }
}
