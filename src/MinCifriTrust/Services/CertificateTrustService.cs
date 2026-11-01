using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace MinCifriTrust.Services;

/// <summary>
/// Скачивание и импорт/удаление сертификатов НУЦ Минцифры
/// (Root → LocalMachine\Root, Sub → LocalMachine\CertificateAuthority).
/// </summary>
public sealed class CertificateTrustService
{
    public const string ExpectedRootThumbprint = "8FF915CCAB7BC16F8C5C8099D53E0E115B3AEC2F";

    private static readonly CertificateSource[] Sources =
    [
        new(
            "https://gu-st.ru/content/Other/doc/russian_trusted_root_ca.cer",
            "russian_trusted_root_ca.cer",
            StoreName.Root,
            ExpectedRootThumbprint),
        new(
            "https://gu-st.ru/content/Other/doc/russian_trusted_sub_ca.cer",
            "russian_trusted_sub_ca.cer",
            StoreName.CertificateAuthority,
            ExpectedThumbprint: null)
    ];

    private readonly string _certsDirectory;
    private readonly HttpClient _httpClient;

    public CertificateTrustService(string? certsDirectory = null, HttpClient? httpClient = null)
    {
        _certsDirectory = certsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "certs");
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string CertsDirectory => _certsDirectory;

    public async Task<TrustOperationResult> EnsureDownloadedAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_certsDirectory);

        foreach (var source in Sources)
        {
            var path = Path.Combine(_certsDirectory, source.FileName);

            byte[] bytes;
            try
            {
                bytes = await _httpClient.GetByteArrayAsync(source.Url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return TrustOperationResult.Fail(
                    $"Не удалось скачать {source.FileName}:\n{ex.Message}");
            }

            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(bytes);
            }
            catch (Exception ex)
            {
                return TrustOperationResult.Fail(
                    $"Файл {source.FileName} не является сертификатом:\n{ex.Message}");
            }

            using (cert)
            {
                if (source.ExpectedThumbprint is not null
                    && !string.Equals(
                        NormalizeThumbprint(cert.Thumbprint),
                        source.ExpectedThumbprint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return TrustOperationResult.Fail(
                        $"Отпечаток {source.FileName} не совпал с ожидаемым.\n" +
                        $"Получен: {NormalizeThumbprint(cert.Thumbprint)}\n" +
                        $"Ожидался: {source.ExpectedThumbprint}");
                }
            }

            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        }

        return TrustOperationResult.Ok("Сертификаты скачаны.");
    }

    /// <summary>
    /// true, если оба сертификата уже в нужных хранилищах LocalMachine.
    /// </summary>
    public bool AreAllTrusted()
    {
        if (!AllFilesPresent())
            return false;

        foreach (var source in Sources)
        {
            var path = Path.Combine(_certsDirectory, source.FileName);
            using var cert = new X509Certificate2(path);
            using var store = OpenStore(source.StoreName, OpenFlags.ReadOnly);
            var found = FindByThumbprint(store, cert.Thumbprint);
            try
            {
                if (found.Count == 0)
                    return false;
            }
            finally
            {
                DisposeAll(found);
            }
        }

        return true;
    }

    public TrustOperationResult Enable()
    {
        if (!AllFilesPresent())
        {
            return TrustOperationResult.Fail(
                $"Нет сертификатов в:\n{_certsDirectory}");
        }

        var imported = 0;
        var skipped = 0;

        foreach (var source in Sources)
        {
            var path = Path.Combine(_certsDirectory, source.FileName);
            using var cert = new X509Certificate2(path);
            using var store = OpenStore(source.StoreName, OpenFlags.ReadWrite);
            var found = FindByThumbprint(store, cert.Thumbprint);
            try
            {
                if (found.Count > 0)
                {
                    skipped++;
                    continue;
                }

                store.Add(cert);
                imported++;
            }
            finally
            {
                DisposeAll(found);
            }
        }

        return TrustOperationResult.Ok(
            $"Импортировано: {imported}, уже было: {skipped}.");
    }

    public TrustOperationResult Disable()
    {
        if (!AllFilesPresent())
        {
            return TrustOperationResult.Fail(
                $"Нет сертификатов в:\n{_certsDirectory}");
        }

        var removed = 0;
        var missing = 0;

        foreach (var source in Sources)
        {
            var path = Path.Combine(_certsDirectory, source.FileName);
            using var cert = new X509Certificate2(path);
            using var store = OpenStore(source.StoreName, OpenFlags.ReadWrite);
            var found = FindByThumbprint(store, cert.Thumbprint);
            try
            {
                if (found.Count == 0)
                {
                    missing++;
                    continue;
                }

                foreach (X509Certificate2 item in found)
                {
                    store.Remove(item);
                    removed++;
                }
            }
            finally
            {
                DisposeAll(found);
            }
        }

        return TrustOperationResult.Ok(
            $"Удалено: {removed}, не найдено: {missing}.");
    }

    private bool AllFilesPresent()
        => Sources.All(s => File.Exists(Path.Combine(_certsDirectory, s.FileName)));

    private static X509Store OpenStore(StoreName storeName, OpenFlags flags)
    {
        var store = new X509Store(storeName, StoreLocation.LocalMachine);
        store.Open(flags);
        return store;
    }

    private static X509Certificate2Collection FindByThumbprint(X509Store store, string thumbprint)
    {
        return store.Certificates.Find(
            X509FindType.FindByThumbprint,
            NormalizeThumbprint(thumbprint),
            validOnly: false);
    }

    private static string NormalizeThumbprint(string thumbprint)
        => thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static void DisposeAll(X509Certificate2Collection certs)
    {
        foreach (X509Certificate2 cert in certs)
            cert.Dispose();
    }

    private sealed record CertificateSource(
        string Url,
        string FileName,
        StoreName StoreName,
        string? ExpectedThumbprint);
}

public sealed record TrustOperationResult(bool Success, string Message)
{
    public static TrustOperationResult Ok(string message) => new(true, message);
    public static TrustOperationResult Fail(string message) => new(false, message);
}
