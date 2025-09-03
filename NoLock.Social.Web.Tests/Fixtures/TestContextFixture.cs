using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.JSInterop;

namespace NoLock.Social.Web.Tests.Fixtures
{
    /// <summary>
    /// Base fixture for Blazor component and page testing with bUnit
    /// </summary>
    public class TestContextFixture : TestContext, IDisposable
    {
        protected Mock<IJSRuntime> MockJSRuntime { get; }

        public TestContextFixture()
        {
            // Initialize mocks
            MockJSRuntime = new Mock<IJSRuntime>();

            // Register services in test context
            Services.AddSingleton(MockJSRuntime.Object);

            // Add Blazor essentials
            Services.AddLogging();
            
            // Setup default JSInterop behavior
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <summary>
        /// Configure additional services for specific test scenarios
        /// </summary>
        protected void ConfigureServices(Action<IServiceCollection> configure)
        {
            configure(Services);
        }

        /// <summary>
        /// Reset all mocks to their initial state
        /// </summary>
        protected void ResetMocks()
        {
            MockJSRuntime.Reset();
        }

        public new void Dispose()
        {
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}