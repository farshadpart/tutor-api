using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Tutor.Api.Utilities;

namespace Tutor.Api.Tests.Utilities;

public class EnvironmentUtilityTests
{
    private const string TutorConnectionStringEnvironmentVariableName = "TutorConnectionString";
    private const string RedisConnectionStringEnvironmentVariableName = "RedisConnectionString";

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsDevelopment_ReturnsConnectionStringsFromConfiguration()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Development);
        var configuration = CreateConfiguration(
            ("ConnectionStrings:TutorContext", "Host=localhost;Database=tutor"),
            ("ConnectionStrings:Redis", "localhost:6379"));

        // Act
        var connectionStrings = EnvironmentUtility.GetConnectionStrings(environment, configuration);

        // Assert
        connectionStrings.ShouldBe([
            ("TutorContext", "Host=localhost;Database=tutor"),
            ("Redis", "localhost:6379")
        ]);
    }

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsDevelopmentAndTutorContextConnectionStringIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Development);
        var configuration = CreateConfiguration(("ConnectionStrings:Redis", "localhost:6379"));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => EnvironmentUtility.GetConnectionStrings(environment, configuration));

        // Assert
        exception.Message.ShouldBe("Connection string: 'TutorContext' not found.");
    }

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsDevelopmentAndRedisConnectionStringIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Development);
        var configuration = CreateConfiguration(("ConnectionStrings:TutorContext", "Host=localhost;Database=tutor"));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => EnvironmentUtility.GetConnectionStrings(environment, configuration));

        // Assert
        exception.Message.ShouldBe("Connection string: 'Redis' not found.");
    }

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsNotDevelopment_ReturnsConnectionStringsFromEnvironmentVariables()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Production);
        var configuration = CreateConfiguration();

        try
        {
            Environment.SetEnvironmentVariable(TutorConnectionStringEnvironmentVariableName, "Host=production;Database=tutor");
            Environment.SetEnvironmentVariable(RedisConnectionStringEnvironmentVariableName, "production:6379");

            // Act
            var connectionStrings = EnvironmentUtility.GetConnectionStrings(environment, configuration);

            // Assert
            connectionStrings.ShouldBe([
                ("TutorContext", "Host=production;Database=tutor"),
                ("Redis", "production:6379")
            ]);
        }
        finally
        {
            ClearConnectionStringEnvironmentVariables();
        }
    }

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsNotDevelopmentAndTutorConnectionStringEnvironmentVariableIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Production);
        var configuration = CreateConfiguration();

        try
        {
            Environment.SetEnvironmentVariable(TutorConnectionStringEnvironmentVariableName, null);
            Environment.SetEnvironmentVariable(RedisConnectionStringEnvironmentVariableName, "production:6379");

            // Act
            var exception = Should.Throw<InvalidOperationException>(() => EnvironmentUtility.GetConnectionStrings(environment, configuration));

            // Assert
            exception.Message.ShouldBe("Connection string: 'TutorContext' not found.");
        }
        finally
        {
            ClearConnectionStringEnvironmentVariables();
        }
    }

    [Fact]
    public void GetConnectionStrings_WhenEnvironmentIsNotDevelopmentAndRedisConnectionStringEnvironmentVariableIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var environment = CreateEnvironment(Environments.Production);
        var configuration = CreateConfiguration();

        try
        {
            Environment.SetEnvironmentVariable(TutorConnectionStringEnvironmentVariableName, "Host=production;Database=tutor");
            Environment.SetEnvironmentVariable(RedisConnectionStringEnvironmentVariableName, null);

            // Act
            var exception = Should.Throw<InvalidOperationException>(() => EnvironmentUtility.GetConnectionStrings(environment, configuration));

            // Assert
            exception.Message.ShouldBe("Connection string: 'Redis' not found.");
        }
        finally
        {
            ClearConnectionStringEnvironmentVariables();
        }
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();
    }

    private static void ClearConnectionStringEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(TutorConnectionStringEnvironmentVariableName, null);
        Environment.SetEnvironmentVariable(RedisConnectionStringEnvironmentVariableName, null);
    }
}
