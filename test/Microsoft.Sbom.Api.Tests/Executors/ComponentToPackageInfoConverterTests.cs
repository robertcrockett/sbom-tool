// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Sbom.Api.Entities;
using Microsoft.Sbom.Api.Manifest;
using Microsoft.Sbom.Api.Tests;
using Microsoft.Sbom.Api.Utils;
using Microsoft.Sbom.Common.Config;
using Microsoft.Sbom.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using HashAlgorithmName = Microsoft.Sbom.Contracts.Enums.AlgorithmName;
using ILogger = Serilog.ILogger;
using PackageInfo = Microsoft.Sbom.Contracts.SbomPackage;

namespace Microsoft.Sbom.Api.Executors.Tests;

using Microsoft.Sbom.Adapters.ComponentDetection;

[TestClass]
public class ComponentToPackageInfoConverterTests
{
    private readonly Mock<ILogger> mockLogger = new Mock<ILogger>();
    private readonly Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();
    private readonly ManifestGeneratorProvider manifestGeneratorProvider;

    [TestInitialize]
    public void Setup()
    {
    }

    public ComponentToPackageInfoConverterTests()
    {
        mockConfiguration.SetupGet(c => c.ManifestToolAction).Returns(ManifestToolActions.Validate);
        mockConfiguration.SetupGet(c => c.HashAlgorithm).Returns(new ConfigurationSetting<HashAlgorithmName> { Value = Constants.DefaultHashAlgorithmName });
        mockConfiguration.SetupGet(c => c.BuildComponentPath).Returns(new ConfigurationSetting<string> { Value = "root" });

        manifestGeneratorProvider = new ManifestGeneratorProvider(new IManifestGenerator[] { new TestManifestGenerator() });
        manifestGeneratorProvider.Init();
    }

    [TestMethod]
    public async Task ConvertTestAsync()
    {
        var scannedComponents = new List<ScannedComponentWithLicense>()
        {
            new ScannedComponentWithLicense
            {
                LocationsFoundAt = "test".Split(),
                Component = new NuGetComponent("nugetpackage", "1.0.0")
            },
            new ScannedComponentWithLicense
            {
                LocationsFoundAt = "test".Split(),
                Component = new NuGetComponent("nugetpackage2", "1.0.0")
            },
            new ScannedComponentWithLicense
            {
                LocationsFoundAt = "test".Split(),
                Component = new GitComponent(new Uri("http://test.uri"), "hash")
            },
            new ScannedComponentWithLicense
            {
                LocationsFoundAt = "test".Split(),
                Component = new MavenComponent("groupId", "artifactId", "1.0.0")
            }
        };

        var (output, errors) = await ConvertScannedComponents(scannedComponents);

        var expectedPackageNames = new List<string>
        {
            "nugetpackage", "nugetpackage2", "http://test.uri/ : hash - Git", "groupId.artifactId"
        };

        CollectionAssert.AreEquivalent(expectedPackageNames, output.Select(c => c.PackageName).ToList());

        Assert.IsFalse(errors?.Any());
    }

