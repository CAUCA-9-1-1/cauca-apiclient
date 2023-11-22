﻿using Cauca.ApiClient.Tests.Mocks;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services
{
    [TestFixture]
    public class BaseSecureClientInternalTests : MockSecureRepository
    {
        public BaseSecureClientInternalTests() : base(new MockBaseApiClientConfiguration
        {
            ApiBaseUrl = "http://test",
        })
        {
            AccessInformation.AccessToken = "Token";
            AccessInformation.AuthorizationType = "Mock";
        }

        [TestCase]
        public void AuthorizationHeaderIsCorrectlyGenerated()
        {
            Assert.AreEqual("Mock Token", GetAuthorizationHeaderValue());
        }
    }
}