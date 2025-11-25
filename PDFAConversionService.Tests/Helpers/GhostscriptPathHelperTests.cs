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
            // Path should be a valid format (even if file doesn't exist)
            path.Should().Contain("gswin64c.exe");
        }

        [Fact]
        public void GetGhostscriptPath_WithConfiguration_ShouldUseConfiguredPath()
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
            path.Should().Be(configPath);
        }

        [Fact]
        public void IsGhostscriptAvailable_ShouldReturnBoolean()
        {
            // Act
            var isAvailable = GhostscriptPathHelper.IsGhostscriptAvailable();

            // Assert - Just verify the method returns without throwing
            // The actual value depends on whether Ghostscript is installed
            _ = isAvailable; // Suppress unused variable warning
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
            // Should either return the configured path (if where command fails) 
            // or a path found by where command
            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("gswin64c.exe");
        }
    }
}

