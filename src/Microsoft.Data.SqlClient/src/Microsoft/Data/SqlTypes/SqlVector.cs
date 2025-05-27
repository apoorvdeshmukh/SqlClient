using System;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;


#nullable enable
namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/SqlVector/*' />
    public class SqlVector<T> : INullable, ISqlVector where T : unmanaged
    {
        #region Constructors

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor1/*' />
        public SqlVector(int length)
        : this()
        {
            _elementCount = length;
        }

        // Construct a vector with an array of values.
        // The Length property is implied by the length of the array.
        // Throws ArgumentException if values is null.
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor2/*' />
        public SqlVector(T[] values)
        : this()
        {
            if (values == null)
            {
                throw new ArgumentException(
                    $"{values} array cannot be null",
                    nameof(values));
            }

            _elementCount = values.Length;
            _elementType = typeof(T) switch
            {
                Type t when t == typeof(float) => 0x0,
                _ => throw new NotSupportedException(
         $"Type {typeof(T)} is not supported. Only float allowed.")
            };
            _rawbytes = new byte[8 + _elementCount * _elementSize];
            initBytes(values);
        }

        internal SqlVector(byte[] data)
            : this()
        {
            _rawbytes = data;
            _elementCount = data[2] | (data[3] << 8);
            _elementType = data[4];
            _elementSize = (byte)MetaType.GetVectorElementSize(_elementType);
        }

        #endregion

        #region Properties
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/IsNull/*' />
        public bool IsNull => _rawbytes == null || _rawbytes.Length == 0;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Null/*' />
        public static SqlVector<T> Null => new(0);

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ElementCount/*' />
        public int ElementCount => _elementCount;

        // Returns the values of the vector.
        //
        // Throws NullReferenceException if the vector is null.
        //
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ToArray/*' />
        public T[]? ToArray()
        {
            int size = Marshal.SizeOf(typeof(T));
            int dataLength = _rawbytes.Length - 8;

            if (dataLength % size != 0)
                throw new InvalidOperationException("Data length after header is not a multiple of element size.");

            int length = dataLength / size;
            T[] result = new T[length];

#if NETFRAMEWORK
            if (typeof(T) == typeof(float))
            {
                float[] temp = new float[length];
                Buffer.BlockCopy(_rawbytes, 8, temp, 0, dataLength);
                return temp as T[];
            }
            else
            {
                throw new NotSupportedException("Only float is supported in .NET Framework.");
            }
#else
    ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(_rawbytes, 8, dataLength);
    return MemoryMarshal.Cast<byte, T>(dataSpan).ToArray();
#endif
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ToString/*' />
        public override string ToString()
        {
            T[]? array = ToArray();
            if (array == null)
            {
                return "NULL";
            }
            return JsonSerializer.Serialize(array);
        }


        #endregion

        #region Private Methods

        byte getType(Type T)
        {
            switch (T)
            {
            
            case Type t when t == typeof(float):
                    return 0x0;
                default:
                    throw new NotSupportedException($"Type {T} is not supported.");
            }
        }

        private void initBytes(T[] values)
        {

            int elementSize = _elementSize;
            int arrayLength = _elementCount;


            // Prefix bytes
            _rawbytes[0] = 0xA9;
            _rawbytes[1] = 0x01;
            _rawbytes[2] = (byte)(arrayLength & 0xFF);
            _rawbytes[3] = (byte)((arrayLength >> 8) & 0xFF);

            // Set type indicator
            if (typeof(T) == typeof(float))
                _rawbytes[4] = 0;
            else
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");

            // Remaining prefix bytes
            _rawbytes[5] = 0x00;
            _rawbytes[6] = 0x00;
            _rawbytes[7] = 0x00;

#if NETFRAMEWORK
            Buffer.BlockCopy(values, 0, _rawbytes, 8, values.Length * _elementSize);
#else
            // Fast span-based copy
            var byteSpan = MemoryMarshal.AsBytes(values.AsSpan());
            byteSpan.CopyTo(_rawbytes.AsSpan(8));
#endif
        }


        private SqlVector()
        {
            int size = 0;
            unsafe
            {
                size = sizeof(T);
            }
            
            _elementType = getType(typeof(T));
            _elementSize = (byte)size;
            _rawbytes = Array.Empty<byte>();

        }

        #endregion

        byte ISqlVector.ElementType => _elementType;
        byte ISqlVector.ElementSize => _elementSize;
        byte[] ISqlVector.VectorPayload
        {
            get
            {
                if (_rawbytes is null)
                {
                    throw new System.NullReferenceException(
                        $"SqlFloatVector is null");
                }
                return _rawbytes;
            }
        }


        #region Fields

        private readonly byte _elementSize;
        private readonly int _elementCount;
        private readonly byte[] _rawbytes;
        private readonly byte _elementType;

        #endregion
    }
}
