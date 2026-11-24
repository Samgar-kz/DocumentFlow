using System.Text;
using DocumentFlow.Application.Files;
using FluentValidation;

namespace DocumentFlow.UnitTests;

public sealed class FileInspectorTests
{
    private static UploadedFile Upload(string name, byte[] bytes) => new(name, bytes.Length, new MemoryStream(bytes));

    [Fact]
    public async Task Accepts_a_real_pdf_and_derives_type_and_hash()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n");

        var file = await FileInspector.InspectAsync(Upload("Q3 report.PDF", bytes), CancellationToken.None);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Q3 report.PDF", file.FileName);
        Assert.Equal(64, file.Sha256.Length);
        Assert.Equal(bytes, file.Bytes);
    }

    [Fact]
    public async Task Rejects_an_executable_renamed_to_pdf()
    {
        var windowsExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            FileInspector.InspectAsync(Upload("invoice.pdf", windowsExecutable), CancellationToken.None));

        Assert.Contains("does not match", Assert.Single(error.Errors).ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("script.exe")]
    [InlineData("page.html")]
    [InlineData("macro.docm")]
    [InlineData("noextension")]
    public async Task Rejects_types_outside_the_allow_list(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            FileInspector.InspectAsync(Upload(name, [1, 2, 3]), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_empty_and_oversized_files()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            FileInspector.InspectAsync(Upload("empty.txt", []), CancellationToken.None));

        var tooBig = new UploadedFile("big.txt", FileInspector.MaxBytes + 1, new MemoryStream([1]));
        await Assert.ThrowsAsync<ValidationException>(() => FileInspector.InspectAsync(tooBig, CancellationToken.None));
    }

    [Fact]
    public async Task Text_files_must_not_contain_binary_data()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            FileInspector.InspectAsync(Upload("notes.txt", [0x41, 0x00, 0x42]), CancellationToken.None));

        var ok = await FileInspector.InspectAsync(Upload("notes.csv", "a,b\n1,2"u8.ToArray()), CancellationToken.None);
        Assert.Equal("text/csv", ok.ContentType);
    }

    [Theory]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData(@"C:\Users\me\Desktop\contract.pdf", "contract.pdf")]
    [InlineData("bad\"name<>.pdf", "badname.pdf")]
    [InlineData(".pdf", "file.pdf")]
    public void Sanitizes_file_names(string input, string expected)
    {
        Assert.Equal(expected, FileInspector.SanitizeFileName(input));
    }
}
