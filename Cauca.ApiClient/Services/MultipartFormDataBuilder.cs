using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Cauca.ApiClient.Services;

internal sealed class MultipartFormDataBuilder
{
    private readonly List<Func<HttpContent>> _contentFactories = [];

    public void AddFile(string name, Stream stream, string fileName, string contentType)
    {
        _contentFactories.Add(() =>
        {
            var buffer = CopyToArray(stream);
            var content = new ByteArrayContent(buffer);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return CreateNamedContent(content, name, fileName);
        });
    }

    public void AddFile(string name, string fileFullPath)
    {
        _contentFactories.Add(() =>
        {
            var content = new StreamContent(File.OpenRead(fileFullPath));
            return CreateNamedContent(content, name, Path.GetFileName(fileFullPath));
        });
    }

    public MultipartFormDataContent Build()
    {
        var content = new MultipartFormDataContent();
        foreach (var contentFactory in _contentFactories)
            content.Add(contentFactory());

        return content;
    }

    private static HttpContent CreateNamedContent(HttpContent content, string name, string fileName)
    {
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = Quote(name),
            FileName = Quote(fileName),
            FileNameStar = fileName
        };

        return content;
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static byte[] CopyToArray(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}