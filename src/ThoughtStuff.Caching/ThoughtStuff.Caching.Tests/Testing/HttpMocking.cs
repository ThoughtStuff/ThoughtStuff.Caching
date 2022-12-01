// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests.Testing;

public static class HttpMocking
{
    public static void SetupHttpClient(this Mock<IHttpClientFactory> httpClientFactory, string response = "")
    {
        var handler = new MockMessageHandler(request =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            };
        });
        SetupHttpClient(httpClientFactory, handler);
    }

    public static void SetupHttpClient(this Mock<IHttpClientFactory> httpClientFactory, Uri expectedUrl, string response)
    {
        var handler = new MockMessageHandler(request =>
        {
            if (request.RequestUri != expectedUrl)
                throw new Exception("Wrong URI for HttpClient"
                    + $"\n Expected: {expectedUrl}"
                    + $"\n Actual:   {request.RequestUri}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            };
        });
        SetupHttpClient(httpClientFactory, handler);
    }

    public static void SetupHttpClient(this Mock<IHttpClientFactory> httpClientFactory, string expectedUrl, string response)
    {
        httpClientFactory.SetupHttpClient(new Uri(expectedUrl), response);
    }

    private static void SetupHttpClient(Mock<IHttpClientFactory> httpClientFactory, MockMessageHandler handler)
    {
        var client = new HttpClient(handler);
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(client);
    }
}
