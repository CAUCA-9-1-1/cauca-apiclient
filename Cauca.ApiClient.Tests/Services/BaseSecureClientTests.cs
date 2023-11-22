using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Tests.Mocks;
using Flurl.Http.Testing;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services
{
    [TestFixture]
    public class BaseSecureClientTests
    {
        private MockBaseApiClientConfiguration _baseApiClientConfiguration;

        [SetUp]
        public void SetupTest()
        {
            _baseApiClientConfiguration = new MockBaseApiClientConfiguration
            {
                ApiBaseUrl = "http://test/",
            };
        }

        [TestCase]
        public async Task RequestIsCorrectlyExecuted()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = new MockSecureRepository(_baseApiClientConfiguration);
                await repo.PostAsync<MockResponse>("mock", country);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .Times(1);
            }
        }

        [TestCase]
        public async Task RequestLoginBeforeExecutingWhenNotLoggedIn()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest
                    .RespondWithJson(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" })
                    .RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = new MockSecureRepository(_baseApiClientConfiguration);
                await repo.PostAsync<MockResponse>("mock", country);

                httpTest.ShouldHaveCalled("http://test/Authentication/logon")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .WithHeader("Authorization", "Bearer NewAccessToken")
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.OK)
                    .Times(1);
            }
        }

        [TestCase]
        public async Task WithApiBaseUrlForAuthentication_RequestLoginBeforeExecutingWhenNotLoggedIn_ShouldBeExecutedWithUrlForAuthentication()
        {
            _baseApiClientConfiguration.ApiBaseUrlForAuthentication = "http://test-for-authentication";

            using (var httpTest = new HttpTest())
            {
                httpTest
                    .RespondWithJson(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" })
                    .RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = new MockSecureRepository(_baseApiClientConfiguration);
                await repo.PostAsync<MockResponse>("mock", country);

                httpTest.ShouldHaveCalled("http://test-for-authentication/Authentication/logon")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .WithHeader("Authorization", "Bearer NewAccessToken")
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.OK)
                    .Times(1);
            }
        }

        [TestCase]
        public async Task LoginCorrectlySetAccessAndRefreshToken()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest
                    .RespondWithJson(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" })
                    .RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = new MockSecureRepository(_baseApiClientConfiguration);
                await repo.PostAsync<MockResponse>("mock", country);

                Assert.AreEqual("NewRefreshToken", repo.AccessInformation.RefreshToken);
                Assert.AreEqual("NewAccessToken", repo.AccessInformation.AccessToken);
            }
        }

        [TestCase]
        public async Task RequestRefreshTokenThenRetryWhenItsExpired()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest
                    .RespondWithJson(new MockResponse(), 401, new { Token_Expired = "True" })
                    .RespondWithJson(new TokenRefreshResult { AccessToken = "NewToken" })
                    .RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = CreateSecureRepository();
                await repo.PostAsync<MockResponse>("mock", country);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.Unauthorized)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/Authentication/refresh")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.OK)
                    .Times(1);
            }
        }

        [TestCase]
        public async Task RequestLogBackInWhenRefreshTokenAndAccessTokenAreExpired()
        {
            var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
            using (var httpTest = new HttpTest())
            {
                httpTest
                    .RespondWithJson(new MockResponse(), 401, new { Token_Expired = "True" })
                    .RespondWithJson(new TokenRefreshResult(), 401, new { Refresh_Token_Expired = true })
                    .RespondWithJson(loginResult)
                    .RespondWithJson(new MockResponse());

                var country = new MockEntity();
                var repo = CreateSecureRepository();
                await repo.PostAsync<MockResponse>("mock", country);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.Unauthorized)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/Authentication/refresh")
                    .WithVerb(HttpMethod.Post)
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.Unauthorized)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/Authentication/logon")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);

                httpTest.ShouldHaveCalled("http://test/mock")
                    .WithRequestJson(country)
                    .WithVerb(HttpMethod.Post)
                    .WithHeader("Authorization", $"{loginResult.AuthorizationType} {loginResult.AccessToken}")
                    .With(call => call.Response.StatusCode == (int)System.Net.HttpStatusCode.OK)
                    .Times(1);
            }
        }

        [TestCase]
        public void RequestIsThrowingErrorWhenUrlIsNotFound()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new MockResponse(), 404);
            var entity = new MockEntity();
            var repo = CreateSecureRepository();
            Assert.ThrowsAsync<NotFoundApiException>(async () => await repo.PostAsync<MockResponse>("mock", entity));
        }

        [TestCase]
        public void RequestIsThrowingErrorWhenGettingBadParameters()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new MockResponse(), 400);
            var entity = new MockEntity();
            var repo = CreateSecureRepository();
            Assert.ThrowsAsync<BadParameterApiException>(async () => await repo.PostAsync<MockResponse>("mock", entity));
        }

        [TestCase]
        public void RequestIsThrowingErrorWhenGettingForbidden()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new MockResponse(), 403);
            var entity = new MockEntity();
            var repo = CreateSecureRepository();
            Assert.ThrowsAsync<ForbiddenApiException>(async () => await repo.PostAsync<MockResponse>("mock", entity));
        }

        [TestCase]
        public void RequestIsThrowingErrorWhenGettingInternalError()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWithJson(new MockResponse(), 500);
            var entity = new MockEntity();
            var repo = new MockSecureRepository(_baseApiClientConfiguration);
            Assert.ThrowsAsync<InternalErrorApiException>(async () => await repo.PostAsync<MockResponse>("mock", entity));
        }

        [TestCase]
        public void RequestIsThrowingErrorWhenNotGettingAnAnswer()
        {
            using var httpTest = new HttpTest();
            httpTest.SimulateTimeout();
            var entity = new MockEntity();
            var repo = new MockSecureRepository(_baseApiClientConfiguration);
            Assert.ThrowsAsync<NoResponseApiException>(async () => await repo.PostAsync<MockResponse>("mock", entity));
        }

        private MockSecureRepository CreateSecureRepository()
        {
            var repo = new MockSecureRepository(_baseApiClientConfiguration);
            repo.AccessInformation.AccessToken = "Token";
            repo.AccessInformation.RefreshToken = "RefreshToken";
            repo.AccessInformation.AuthorizationType = "Mock";
            return repo;
        }
    }
}
