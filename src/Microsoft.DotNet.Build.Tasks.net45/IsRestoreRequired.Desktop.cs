// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class IsRestoreRequired
    {
        static IsRestoreRequired()
        {
            AssemblyResolver.Enable();
        }
    }
}