using Xunit;

namespace FinanceApp.Api.Tests;

// Program configures Serilog globally, so all HTTP integration tests share one
// host and are kept sequential rather than trying to freeze that logger twice.
[CollectionDefinition("API integration", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
