using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;

namespace DocumentFlow.Application.Files;

/// <summary>A file received from a client, before validation.</summary>
public sealed record UploadedFile(string FileName, long Length, Stream Content);

/// <summary>A validated file held in memory, ready to be stored.</summary>
public sealed record InspectedFile(string FileName, string ContentType, byte[] Bytes, string Sha256);

/// <summary>
/// Validates uploads with an allow-list of extensions and checks the file's leading "magic bytes",
/// so a renamed executable cannot be uploaded as a PDF. The content type is derived from the
/// verified extension; the client-supplied type is never trusted.
/// </summary>
public static class FileInspector
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int FileNameMaxLength = 200;

    private static readonly byte[] Pdf = "%PDF"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];

    private static readonly Dictionary<string, (string ContentType, byte[]? Signature)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", Pdf),
            [".png"] = ("image/png", Png),
            [".jpg"] = ("image/jpeg", Jpeg),
            [".jpeg"] = ("image/jpeg", Jpeg),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Zip),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Zip),
            [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", Zip),
            [".txt"] = ("text/plain", null),
            [".csv"] = ("text/csv", null),
        };

    public static IReadOnlyCollection<string> AllowedExtensions => Allowed.Keys;

    public static async Task<InspectedFile> InspectAsync(UploadedFile file, CancellationToken ct)
    {
        var fileName = SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(extension, out var rule))
        {
            throw Invalid($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", Allowed.Keys)}.");
        }

        if (file.Length == 0)
        {
            throw Invalid("The file is empty.");
        }

        if (file.Length > MaxBytes)
        {
            throw Invalid($"The file is larger than {MaxBytes / (1024 * 1024)} MB.");
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBytes)
        {
            throw Invalid($"The file is larger than {MaxBytes / (1024 * 1024)} MB.");
        }

        var bytes = buffer.ToArray();
        var looksRight = rule.Signature is null
            ? !bytes.AsSpan(0, Math.Min(bytes.Length, 8192)).Contains((byte)0)
            : bytes.AsSpan().StartsWith(rule.Signature);

        if (!looksRight)
        {
            throw Invalid($"The file content does not match the '{extension}' file type.");
        }

        return new InspectedFile(fileName, rule.ContentType, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>Keeps only the file name part and removes characters that are unsafe in headers or paths.</summary>
    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/').Split('/').Last()).Trim();
        var invalid = Path.GetInvalidFileNameChars().Concat(['"', '<', '>', '|', ':', '*', '?']).ToHashSet();
        name = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray());

        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name)))
        {
            name = "file" + Path.GetExtension(name);
        }

        if (name.Length > FileNameMaxLength)
        {
            var extension = Path.GetExtension(name);
            name = name[..(FileNameMaxLength - extension.Length)] + extension;
        }

        return name;
    }

    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure("file", message)]);
}
