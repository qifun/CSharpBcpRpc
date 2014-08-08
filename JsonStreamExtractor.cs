using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.qifun.jsonStream;
using haxe.lang;

namespace Rpc
{
    internal class JsonStreamExtractor
    {
        private static int JsonStreamObjectIndex = Type.getEnumConstructs(typeof(JsonStream)).indexOf("OBJECT", Null<int>._ofDynamic(0));
        private static int JsonStreamArrayIndex = Type.getEnumConstructs(typeof(JsonStream)).indexOf("ARRAY", Null<int>._ofDynamic(0));

        public static Null<WrappedHaxeIterator<JsonStreamPair>> Object(JsonStream jsonStream)
        {
            if (Type.enumIndex(jsonStream) == JsonStreamObjectIndex)
            {
                return Null<WrappedHaxeIterator<JsonStreamPair>>._ofDynamic(new WrappedHaxeIterator<JsonStreamPair>(Type.enumParameters(0)));
            }
            else
            {
                return Null<WrappedHaxeIterator<JsonStreamPair>>._ofDynamic(null);
            }
        }
    }
}
