// Some unit tests exercise MailboxCommandSettings.Validate(), which reads the process-wide
// MASSI_IMAP_* environment variables through MailboxEnvBinder. Tests that temporarily set one of
// those variables (e.g. Validate_InvalidPortEnvVar_FailsViaBinder) would race with any other test
// reading them if test classes ran concurrently, so we serialize the whole assembly. This suite is
// small and fast, so the cost is negligible.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