    [TestMethod]
    public async Task ConvertNuGet_AuthorPopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NuGetComponent("nugetpackage", "1.0.0")
            {
                Authors = new[] { "author1", "author2" }
            }
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.AreEqual($"Organization: {((NuGetComponent)scannedComponent.Component).Authors.First()}", packageInfo.Supplier);
    }

    [TestMethod]
    public async Task ConvertNuGet_AuthorNotPopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NuGetComponent("nugetpackage", "1.0.0") { Authors = null }
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.IsNull(packageInfo.Supplier);
    }

    [TestMethod]
    public async Task ConvertNuGet_LicensePopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NuGetComponent("nugetpackage", "1.0.0") { Authors = null },
            License = "MIT"
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.AreEqual("MIT", packageInfo.LicenseInfo.Concluded);
    }

    [TestMethod]
    public async Task ConvertNuGet_LicenseNotPopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NuGetComponent("nugetpackage", "1.0.0") { Authors = null },
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.IsNull(packageInfo.LicenseInfo?.Concluded);
    }

    [TestMethod]
    public async Task ConvertNpm_AuthorPopulated_Name()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NpmComponent("nugetpackage", "1.0.0", author: new NpmAuthor("Suzy Author"))
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.AreEqual($"Organization: {((NpmComponent)scannedComponent.Component).Author.Name}", packageInfo.Supplier);
    }

    [TestMethod]
    public async Task ConvertNpm_AuthorPopulated_NameAndEmail()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NpmComponent("nugetpackage", "1.0.0", author: new NpmAuthor("Suzy Author", "suzya@contoso.com"))
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.AreEqual($"Organization: {((NpmComponent)scannedComponent.Component).Author.Name} ({((NpmComponent)scannedComponent.Component).Author.Email})", packageInfo.Supplier);
    }

    [TestMethod]
    public async Task ConvertNpm_AuthorNotPopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NpmComponent("npmpackage", "1.0.0") { Author = null }
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.IsNull(packageInfo.Supplier);
    }

    [TestMethod]
    public async Task ConvertNpm_LicensePopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NpmComponent("npmpackage", "1.0.0") { Author = null },
            License = "MIT"
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.AreEqual("MIT", packageInfo.LicenseInfo.Concluded);
    }

    [TestMethod]
    public async Task ConvertNpm_LicenseNotPopulated()
    {
        var scannedComponent = new ScannedComponentWithLicense
        {
            Component = new NpmComponent("npmpackage", "1.0.0") { Author = null },
        };

        var packageInfo = await ConvertScannedComponent(scannedComponent);

        Assert.IsNull(packageInfo.LicenseInfo?.Concluded);
    }

    [TestMethod]
    public async Task ConvertWorksWithBuildComponentPathNull()
    {
        var scannedComponents = new List<ScannedComponentWithLicense>()
        {
            new ScannedComponentWithLicense
            {
                Component = new NuGetComponent("nugetpackage", "1.0.0")
            },
            new ScannedComponentWithLicense
            {
                Component = new NuGetComponent("nugetpackage2", "1.0.0")
            },
            new ScannedComponentWithLicense
            {
                Component = new GitComponent(new Uri("http://test.uri"), "hash")
            },
            new ScannedComponentWithLicense
            {
                Component = new MavenComponent("groupId", "artifactId", "1.0.0")
            }
        };

        var (output, errors) = await ConvertScannedComponents(scannedComponents);

        var expectedPackageNames = new List<string>
        {
            "nugetpackage", "nugetpackage2", "http://test.uri/ : hash - Git", "groupId.artifactId"
        };

        CollectionAssert.AreEquivalent(expectedPackageNames, output.Select(c => c.PackageName).ToList());

        Assert.IsFalse(errors?.Any());
    }

    private async Task<PackageInfo> ConvertScannedComponent(ScannedComponentWithLicense scannedComponent)
    {
        var componentsChannel = Channel.CreateUnbounded<ScannedComponent>();
        await componentsChannel.Writer.WriteAsync(scannedComponent);
        componentsChannel.Writer.Complete();
        var packageInfoConverter = new ComponentToPackageInfoConverter(mockLogger.Object);
        var (output, _) = packageInfoConverter.Convert(componentsChannel);
        var packageInfo = await output.ReadAsync();
        return packageInfo;
    }

    private async Task<(IEnumerable<PackageInfo>, IEnumerable<FileValidationResult>)> ConvertScannedComponents(IEnumerable<ScannedComponent> scannedComponents)
    {
        var componentsChannel = Channel.CreateUnbounded<ScannedComponent>();
        foreach (var scannedComponent in scannedComponents)
        {
            await componentsChannel.Writer.WriteAsync(scannedComponent);
        }

        componentsChannel.Writer.Complete();
        var packageInfoConverter = new ComponentToPackageInfoConverter(mockLogger.Object);
        var (output, errors) = packageInfoConverter.Convert(componentsChannel);
        return (await output.ReadAllAsync().ToListAsync(), await errors.ReadAllAsync().ToListAsync());
    }
}
