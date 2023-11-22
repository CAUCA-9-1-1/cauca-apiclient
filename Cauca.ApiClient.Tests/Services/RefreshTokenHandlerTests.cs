using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Mocks;
using Flurl.Http.Testing;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services
{
    [TestFixture]
    public class RefreshTokenHandlerTests
    {
        private BaseApiClientConfiguration _baseApiClientConfiguration;
        private IAsyncPolicy noRetryPolicy;
        private RefreshTokenHandler tokenHandler;
        private AccessInformation accessInformation;

        [SetUp]
        public void SetupTest()
        {
            noRetryPolicy = new InstantRetryBuilder().BuildRetryPolicy(0);
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

            tokenHandler = new RefreshTokenHandler(_baseApiClientConfiguration, accessInformation, noRetryPolicy);
        }

        [TestCase(true, "http://test/Authentication/refreshforexternalsystem")]
        [TestCase(false, "http://test/Authentication/refresh")]
        public async Task UrlIsCorrectlyGeneratedForExternalSystemAndNormalUserRefresh(bool useExternalSystem, string urlThatShouldHaveBeenCalled)
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new TokenRefreshResult());
            _baseApiClientConfiguration.UseExternalSystemLogin = useExternalSystem;
            
            await tokenHandler.RefreshToken();

            httpTest.ShouldHaveCalled(urlThatShouldHaveBeenCalled);
        }

        [Test]
        public async Task AuthenticationUrlIsSet_WhenLoggingIn_ShouldUseBaseUrl()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = null;
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(loginResult);

            await tokenHandler.Login();

            httpTest.ShouldHaveCalled($"{_baseApiClientConfiguration.ApiBaseUrl}/Authentication/logon");
        }

        [Test]
        public async Task AuthenticationUrlIsSet_WhenLoggingIn_ShouldUseBaseAuthenticationUrl()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = "http://test/secureApi";
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(loginResult);

            await tokenHandler.Login();

            httpTest.ShouldHaveCalled($"{_baseApiClientConfiguration.ApiBaseUrlForAuthentication}/Authentication/logon");
        }

        [Test]
        public async Task NewAccessTokenIsCorrectlyCopiedInTheCurrentConfiguration()
        {
            var newToken = "newtoken";
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new TokenRefreshResult { AccessToken = newToken });

            await tokenHandler.RefreshToken();

            Assert.AreEqual(newToken, accessInformation.AccessToken);
        }

        [Test]
        public async Task NullIsCorrectlyReturnedForAnyOtherReason()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new TokenRefreshResult(), 404);
            
            await tokenHandler.RefreshToken();

            Assert.IsNull(accessInformation.AccessToken);
        }
    }
}
