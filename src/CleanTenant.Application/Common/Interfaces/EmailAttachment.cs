

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>E-posta dosya eki.</summary>
public class EmailAttachment
{
    public string FileName { get; set; }
    public byte[] Content { get; set; }
    public string ContentType { get; set; }

    public EmailAttachment(string fileName, byte[] content, string contentType = "application/octet-stream")
    {
        FileName = fileName;
        Content = content;
        ContentType = contentType;
    }
}
