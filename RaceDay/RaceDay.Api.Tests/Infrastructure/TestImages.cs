using System.Net.Http.Headers;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>Builds small fake image uploads for tests.</summary>
public static class TestImages
{
    // The first bytes of a real PNG file, which is all the validator looks at
    public static byte[] PngBytes() => new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    public static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    public static MultipartFormDataContent ValidPng(string fileName = "image.png") => Form(PngBytes(), fileName);
}