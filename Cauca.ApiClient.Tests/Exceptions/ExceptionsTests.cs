using Cauca.ApiClient.Exceptions;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Exceptions
{
    [TestFixture]
    public class ExceptionsTests
    {
        protected string Url = "http://www.test.com/";

        [TestCase]
        public void BadParameterApiExceptionMessageIsCorrectlyGenerated()
        {
            new BadParameterApiException(Url).Message.Should().Be("API returned a 400 (bad request) response for url 'http://www.test.com/'.");
        }

        [TestCase]
        public void ExpiredRefreshTokenExceptionMessageIsCorrectlyGenerated()
        {
            new ExpiredRefreshTokenException().Message.Should().Be("The refresh token is expired.");
        }

        [TestCase]
        public void ForbiddenApiExceptionMessageIsCorrectlyGenerated()
        {
            new ForbiddenApiException(Url).Message.Should().Be("API returned a 403 (forbidden) response for url 'http://www.test.com/'.");
        }

        [TestCase]
        public void InternalErrorApiExceptionMessageIsCorrectlyGenerated()
        {
            new InternalErrorApiException(Url).Message.Should().Be("API returned a 500 (internal error) response for url 'http://www.test.com/'.");
        }

        [TestCase]
        public void InvalidRefreshTokenApiExceptionMessageIsCorrectlyGenerated()
        {
            new InvalidRefreshTokenException().Message.Should().Be("The refresh token is invalid.");
        }

        [TestCase]
        public void NoResponseApiExceptionMessageIsCorrectlyGenerated()
        {
            new NoResponseApiException().Message.Should().Be("API didn't return an answer in a timely manner.");
        }

        [TestCase]
        public void NotFoundApiExceptionMessageIsCorrectlyGenerated()
        {
            new NotFoundApiException(Url).Message.Should().Be("API returned a 404 (not found) response for url 'http://www.test.com/'.");
        }

        [TestCase]
        public void UnauthorizedApiExceptionMessageIsCorrectlyGenerated()
        {
            new UnauthorizedApiException(Url).Message.Should().Be("API returned a 401 (unauthorized) response for url 'http://www.test.com/'.");
        }
    }
}
