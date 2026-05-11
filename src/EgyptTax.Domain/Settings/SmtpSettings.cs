namespace EgyptTax.Domain.Settings;

/// <summary>
/// Gux.13 Tab 5 — outbound email configuration. Single-row config.
/// Default <see cref="SendMethod"/> is MailClient (zero config:
/// MAPI opens Outlook/Thunderbird). DirectSmtp is the SMB+ feature
/// that lets DaftarX send emails directly without the operator's
/// mail client running.
///
/// Password storage: <see cref="EncryptedPassword"/> holds the
/// ciphertext produced by ASP.NET Data Protection. Plaintext is
/// never stored. The handler encrypts on write, decrypts on send.
/// </summary>
public sealed class SmtpSettings
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public EmailSendMethod SendMethod { get; private set; } = EmailSendMethod.MailClient;
    public string? Server { get; private set; }
    public int Port { get; private set; } = 587;
    public string? Username { get; private set; }
    /// <summary>Ciphertext blob from IDataProtector. Never plaintext.</summary>
    public string? EncryptedPassword { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }
    public bool UseTls { get; private set; } = true;

    private SmtpSettings() { }

    public static SmtpSettings CreateDefault() => new();

    public void UseMailClient()
    {
        SendMethod = EmailSendMethod.MailClient;
        // Don't wipe SMTP fields — operator may toggle back.
    }

    public void UseDirectSmtp(
        string server,
        int port,
        string username,
        string encryptedPassword,
        string fromAddress,
        string fromName,
        bool useTls)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromAddress);
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        SendMethod = EmailSendMethod.DirectSmtp;
        Server = server.Trim();
        Port = port;
        Username = username.Trim();
        EncryptedPassword = encryptedPassword;
        FromAddress = fromAddress.Trim();
        FromName = string.IsNullOrWhiteSpace(fromName) ? fromAddress.Trim() : fromName.Trim();
        UseTls = useTls;
    }
}

public enum EmailSendMethod
{
    /// <summary>Open the OS default mail client via MAPI with the
    /// PDF pre-attached. Zero-config; works for any edition.</summary>
    MailClient = 0,
    /// <summary>DaftarX sends the email directly via SMTP. Needs
    /// the SMTP fields configured. SMB+ feature.</summary>
    DirectSmtp = 1,
}
