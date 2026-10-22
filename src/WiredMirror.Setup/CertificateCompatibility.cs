namespace System.Security.Cryptography.X509Certificates;

// .NET 8 equivalent for the upstream .NET 10 API. Only a DER certificate from
// WinVerifyTrust's verified signer chain is accepted; never import key containers.
internal static class X509CertificateLoader
{
    internal static X509Certificate2 LoadCertificate(byte[] raw)
    {
        if (X509Certificate2.GetCertContentType(raw) != X509ContentType.Cert)
            throw new CryptographicException("Expected an X.509 certificate.");
        return new X509Certificate2(raw);
    }
}
