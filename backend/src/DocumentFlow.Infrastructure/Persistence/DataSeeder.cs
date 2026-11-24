using System.Security.Cryptography;
using System.Text;
using DocumentFlow.Application.Abstractions;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentFlow.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Creates demo users and documents in every workflow state. Never enable in production.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password for all demo users (demo@, priya@, daniel@, emma@documentflow.dev).</summary>
    public string DemoPassword { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}

/// <summary>
/// Seeds an organisation with documents in every workflow state. Workflow actions go through the
/// domain model, so the audit trail and notifications are produced exactly as in real use.
/// </summary>
public sealed class DataSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IFileStorage storage,
    TimeProvider clock,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger)
{
    public const string DemoEmail = "demo@documentflow.dev";
    public const string AdminEmail = "admin@documentflow.dev";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || await db.Users.AnyAsync(u => u.Email == DemoEmail, ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DemoPassword) || string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            throw new InvalidOperationException("Seed:DemoPassword and Seed:AdminPassword must be set when seeding is enabled.");
        }

        var now = clock.GetUtcNow();
        DateTimeOffset Ago(double days) => now.AddDays(-days);

        var admin = AddUser(AdminEmail, "Platform Admin", "IT", settings.AdminPassword, UserRole.Admin);
        var alex = AddUser(DemoEmail, "Alex Morgan", "Finance", settings.DemoPassword);
        var priya = AddUser("priya@documentflow.dev", "Priya Shah", "Legal", settings.DemoPassword);
        var daniel = AddUser("daniel@documentflow.dev", "Daniel Kim", "Finance Leadership", settings.DemoPassword);
        var emma = AddUser("emma@documentflow.dev", "Emma Wilson", "Operations", settings.DemoPassword);
        foreach (var user in new[] { admin, alex, priya, daniel, emma })
        {
            user.CreatedAt = Ago(45);
        }

        // 1. Contract half-way through a two-step review: legal approved, finance pending.
        var contract = await NewDocumentAsync(alex, "Vendor Services Agreement - Northwind Logistics", DocumentCategory.Contract,
            "Three-year logistics services agreement covering warehousing and last-mile delivery.", Ago(6.2), ct);
        contract.Submit(alex.Id, [priya.Id, daniel.Id], Ago(6));
        contract.Approve(priya.Id, "Liability cap and termination clauses look good.", Ago(4));

        // 2. Fully approved report.
        var report = await NewDocumentAsync(alex, "Q3 Financial Summary", DocumentCategory.Report,
            "Quarterly results, cash position and variance analysis against budget.", Ago(20), ct);
        report.Submit(alex.Id, [daniel.Id], Ago(19.5));
        report.Approve(daniel.Id, "Numbers reconcile with the ledger. Approved.", Ago(18));

        // 3. Policy waiting for the demo user's approval.
        var remote = await NewDocumentAsync(emma, "Remote Work Policy 2027", DocumentCategory.Policy,
            "Updated eligibility, equipment allowance and core collaboration hours.", Ago(3), ct);
        remote.Submit(emma.Id, [alex.Id, priya.Id], Ago(2));

        // 4. Rejected invoice that needs a revision.
        var invoice = await NewDocumentAsync(alex, "Office Equipment Invoice INV-4471", DocumentCategory.Invoice,
            "Laptops and docking stations for the new analysts.", Ago(9.5), ct);
        invoice.Submit(alex.Id, [daniel.Id], Ago(9));
        invoice.Reject(daniel.Id, "Please attach the purchase order number before resubmitting.", Ago(8));

        // 5. Draft that has not been submitted yet.
        var memo = await NewDocumentAsync(alex, "Team Offsite Planning Memo", DocumentCategory.Memo,
            "Agenda, venue options and budget for the spring offsite.", Ago(1), ct);

        // 6. Second item in the demo user's inbox, with a discussion.
        var retention = await NewDocumentAsync(priya, "Data Retention Standard", DocumentCategory.Policy,
            "Retention periods for financial, HR and customer records.", Ago(5.5), ct);
        retention.Submit(priya.Id, [alex.Id, daniel.Id], Ago(5));
        retention.AddComment(priya.Id, "Section 4 now reflects the updated privacy regulation.", Ago(5));

        // 7. Approved contract owned by someone else.
        var nda = await NewDocumentAsync(emma, "NDA - Contoso Analytics", DocumentCategory.Contract,
            "Mutual non-disclosure agreement for the analytics pilot.", Ago(14), ct);
        nda.Submit(emma.Id, [priya.Id], Ago(13.5));
        nda.Approve(priya.Id, null, Ago(12));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded demo organisation with {Count} documents", 7);
    }

    private User AddUser(string email, string name, string department, string password, UserRole role = UserRole.Member)
    {
        var user = new User(email, name, department, role);
        user.SetPasswordHash(passwordHasher.Hash(user, password));
        db.Users.Add(user);
        return user;
    }

    private async Task<Document> NewDocumentAsync(
        User owner, string title, DocumentCategory category, string description, DateTimeOffset at, CancellationToken ct)
    {
        var document = Document.Create(owner.Id, title, description, category, at);
        document.CreatedAt = at;

        var bytes = SamplePdf(title, description);
        var key = $"{document.Id:N}/{Guid.CreateVersion7():N}.pdf";
        using (var content = new MemoryStream(bytes))
        {
            await storage.SaveAsync(key, content, ct);
        }

        var fileName = string.Concat(title.Split(Path.GetInvalidFileNameChars())).Replace(' ', '-') + ".pdf";
        document.AddVersion(
            owner.Id,
            new StoredFile(fileName, "application/pdf", bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)), key),
            "Initial version",
            at);

        db.Documents.Add(document);
        return document;
    }

    /// <summary>Builds a tiny one-page PDF so downloads open in any viewer.</summary>
    private static byte[] SamplePdf(string title, string body)
    {
        static string Escape(string text) => text.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");

        var stream = $"BT /F1 20 Tf 72 720 Td ({Escape(title)}) Tj ET\nBT /F1 12 Tf 72 690 Td ({Escape(body)}) Tj ET\nBT /F1 10 Tf 72 72 Td (Sample document generated by DocumentFlow) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
