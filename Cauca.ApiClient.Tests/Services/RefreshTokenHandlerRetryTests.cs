using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Mocks;
using Flurl.Http.Testing;
using NUnit.Framework;
using Polly;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Tests.Services
{


    [TestFixture]
    internal class RefreshTokenHandlerRetryTests
    {
        private BaseApiClientConfiguration _baseApiClientConfiguration;
        private IAsyncPolicy twoRetryPolicy;
        private AccessInformation accessInformation;
        private RefreshTokenHandler tokenHandler;

        [SetUp]
        public void SetupTest()
        {
            twoRetryPolicy = new InstantRetryBuilder().BuildRetryPolicy(2);
            accessInformation = new AccessInformation
            {
                AccessToken = "accesstoken",
                RefreshToken = "refreshtoken",
                AuthorizationType = "bearer"
            };
            _baseApiClientConfiguration = new MockBaseApiClientConfiguration
            {
                ApiBaseUrl = "http://test",
                UseExternalSystemLogin = false
            };

            tokenHandler = new RefreshTokenHandler(_baseApiClientConfiguration, accessInformation, twoRetryPolicy);
        }

        [Test]
        public async Task OnTransientFailure_WhenLoggingIn_ShouldRetry()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = null;
            using var httpTest = new HttpTest();
            httpTest.RespondWith(status: 502);
            httpTest.RespondWithJson(loginResult);

            await tokenHandler.Login();

            httpTest.ShouldHaveCalled($"{_baseApiClientConfiguration.ApiBaseUrl}/Authentication/logon").Times(2);
        }

        [Test]
        public async Task OnConnectionTimeOut_WhenLoggingIn_ShouldRetry()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = null;
            using var httpTest = new HttpTest();
            httpTest.SimulateTimeout();
            httpTest.RespondWithJson(loginResult);

            await tokenHandler.Login();

            httpTest.ShouldHaveCalled($"{_baseApiClientConfiguration.ApiBaseUrl}/Authentication/logon").Times(2);
        }

        [Test]
        public async Task OnTransientFailure_WhenRefreshingToken_ShouldRetry()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = null;
            using var httpTest = new HttpTest();
            httpTest.RespondWith(status: 502);
            httpTest.RespondWithJson(loginResult);

            await tokenHandler.RefreshToken();

            httpTest.ShouldHaveCalled($"{_baseApiClientConfiguration.ApiBaseUrl}/Authentication/refresh").Times(2);
        }
    }
}
