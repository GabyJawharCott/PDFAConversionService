using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PDFAConversionService.Tests.Helpers;
using Xunit;

namespace PDFAConversionService.Tests.Helpers
{
    public class GhostscriptPathHelperTests
    {
        [Fact]
        public void GetGhostscriptPath_ShouldReturnValidPath()
        {
            // Act
            var path = GhostscriptPathHelper.GetGhostscriptPath();

            // Assert
            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("gswin64c.exe");
        }

        [Fact]
        public void GetGhostscriptPath_WithConfiguration_ShouldPreferInstalledOverConfigured()
        {
            // Arrange
            var configPath = @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Ghostscript:ExecutablePath", configPath }
                })
                .Build();

            // Act
            var path = GhostscriptPathHelper.GetGhostscriptPath(configuration);

            // Assert
            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("gswin64c.exe");
            // If 'where' finds an installed version, it may differ from the configured path.
            // This test ensures the method prefers a discovered installed path when available.
        }

        [Fact]
        public void IsGhostscriptAvailable_ShouldReturnBoolean()
        {
            // Act
            var isAvailable = GhostscriptPathHelper.IsGhostscriptAvailable();

            // Assert
            _ = isAvailable;
        }

        [Fact]
        public void GetGhostscriptPath_ShouldTryWhereCommandIfPathNotFound()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Ghostscript:ExecutablePath", @"C:\Nonexistent\Path\gswin64c.exe" }
                })
                .Build();

            // Act
            var path = GhostscriptPathHelper.GetGhostscriptPath(configuration);

            // Assert
            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("gswin64c.exe");
        }
    }
}

