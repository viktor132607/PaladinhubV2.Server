using System.Text;

namespace PaladinHubV2.Server.API.Services;

public sealed class PgDumpArchiveValidator :
    IPgDumpArchiveValidator
{
    private static readonly byte[] PgDumpMagic =
        Encoding.ASCII.GetBytes("PGDMP");

    public async Task ValidateAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            PgDumpMagic.Length,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        byte[] header =
            new byte[PgDumpMagic.Length];

        int totalRead = 0;

        while (totalRead < header.Length)
        {
            int read = await stream.ReadAsync(
                header.AsMemory(
                    totalRead,
                    header.Length - totalRead),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead != PgDumpMagic.Length ||
            !header.SequenceEqual(PgDumpMagic))
        {
            throw new InvalidDataException(
                "The file is not a PostgreSQL custom-format backup archive.");
        }
    }
}
