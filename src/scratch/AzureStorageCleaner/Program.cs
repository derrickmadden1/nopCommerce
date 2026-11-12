using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;

namespace AzureStorageCleaner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var isDeleteMode = args.Any(a => a.Equals("--delete", StringComparison.OrdinalIgnoreCase) || a.Equals("-d", StringComparison.OrdinalIgnoreCase));

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================");
        Console.WriteLine(" nopCommerce Azure Blob Storage Image Auditor & Cleaner");
        Console.WriteLine($" Mode: {(isDeleteMode ? "LIVE DELETION (destructive)" : "DRY RUN (simulation only - NO files deleted)")}");
        Console.WriteLine("==========================================================");
        Console.ResetColor();

        // 1. Fetch settings from local DB
        var sqlConnString = "Server=DERRICK-SURFACE\\SQLEXPRESS;Database=rcc_azure_20261112;Integrated Security=SSPI;TrustServerCertificate=True";
        Console.WriteLine("\n[1/4] Connecting to database to fetch active pictures and Azure settings...");

        string azureConnString = null;
        string containerName = "rosecottagestore";
        var activePictures = new Dictionary<int, (string SeoFilename, string MimeType)>();

        await using (var conn = new SqlConnection(sqlConnString))
        {
            await conn.OpenAsync();

            // Load settings
            await using (var cmdSettings = conn.CreateCommand())
            {
                cmdSettings.CommandText = "SELECT [Name], [Value] FROM [Setting] WHERE [Name] IN ('azureblobsettings.connectionstring', 'azureblobsettings.containername')";
                await using var reader = await cmdSettings.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(0).ToLowerInvariant();
                    var val = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (name == "azureblobsettings.connectionstring") azureConnString = val;
                    if (name == "azureblobsettings.containername" && !string.IsNullOrWhiteSpace(val)) containerName = val.Trim();
                }
            }

            // Load active pictures
            await using (var cmdPictures = conn.CreateCommand())
            {
                cmdPictures.CommandText = "SELECT [Id], [SeoFilename], [MimeType] FROM [Picture]";
                await using var reader = await cmdPictures.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var seo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var mime = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    activePictures[id] = (seo, mime);
                }
            }
        }

        Console.WriteLine($" -> Loaded {activePictures.Count:N0} active pictures from nopCommerce database.");
        if (string.IsNullOrWhiteSpace(azureConnString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Azure Blob Connection string not found in database settings.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($" -> Azure Container: {containerName}");

        // 2. Scan Azure Blob Storage
        Console.WriteLine("\n[2/4] Scanning blobs in Azure Storage container...");
        var blobServiceClient = new BlobServiceClient(azureConnString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Container '{containerName}' does not exist in Azure.");
            Console.ResetColor();
            return 1;
        }

        var results = new List<BlobAnalysisResult>();
        long totalBytesScanned = 0;

        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            var size = blob.Properties.ContentLength ?? 0;
            totalBytesScanned += size;

            var result = AnalyzeBlob(blob.Name, size, blob.Properties.LastModified, activePictures);
            results.Add(result);

            if (results.Count % 1000 == 0)
            {
                Console.Write($"\r -> Scanned {results.Count:N0} blobs ({FormatBytes(totalBytesScanned)})...");
            }
        }

        Console.WriteLine($"\r -> Completed scan of {results.Count:N0} total blobs ({FormatBytes(totalBytesScanned)}).");

        // 3. Summarize findings
        Console.WriteLine("\n[3/4] Analysis Results:");
        var activeBlobs = results.Where(r => r.Category == BlobCategory.Active || r.Category == BlobCategory.SystemDefault).ToList();
        var protectedBlobs = results.Where(r => r.Category == BlobCategory.ProtectedNonThumbnail).ToList();
        var staleBlobs = results.Where(r => r.Category == BlobCategory.OrphanedDeletedPicture || r.Category == BlobCategory.OutdatedSeoName).ToList();

        var totalActiveBytes = activeBlobs.Sum(r => r.SizeBytes);
        var totalProtectedBytes = protectedBlobs.Sum(r => r.SizeBytes);
        var totalStaleBytes = staleBlobs.Sum(r => r.SizeBytes);

        Console.WriteLine($"\n  Total Blobs Scanned : {results.Count,8:N0} ({FormatBytes(totalBytesScanned)})");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Active Thumbnails   : {activeBlobs.Count,8:N0} ({FormatBytes(totalActiveBytes)})");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Protected Non-Thumbs: {protectedBlobs.Count,8:N0} ({FormatBytes(totalProtectedBytes)}) (Backups, XML feeds, etc. - Kept)");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Stale/Orphaned Blobs: {staleBlobs.Count,8:N0} ({FormatBytes(totalStaleBytes)}) (Safe to delete)");
        Console.ResetColor();

        Console.WriteLine("\n  Breakdown of Stale Blobs:");
        var breakdown = staleBlobs.GroupBy(r => r.Reason).OrderByDescending(g => g.Count());
        foreach (var group in breakdown)
        {
            var groupBytes = group.Sum(x => x.SizeBytes);
            Console.WriteLine($"    - {group.Key,-50}: {group.Count(),6:N0} blobs ({FormatBytes(groupBytes)})");
        }

        // Export report
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "azure_image_cleanup_report.csv");
        await using (var writer = new StreamWriter(reportPath))
        {
            await writer.WriteLineAsync("BlobName,SizeBytes,SizeFormatted,LastModifiedUtc,Category,Reason");
            foreach (var r in results)
            {
                await writer.WriteLineAsync($"\"{r.BlobName}\",{r.SizeBytes},\"{FormatBytes(r.SizeBytes)}\",\"{r.LastModified:u}\",{r.Category},\"{r.Reason}\"");
            }
        }
        Console.WriteLine($"\n  Detailed audit log written to:\n  -> {reportPath}");

        // 4. Deletion phase (if requested)
        if (isDeleteMode)
        {
            if (staleBlobs.Count == 0)
            {
                Console.WriteLine("\nNo stale blobs to delete.");
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[4/4] DELETING {staleBlobs.Count:N0} stale blobs from Azure container '{containerName}'...");
            Console.ResetColor();

            int deletedCount = 0;
            long deletedBytes = 0;

            foreach (var stale in staleBlobs)
            {
                var deleted = await containerClient.DeleteBlobIfExistsAsync(stale.BlobName, DeleteSnapshotsOption.IncludeSnapshots);
                if (deleted.Value)
                {
                    deletedCount++;
                    deletedBytes += stale.SizeBytes;
                }

                if (deletedCount % 200 == 0 || deletedCount == staleBlobs.Count)
                {
                    Console.Write($"\r -> Deleted {deletedCount:N0} / {staleBlobs.Count:N0} blobs ({FormatBytes(deletedBytes)} reclaimed)...");
                }
            }

            Console.WriteLine($"\n\nSuccess! Reclaimed {FormatBytes(deletedBytes)} by deleting {deletedCount:N0} stale blobs.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[4/4] DRY RUN COMPLETE - No files were modified or deleted.");
            Console.WriteLine("To perform the actual deletion, run with the --delete flag.");
            Console.ResetColor();
        }

        return 0;
    }

    private static BlobAnalysisResult AnalyzeBlob(string blobName, long size, DateTimeOffset? lastModified, Dictionary<int, (string SeoFilename, string MimeType)> activePictures)
    {
        if (blobName.StartsWith("default-", StringComparison.OrdinalIgnoreCase))
        {
            return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.SystemDefault, "System Default Image");
        }

        var withoutExt = Path.GetFileNameWithoutExtension(blobName);
        var ext = Path.GetExtension(blobName).TrimStart('.').ToLowerInvariant();

        // If not a standard 7-digit prefix, protect it (could be backups, feeds, custom static assets)
        if (withoutExt.Length < 7 || !int.TryParse(withoutExt[..7], out var id))
        {
            return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.ProtectedNonThumbnail, "Protected Asset (Manual file, backup, or feed - skipped)");
        }

        // Check if Picture ID exists in database
        if (!activePictures.TryGetValue(id, out var pictureInfo))
        {
            return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.OrphanedDeletedPicture, "Deleted Picture (ID not in database)");
        }

        var (dbSeo, dbMime) = pictureInfo;
        var remainder = withoutExt.Length > 7 && withoutExt[7] == '_' ? withoutExt[8..] : string.Empty;

        // Case 1: No SEO in blob or DB (e.g. 0000123.jpg or 0000123_100.jpg)
        if (string.IsNullOrEmpty(remainder) || int.TryParse(remainder, out _))
        {
            if (string.IsNullOrEmpty(dbSeo))
            {
                return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.Active, "Active Thumbnail");
            }
            else
            {
                return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.OutdatedSeoName, $"Missing current SEO slug '{dbSeo}'");
            }
        }

        // Case 2: Exact SEO match (raw image, size = 0, e.g. 0000123_my-seo.jpg)
        if (string.Equals(remainder, dbSeo, StringComparison.OrdinalIgnoreCase))
        {
            return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.Active, "Active Thumbnail");
        }

        // Case 3: Resized image with SEO (e.g. 0000123_my-seo_450.jpg)
        if (!string.IsNullOrEmpty(dbSeo) && remainder.StartsWith(dbSeo + "_", StringComparison.OrdinalIgnoreCase))
        {
            var sizePart = remainder[(dbSeo.Length + 1)..];
            if (int.TryParse(sizePart, out _))
            {
                return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.Active, "Active Thumbnail");
            }
        }

        // Case 4: SEO slug mismatch (product name or SEO name was changed)
        return new BlobAnalysisResult(blobName, size, lastModified, BlobCategory.OutdatedSeoName, $"Outdated SEO slug (Current: '{dbSeo}', Blob remainder: '{remainder}')");
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {suffixes[order]}";
    }
}

public enum BlobCategory
{
    Active,
    SystemDefault,
    ProtectedNonThumbnail,
    OrphanedDeletedPicture,
    OutdatedSeoName,
    Unrecognized
}

public record BlobAnalysisResult(string BlobName, long SizeBytes, DateTimeOffset? LastModified, BlobCategory Category, string Reason);
