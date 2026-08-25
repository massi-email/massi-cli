using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace EmailSweepAutomation;

public class GmailService
{
    public async Task<ImapClient> GetClientAsync(string email, string clientId, string clientSecret)
    {
        return await AuthenticateInternalAsync(email, clientId, clientSecret);
    }

    public async Task AuthenticateAsync(string email, string clientId, string clientSecret)
    {
        using var client = await AuthenticateInternalAsync(email, clientId, clientSecret);
        await client.DisconnectAsync(true);
    }

    private async Task<ImapClient> AuthenticateInternalAsync(string email, string clientId, string clientSecret)
    {
        var clientSecrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        var codeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            DataStore = new FileDataStore("CredentialCacheFolder", false),
            Scopes = new[] { "https://mail.google.com/" },
            ClientSecrets = clientSecrets
        });

        var codeReceiver = new LocalServerCodeReceiver();
        var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);

        AnsiConsole.MarkupLineInterpolated($"Authenticating with [red]{email}[/]...");

        var credential = await authCode.AuthorizeAsync(email, CancellationToken.None);

        if (credential.Token.IsStale)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Token is stale, refreshing...[/]");
            var tokenRefershed = false;
            try
            {
                tokenRefershed = await credential.RefreshTokenAsync(CancellationToken.None);
            } 
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Token refresh failed: {ex.Message}[/]");
            }
            if (tokenRefershed)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]Token refreshed.[/]");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Token refresh failed. Need to login again.[/]");
                credential = await authCode.AuthorizeAsync(email, CancellationToken.None);
            }
        }

        var oauth2 = new SaslMechanismOAuth2(credential.UserId, credential.Token.AccessToken);

        var client = new ImapClient();
        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(oauth2);
        return client;
    }
}
