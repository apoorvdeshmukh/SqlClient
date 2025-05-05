using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlTypes
{
    internal static class Converters
    {
        public static Half ToHalf(ReadOnlySpan<byte> src)
        {
#if NETFRAMEWORK
            return Half.ToHalf(src.ToArray(), 0);
#else
    return BitConverter.ToHalf(src);
#endif
        }

        public static float ToSingle(ReadOnlySpan<byte> src)
        {
#if NETFRAMEWORK
            return BitConverter.ToSingle(src.ToArray(), 0);
#else
    return BitConverter.ToSingle(src);
#endif
        }

        public static double ToDouble(ReadOnlySpan<byte> src)
        {
#if NETFRAMEWORK
            return BitConverter.ToDouble(src.ToArray(), 0);
#else
    return BitConverter.ToDouble(src);
#endif
        }

        public static int ToInt(ReadOnlySpan<byte> src)
        {
#if NETFRAMEWORK
            return BitConverter.ToInt32(src.ToArray(), 0);
#else
    return BitConverter.ToInt32(src);
#endif
        }

        public static byte ToByte(ReadOnlySpan<byte> src)
        {
            if (src.Length < 1)
            {
                throw new ArgumentOutOfRangeException(
                  "Byte array is too short", nameof(src));
            }

            return src[0];
        }
    }
}
