// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class PoolBlockingUtilities
    {
        internal static bool TryConvertToPoolBlockingPeriod(string value, out PoolBlockingPeriod result)
        {
            Debug.Assert(Enum.GetNames(typeof(PoolBlockingPeriod)).Length == 3, "PoolBlockingPeriod enum has changed, update needed");
            Debug.Assert(value != null, "TryConvertToPoolBlockingPeriod(null,...)");

            if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.Auto)))
            {
                result = PoolBlockingPeriod.Auto;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.AlwaysBlock)))
            {
                result = PoolBlockingPeriod.AlwaysBlock;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.NeverBlock)))
            {
                result = PoolBlockingPeriod.NeverBlock;
                return true;
            }
            else
            {
                result = DbConnectionStringDefaults.PoolBlockingPeriod;
                return false;
            }
        }

        internal static bool IsValidPoolBlockingPeriodValue(PoolBlockingPeriod value)
        {
            Debug.Assert(Enum.GetNames(typeof(PoolBlockingPeriod)).Length == 3, "PoolBlockingPeriod enum has changed, update needed");
            return value == PoolBlockingPeriod.Auto || value == PoolBlockingPeriod.AlwaysBlock || value == PoolBlockingPeriod.NeverBlock;
        }

        internal static string PoolBlockingPeriodToString(PoolBlockingPeriod value)
        {
            Debug.Assert(IsValidPoolBlockingPeriodValue(value));

            return value switch
            {
                PoolBlockingPeriod.AlwaysBlock => nameof(PoolBlockingPeriod.AlwaysBlock),
                PoolBlockingPeriod.NeverBlock => nameof(PoolBlockingPeriod.NeverBlock),
                _ => nameof(PoolBlockingPeriod.Auto),
            };
        }

        /// <summary>
        /// This method attempts to convert the given value to a PoolBlockingPeriod enum. The algorithm is:
        /// * if the value is from type string, it will be matched against PoolBlockingPeriod enum names only, using ordinal, case-insensitive comparer
        /// * if the value is from type PoolBlockingPeriod, it will be used as is
        /// * if the value is from integral type (SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, or UInt64), it will be converted to enum
        /// * if the value is another enum or any other type, it will be blocked with an appropriate ArgumentException
        ///
        /// in any case above, if the converted value is out of valid range, the method raises ArgumentOutOfRangeException.
        /// </summary>
        /// <returns>PoolBlockingPeriod value in the valid range</returns>
        internal static PoolBlockingPeriod ConvertToPoolBlockingPeriod(string keyword, object value)
        {
            Debug.Assert(value != null, "ConvertToPoolBlockingPeriod(null)");
            if (value is string sValue)
            {
                // We could use Enum.TryParse<PoolBlockingPeriod> here, but it accepts value combinations like
                // "ReadOnly, ReadWrite" which are unwelcome here
                // Also, Enum.TryParse is 100x slower than plain StringComparer.OrdinalIgnoreCase.Equals method.

                if (TryConvertToPoolBlockingPeriod(sValue, out PoolBlockingPeriod result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToPoolBlockingPeriod(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                PoolBlockingPeriod eValue;

                if (value is PoolBlockingPeriod period)
                {
                    // quick path for the most common case
                    eValue = period;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["PoolBlockingPeriod"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-PoolBlockingPeriod enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(PoolBlockingPeriod), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
                        eValue = (PoolBlockingPeriod)Enum.ToObject(typeof(PoolBlockingPeriod), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(PoolBlockingPeriod), e);
                    }
                }

                // ensure value is in valid range
                if (IsValidPoolBlockingPeriodValue(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(PoolBlockingPeriod), (int)eValue);
                }
            }
        }
    }
}
