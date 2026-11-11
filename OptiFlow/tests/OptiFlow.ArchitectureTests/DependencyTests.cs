using FluentAssertions;
using NetArchTest.Rules;
using OptiFlow.Api.Controllers;
using OptiFlow.Application.Diff;
using OptiFlow.Domain.Entities;
using OptiFlow.Infrastructure.Persistence;
using OptiFlow.Opal.Tools;
using OptiFlow.Worker;
using Xunit;

namespace OptiFlow.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOnApplicationInfrastructureApiWorkerOrOpal()
    {
        TestResult result = Types.InAssembly(typeof(ContentItem).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OptiFlow.Application",
                "OptiFlow.Infrastructure",
                "OptiFlow.Api",
                "OptiFlow.Worker",
                "OptiFlow.Opal",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "System.Net.Http",
                "System.Text.Json")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureApiWorkerOrOpal()
    {
        TestResult result = Types.InAssembly(typeof(ContentDiffEngine).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OptiFlow.Infrastructure",
                "OptiFlow.Api",
                "OptiFlow.Worker",
                "OptiFlow.Opal",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Opal_ShouldNotDependDirectlyOnInfrastructureOrEfCore()
    {
        TestResult result = Types.InAssembly(typeof(AnalyzeContentChangesTool).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OptiFlow.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Controllers_ShouldNotDependDirectlyOnInfrastructurePersistence()
    {
        TestResult result = Types.InAssembly(typeof(ContentController).Assembly)
            .That()
            .ResideInNamespace("OptiFlow.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "OptiFlow.Infrastructure.Persistence",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Worker_ShouldNotDependDirectlyOnEntityFrameworkCore()
    {
        TestResult result = Types.InAssembly(typeof(JobProcessorWorker).Assembly)
            .That()
            .ResideInNamespace("OptiFlow.Worker")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void InfrastructureDbContext_ShouldExistInExpectedNamespace()
    {
        typeof(OptiFlowDbContext).Namespace.Should().Be("OptiFlow.Infrastructure.Persistence");
    }
}
