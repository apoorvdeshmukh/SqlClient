using System;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

#nullable enable
namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/SqlVector/*' />
    public class SqlVector<T> : SqlTDSVector, INullable where T : unmanaged
    {


        #region Constructors

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor1/*' />
        public SqlVector(int length)
        : this()
        {
            _length = length;
            _values = null;
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
                    $"{_elementName} array cannot be null",
                    nameof(values));
            }

            _length = values.Length;
            _values = values;
            _elementType = typeof(T) switch
            {
                Type t when t == typeof(float) => 0x0,
                Type t when t == typeof(Half) => 0x1,
                _ => throw new NotSupportedException(
         $"Type {typeof(T)} is not supported. Only float, Half, and int are allowed.")
            };
            _rawbytes = new byte[8 + _length * _elementSize];
            initBytes();

        }

        private void initBytes()
        {

            int elementSize = _elementSize;
            int arrayLength = _length;


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

            // Copy data
            var byteSpan = MemoryMarshal.AsBytes(new Span<T>(_values));
            byteSpan.CopyTo(_rawbytes.AsSpan(8));
        }

        // Convert a byte array to a T value, or throw ArgumentException.
        internal delegate T Converter(ReadOnlySpan<byte> src);

        // Construct a vector from a byte array.
        //
        // The length is supplied explicitly to help validate the byte array.
        //
        // The converter function is called for each chunk of bytes in the raw
        // array that should be converted to a T value.
        //
        // Throws ArgumentException if:
        //   - raw is null
        //   - raw.Length isn't a multiple of sizeof(T)
        //   - raw doesn't contain exactly length values
        //
        internal SqlVector(int length, byte[] raw, Converter converter)
        : this()
        {
            if (raw == null)
            {
                throw new ArgumentException(
                  $"{nameof(raw)} bytes cannot be null",
                  nameof(raw));
            }

            if (raw.Length % _elementSize != 0)
            {
                throw new ArgumentException(
                    $"{nameof(raw)} length={raw.Length} must be a multiple of " +
                    $"{_elementName} size={_elementSize}",
                    nameof(raw));
            }
            if (raw.Length / _elementSize != length)
            {
                throw new ArgumentException(
                    $"{nameof(raw)} length={raw.Length} must be equal to " +
                    $"length={length} * {_elementName} size={_elementSize}",
                    nameof(raw));
            }

            _length = length;

            _values = new T[length];
            for (int i = 0; i < length; ++i)
            {
                ReadOnlySpan<byte> span =
                    new(raw, i * (int)_elementSize, (int)_elementSize);

                _values[i] = converter(span);
            }
        }

        #endregion

        #region Properties
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/IsNull/*' />
        public bool IsNull => _values == null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ElementCount/*' />
        public int ElementCount => _length;

        // Returns the fully qualified name of the element type T.
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ElementName/*' />
        public string ElementName => _elementName;

        // Returns the number of bytes each element occupies.
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ElementSize/*' />
        public byte ElementSize => _elementSize;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ElementType/*' />
        public byte ElementType => _elementType;

        // Returns the values of the vector.
        //
        // Throws NullReferenceException if the vector is null.
        //
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Values/*' />
        public T[] Values
        {
            get
            {
                if (_values is null)
                {
                    throw new System.NullReferenceException(
                        $"{nameof(T)} values is null");
                }
                return _values;
            }
        }

        byte[] SqlTDSVector.VectorPayload
        {
            get
            {
                if (_rawbytes is null)
                {
                    throw new System.NullReferenceException(
                        $"{nameof(T)} bytes is null");
                }
                return _rawbytes;
            }
        }

        #endregion

        #region Helpers

        // Acquire the name and size of each T element.
        private SqlVector()
        {
            var name = typeof(T).FullName;

            if (name is null)
            {
                throw new ArgumentException("Generic type must have a name");
            }

            _elementName = name;

            int size = 0;
            unsafe
            {
                size = sizeof(T);
            }

            if (size < 1 || size > 255)
            {
                throw new ArgumentException(
                    $"{_elementName} size={size} cannot be less than 1 or more than 255",
                    _elementName);
            }

            _elementSize = (byte)size;
            _rawbytes = Array.Empty<byte>();
        }

        #endregion

        #region Fields

        private readonly string _elementName;
        private readonly byte _elementSize;
        private readonly int _length;
        private readonly T[]? _values;
        private readonly byte[] _rawbytes;
        private readonly byte _elementType;

        #endregion
    }
}
