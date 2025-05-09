// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal class DbConnectionPoolGroupProviderInfo
    {
        private DbConnectionPoolGroup _poolGroup;

        internal DbConnectionPoolGroup PoolGroup
        {
            get
            {
                return _poolGroup;
            }
            set
            {
                _poolGroup = value;
            }
        }
    }
}
