using Cauca.ApiClient.Extensions;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Exceptions
{
    [TestFixture]
    public class RestResponseExtensionsTests : BaseRestResponseTests
    {
        [TestCase]
        public void ExpiredRefreshTokenIsCorrectlyDetected()
        {
            var response = GetResponse(System.Net.HttpStatusCode.Unauthorized, RestResponseExtensions.RefreshTokenExpired);
            response.RefreshTokenIsExpired().Should().BeTrue();
        }

        [TestCase]
        public void InvalidAccessTokenIsCorrectlyDetected()
        {
            var response = GetResponse(System.Net.HttpStatusCode.Unauthorized, RestResponseExtensions.RefreshTokenInvalid);
            response.RefreshTokenIsInvalid().Should().BeTrue();
        }

        [TestCase]
        public void ExpiredAccessTokenIsCorrectlyDetected()
        {
            var response = GetResponse(System.Net.HttpStatusCode.Unauthorized, RestResponseExtensions.AccessTokenExpired);
            response.AccessTokenIsExpired().Should().BeTrue();
        }
    }
}