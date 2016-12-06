﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Promotes dependencies from reference (ref) assembly TargetFramework to the implementation (lib) assembly 
    /// TargetFramework and vice versa.  
    /// NuGet only ever chooses a single dependencyGroup from a package.  Often the TFM of the implementation and 
    /// reference differ so in order to ensure the correct dependencies are applied we have to promote dependencies
    /// from a less specific ref to the more specific lib, and from a less specific lib to a more specific ref.
    /// </summary>
    public class PromoteDependencies : PackagingTask
    {
        private const string TargetFrameworkMetadataName = "TargetFramework";

        [Required]
        public ITaskItem[] Dependencies { get; set; }
        
        [Required]
        public string FrameworkListsPath { get; set; }

        [Output]
        public ITaskItem[] PromotedDependencies { get; set; }
        
        public override bool Execute()
        {
            List<ITaskItem> promotedDependencies = new List<ITaskItem>();

            var dependencies = Dependencies.Select(d => new Dependency(d)).ToArray();

            var refSets = dependencies.Where(d => d.Id != "_._").Where(d => d.IsReference).GroupBy(d => d.TargetFramework).ToDictionary(g => NuGetFramework.Parse(g.Key), g => g.ToArray());
            var refFxs = refSets.Keys.ToArray();

            var libSets = dependencies.Where(d => !d.IsReference).GroupBy(d => d.TargetFramework).ToDictionary(g => NuGetFramework.Parse(g.Key), g => g.ToArray());
            var libFxs = libSets.Keys.ToArray();


            if (libFxs.Length > 0)
            {
                foreach (var refFx in refFxs)
                {
                    // find best lib (if any)
                    var nearestLibFx = FrameworkUtilities.GetNearest(refFx, libFxs);

                    if (nearestLibFx != null && !nearestLibFx.Equals(refFx))
                    {
                        promotedDependencies.AddRange(CopyDependencies(libSets[nearestLibFx], refFx));
                    }
                }
            }

            if (refFxs.Length > 0)
            {
                foreach (var libFx in libFxs)
                {
                    // find best lib (if any)
                    var nearestRefFx = FrameworkUtilities.GetNearest(libFx, refFxs);

                    if (nearestRefFx == null && !nearestRefFx.Equals(libFx))
                    {
                        // This should never happen and indicates a bug in the package.  If a package contains references,
                        // all implementations should have an applicable reference assembly.
                        Log.LogError($"Could not find applicable reference assembly for implementation framework {libFx} from reference frameworks {string.Join(", ", refFxs.Select(f => f.GetShortFolderName()))}");
                    }
                    else
                    {
                        promotedDependencies.AddRange(CopyDependencies(refSets[nearestRefFx], libFx));
                    }
                }
            }

            PromotedDependencies = promotedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<ITaskItem> CopyDependencies(IEnumerable<Dependency> dependencies, NuGetFramework targetFramework)
        {
            foreach (var dependency in dependencies)
            {
                if (!Frameworks.IsInbox(FrameworkListsPath, targetFramework, dependency.Id, dependency.Version))
                {
                    var copiedDepenedency = new TaskItem(dependency.OriginalItem);
                    copiedDepenedency.SetMetadata(TargetFrameworkMetadataName, targetFramework.GetShortFolderName());
                    yield return copiedDepenedency;
                }
            }
        }

        private class Dependency
        {
            public Dependency(ITaskItem item)
            {
                Id = item.ItemSpec;
                Version = item.GetMetadata("Version");
                IsReference = item.GetMetadata("TargetPath").StartsWith("ref/", System.StringComparison.OrdinalIgnoreCase);
                TargetFramework = item.GetMetadata(TargetFrameworkMetadataName);
                OriginalItem = item;
            }

            public string Id { get; }
            public string Version { get; }

            public bool IsReference { get; }
            public string TargetFramework { get; }

            public ITaskItem OriginalItem { get; }
        }
    }
}
