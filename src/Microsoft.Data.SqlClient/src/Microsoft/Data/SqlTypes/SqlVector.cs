using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Microsoft.Data.SqlTypes;

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/SqlVector/*' />
public sealed class SqlVector<T> : INullable, ISqlVector
where T : unmanaged
{
    #region Constants

    private const byte VecHeaderMagicNo = 0xA9;
    private const byte VecVersionNo = 0x01;

    #endregion

    #region Fields

    private readonly byte _elementType;
    private readonly byte _elementSize;
    private readonly byte[] _tdsBytes;
    private T[]? _array;

    #endregion

    #region Constructors

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor1/*' />
    public SqlVector(int length)
    {
        if (length < 0)
        {
            throw ADP.ArgumentOutOfRange(nameof(length), SQLResource.InvalidArraySizeMessage);
        }

        (_elementType, _elementSize) = GetTypeFieldsOrThrow();

        IsNull = true;

        Length = length;
        Size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * Length);

        _tdsBytes = Array.Empty<byte>();
        _array = null;
        Values = new();
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor2/*' />
    public SqlVector(ReadOnlyMemory<T> values)
    {
        (_elementType, _elementSize) = GetTypeFieldsOrThrow();

        IsNull = false;

        Length = values.Length;
        Size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * Length);

        _tdsBytes = MakeTdsBytes(values);
        _array = null;
        Values = values;
    }

    internal SqlVector(byte[] rawBytes)
    {
        (_elementType, _elementSize) = GetTypeFieldsOrThrow();

        (Length, Size) = GetCountsOrThrow(rawBytes);

        IsNull = false;

        _tdsBytes = rawBytes;
        _array = MakeArray();
        Values = new(_array);
    }

    #endregion

    #region Methods

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ToString/*' />
    public override string ToString()
    {
        if (IsNull)
        {
            return SQLResource.NullString;
        }
        return JsonSerializer.Serialize(Values);
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ToArray/*' />
    public T[] ToArray()
    {
        if (IsNull)
        {
            return Array.Empty<T>();
        }

        if (_array is null)
        {
            _array = Values.ToArray();
        }

        return _array;
    }

    #endregion

    #region Properties

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/IsNull/*' />
    public bool IsNull { get; init; }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Null/*' />
    public static SqlVector<T>? Null => null;

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Length/*' />
    public int Length { get; init; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Size/*' />
    public int Size { get; init; }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Values/*' />
    public ReadOnlyMemory<T> Values { get; init; }

    #endregion

    #region ISqlVector Internal Properties
    byte ISqlVector.ElementType => _elementType;
    byte ISqlVector.ElementSize => _elementSize;
    byte[] ISqlVector.VectorPayload => _tdsBytes;
    #endregion

    #region Helpers

    private (byte, byte) GetTypeFieldsOrThrow()
    {
        byte elementType;
        byte elementSize;

        if (typeof(T) == typeof(float))
        {
            elementType = (byte)MetaType.SqlVectorElementType.Float32;
            elementSize = sizeof(float);
        }
        else
        {
            throw SQL.VectorTypeNotSupported(typeof(T).FullName);
        }

        return (elementType, elementSize);
    }

    private byte[] MakeTdsBytes(ReadOnlyMemory<T> values)
    {
        byte[] result = new byte[Size];

        // Header Bytes
        result[0] = VecHeaderMagicNo;
        result[1] = VecVersionNo;
        result[2] = (byte)(Length & 0xFF);
        result[3] = (byte)((Length >> 8) & 0xFF);
        result[4] = _elementType;
        result[5] = 0x00;
        result[6] = 0x00;
        result[7] = 0x00;

#if NETFRAMEWORK
        // Copy data via marshaling.
        if (MemoryMarshal.TryGetArray(values, out ArraySegment<T> segment))
        {
            Buffer.BlockCopy(segment.Array, segment.Offset * _elementSize, result, TdsEnums.VECTOR_HEADER_SIZE, segment.Count * _elementSize);
        }
        else
        {
            Buffer.BlockCopy(values.ToArray(), 0, result, TdsEnums.VECTOR_HEADER_SIZE, values.Length * _elementSize);
        }
#else
        // Fast span-based copy.
        var byteSpan = MemoryMarshal.AsBytes(values.Span);
        byteSpan.CopyTo(result.AsSpan(TdsEnums.VECTOR_HEADER_SIZE));
#endif
        return result;
    }

    private (int, int) GetCountsOrThrow(byte[] rawBytes)
    {
        // Validate some of the header fields.
        if (
            // Do we have enough bytes for the header?
            rawBytes.Length < TdsEnums.VECTOR_HEADER_SIZE ||
            // Do we have the expected magic number?
            rawBytes[0] != VecHeaderMagicNo ||
            // Do we support the version?
            rawBytes[1] != VecVersionNo ||
            // Do the vector types match?
            rawBytes[4] != _elementType)
        {
            // No, so throw.
            throw ADP.InvalidVectorHeader();
        }

        // GOTCHA: This method is called _before_ our Length and Size
        // properties are set, so don't use them here!

        // The vector length is an unsigned 16-bit integer, little-endian.
        int length = rawBytes[2] | (rawBytes[3] << 8);
        // The vector size is the number of bytes required to represent the vector in TDS.
        int size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * length);

        // Are there exactly enough bytes for the vector elements?
        if (rawBytes.Length != size)
        {
            // No, so throw.
            throw ADP.InvalidVectorHeader();
        }

        return (length, size);
    }

    private T[] MakeArray()
    {
        if (_tdsBytes.Length == 0)
        {
            return Array.Empty<T>();
        }

#if NETFRAMEWORK
        // Allocate array and copy bytes into it
        T[] result = new T[Length];
        Buffer.BlockCopy(_tdsBytes, 8, result, 0, _elementSize * Length);
        return result;
#else
        ReadOnlySpan<byte> dataSpan = _tdsBytes.AsSpan(8, _elementSize * Length);
        return MemoryMarshal.Cast<byte, T>(dataSpan).ToArray();
#endif
    }
    
    #endregion
}
