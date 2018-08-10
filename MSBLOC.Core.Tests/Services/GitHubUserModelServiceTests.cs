﻿using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MSBLOC.Core.Interfaces;
using MSBLOC.Core.Services;
using MSBLOC.Core.Tests.Util;
using NSubstitute;
using Octokit;
using Xunit;
using Xunit.Abstractions;

namespace MSBLOC.Core.Tests.Services
{
    public class GitHubUserModelServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ILogger<GitHubUserModelServiceTests> _logger;

        private static Faker<RepositoriesResponse> _fakeRepositoriesResponse;
        private static Faker<Installation> _fakeInstallation;

        public GitHubUserModelServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _logger = TestLogger.Create<GitHubUserModelServiceTests>(testOutputHelper);
        }

        static GitHubUserModelServiceTests()
        {
            var faker = new Faker();

            var appId = faker.Random.Long(0);

            var fakeUser = new Faker<User>()
                .RuleFor(u => u.Id, (f, u) => f.Random.Int(min: 0))
                .RuleFor(u => u.Login, (f, u) => f.Person.UserName);

            _fakeInstallation = new Faker<Installation>()
                .RuleFor(i => i.Id, (f, i) => f.Random.Long(min: 0))
                .RuleFor(i => i.AppId, (f, i) => appId)
                .RuleFor(i => i.Account, (f, i) => fakeUser.Generate());

            var fakeRepository = new Faker<Repository>()
                .RuleFor(response => response.Id, (faker1, repository) => faker1.Random.Long(0))
                .RuleFor(response => response.Owner, (faker1, repository) => fakeUser.Generate())
                .RuleFor(response => response.Name, (faker1, repository) => faker1.Lorem.Word())
                .RuleFor(response => response.Url, (faker1, repository) => faker1.Internet.Url());

            _fakeRepositoriesResponse = new Faker<RepositoriesResponse>()
                .RuleFor(r => r.Repositories, (f, r) => fakeRepository.Generate(faker.Random.Int(1, 10)))
                .RuleFor(r => r.TotalCount, (f, r) => r.Repositories.Count);
        }

        [Fact]
        public async Task ShouldGetUserInstallations()
        {
            var installation1 = _fakeInstallation.Generate();
            var repositoriesResponse1 = _fakeRepositoriesResponse.Generate();

            var installation2= _fakeInstallation.Generate();
            var repositoriesResponse2 = _fakeRepositoriesResponse.Generate();

            var installationsResponse = new InstallationsResponse(2, new[] { installation1, installation2 });

            var gitHubAppsClient = Substitute.For<IGitHubAppsClient>();
            gitHubAppsClient.GetAllInstallationsForUser()
                .Returns(installationsResponse);

            var gitHubAppsInstallationsClient = Substitute.For<IGitHubAppsInstallationsClient>();

            gitHubAppsInstallationsClient.GetAllRepositoriesForUser(installation1.Id)
                .Returns(repositoriesResponse1);

            gitHubAppsInstallationsClient.GetAllRepositoriesForUser(installation2.Id)
                .Returns(repositoriesResponse2);

            var gitHubUserModelService = CreateTarget(
                gitHubAppsClient: gitHubAppsClient,
                gitHubAppsInstallationsClient: gitHubAppsInstallationsClient
            );

            var userInstallations = await gitHubUserModelService.GetUserInstallations();
            userInstallations.Count.Should().Be(2);

            userInstallations[0].Id.Should().Be(installation1.Id);
            userInstallations[0].Repositories.Count.Should().Be(repositoriesResponse1.Repositories.Count);
            userInstallations[0].Repositories[0].Id.Should().Be(repositoriesResponse1.Repositories[0].Id);

            userInstallations[1].Id.Should().Be(installation2.Id);
            userInstallations[1].Repositories.Count.Should().Be(repositoriesResponse2.Repositories.Count);
            userInstallations[1].Repositories[0].Id.Should().Be(repositoriesResponse2.Repositories[0].Id);
        }

        [Fact]
        public async Task ShouldGetUserInstallation()
        {
            var installation1 = _fakeInstallation.Generate();
            var repositoriesResponse1 = _fakeRepositoriesResponse.Generate();

            var gitHubAppsClient = Substitute.For<IGitHubAppsClient>();
            gitHubAppsClient.GetInstallation(installation1.Id).Returns(installation1);

            var gitHubAppsInstallationsClient = Substitute.For<IGitHubAppsInstallationsClient>();

            gitHubAppsInstallationsClient.GetAllRepositoriesForUser(installation1.Id)
                .Returns(repositoriesResponse1);

            var gitHubUserModelService = CreateTarget(
                gitHubAppsClient: gitHubAppsClient,
                gitHubAppsInstallationsClient: gitHubAppsInstallationsClient
            );

            var userInstallation = await gitHubUserModelService.GetUserInstallation(installation1.Id);

            userInstallation.Id.Should().Be(installation1.Id);
            userInstallation.Repositories.Count.Should().Be(repositoriesResponse1.Repositories.Count);
            userInstallation.Repositories[0].Id.Should().Be(repositoriesResponse1.Repositories[0].Id);
        }

        private static GitHubUserModelService CreateTarget(
            IGitHubAppsInstallationsClient gitHubAppsInstallationsClient = null,
            IGitHubAppsClient gitHubAppsClient = null,
            IGitHubUserClientFactory gitHubUserClientFactory = null,
            IGitHubClient gitHubClient = null)
        {
            gitHubAppsInstallationsClient = gitHubAppsInstallationsClient ?? Substitute.For<IGitHubAppsInstallationsClient>();

            gitHubAppsClient = gitHubAppsClient ?? Substitute.For<IGitHubAppsClient>();
            gitHubAppsClient.Installations.Returns(gitHubAppsInstallationsClient);

            gitHubClient = gitHubClient ?? Substitute.For<IGitHubClient>();
            gitHubClient.GitHubApps.Returns(gitHubAppsClient);

            gitHubUserClientFactory = gitHubUserClientFactory ?? Substitute.For<IGitHubUserClientFactory>();
            gitHubUserClientFactory.CreateClient().Returns(gitHubClient);

            var gitHubUserModelService = new GitHubUserModelService(gitHubUserClientFactory);
            return gitHubUserModelService;
        }
    }
}
