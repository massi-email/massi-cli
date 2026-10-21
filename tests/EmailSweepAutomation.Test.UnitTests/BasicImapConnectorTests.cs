using MailKit.Security;

namespace EmailSweepAutomation.Test.UnitTests;

/// <summary>
/// The two decisions the IMAP connector makes before it opens a socket.
///
/// Neither can be asserted through <c>ConnectAsync</c> without a server to connect to, and both
/// are the kind of thing that fails silently in production: one of them decides whether the
/// password goes out in cleartext.
/// </summary>
public class BasicImapConnectorTests
{
    [Fact]
    public void WithSsl_TlsIsEstablishedBeforeAnythingIsSent()
        => Assert.Equal(SecureSocketOptions.SslOnConnect,
            BasicImapConnector.SecureOptionsFor(useSsl: true));

    [Fact]
    public void WithoutSsl_StartTlsIsRequired_NotMerelyPreferred()
    {
        // StartTlsWhenAvailable — the previous behaviour — sends the password in cleartext
        // against a server that does not advertise STARTTLS, and reports success. Nothing
        // afterwards distinguishes that from an encrypted session, so the failure is invisible
        // to the one person it happens to.
        Assert.Equal(SecureSocketOptions.StartTls,
            BasicImapConnector.SecureOptionsFor(useSsl: false));
        Assert.NotEqual(SecureSocketOptions.StartTlsWhenAvailable,
            BasicImapConnector.SecureOptionsFor(useSsl: false));
    }

    [Fact]
    public void CleartextIsAvailable_ButOnlyByAsking()
    {
        // A mail server on the same machine is a real case — the E2E suite's is, and so is a
        // self-hoster's — and refusing it outright would mean refusing to work where there is no
        // network to eavesdrop on. What must never happen is reaching this state by accident,
        // which is why it is a flag on the request and not a fallback in the handshake.
        Assert.Equal(SecureSocketOptions.None,
            BasicImapConnector.SecureOptionsFor(useSsl: false, allowCleartext: true));
    }

    [Fact]
    public void CleartextIsIgnored_WhenSslWasAskedFor()
    {
        // The two flags can disagree; encryption wins. Reading it the other way would let a
        // stray "allow cleartext" downgrade a connection somebody explicitly secured.
        Assert.Equal(SecureSocketOptions.SslOnConnect,
            BasicImapConnector.SecureOptionsFor(useSsl: true, allowCleartext: true));
    }

    [Fact]
    public void WithNoPortAndSsl_TheImapsPortIsUsed()
        => Assert.Equal(993, BasicImapConnector.PortFor(null, useSsl: true));

    [Fact]
    public void WithNoPortAndNoSsl_ThePlainImapPortIsUsed()
    {
        // 993 regardless of the security setting was the old default, which put a STARTTLS
        // connection on the implicit-TLS port: the client reads the TLS handshake as a protocol
        // greeting and fails with an error naming neither the port nor the setting.
        Assert.Equal(143, BasicImapConnector.PortFor(null, useSsl: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExplicitPort_IsAlwaysHonoured(bool useSsl)
    {
        // Users do run IMAP on non-standard ports, and the default must never override one
        // they typed.
        Assert.Equal(1143, BasicImapConnector.PortFor(1143, useSsl));
    }
}
