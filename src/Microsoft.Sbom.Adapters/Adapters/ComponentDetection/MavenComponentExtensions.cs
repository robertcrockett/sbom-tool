﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Sbom.Adapters.ComponentDetection;

using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Sbom.Contracts;

/// <summary>
/// Extensions methods for <see cref="MavenComponent" />.
/// </summary>
internal static class MavenComponentExtensions
{
    /// <summary>
    /// Converts a <see cref="MavenComponent" /> to an <see cref="SbomPackage" />.
    /// </summary>
    /// <param name="mavenComponent">The <see cref="MavenComponent" /> to convert.</param>
    /// <returns>The converted <see cref="SbomPackage" />.</returns>
    public static SbomPackage? ToSbomPackage(this MavenComponent mavenComponent) => new()
    {
        Id = mavenComponent.Id,
        PackageName = $"{mavenComponent.GroupId}.{mavenComponent.ArtifactId}",
        PackageUrl = mavenComponent.PackageUrl?.ToString(),
        PackageVersion = mavenComponent.Version,
        FilesAnalyzed = false,
        Type = "maven",
    };
}
