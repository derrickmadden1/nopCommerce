using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;
using SkiaSharp;

namespace DatabaseImageMigrator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var isExecute = args.Any(a => a.Equals("--execute", StringComparison.OrdinalIgnoreCase));
        var purgeAzureThumbs = args.Any(a => a.Equals("--purge-azure-thumbs", StringComparison.OrdinalIgnoreCase));

        int quality = 80;
        int maxSize = 1980;
        string dbName = "rcc_azure_20261112";
        string explicitConnString = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--conn", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) explicitConnString = args[i + 1];
            if (args[i].Equals("--quality", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var q)) quality = q;
            if (args[i].Equals("--max-size", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var ms)) maxSize = ms;
            if (args[i].Equals("--db", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) dbName = args[i + 1];
        }

        var sqlConnString = !string.IsNullOrWhiteSpace(explicitConnString)
            ? explicitConnString
            : $"Server=DERRICK-SURFACE\\SQLEXPRESS;Database={dbName};Integrated Security=SSPI;TrustServerCertificate=True";

        // Extract a safe display name for console logging
        var displayTarget = !string.IsNullOrWhiteSpace(explicitConnString)
            ? new SqlConnectionStringBuilder(explicitConnString).InitialCatalog + " on " + new SqlConnectionStringBuilder(explicitConnString).DataSource
            : dbName;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine(" nopCommerce Database Image WebP Converter & Migration Tool");
        Console.WriteLine($" Target Database: {displayTarget}");
        Console.WriteLine($" Mode           : {(isExecute ? "LIVE EXECUTION (Updates DB)" : "DRY RUN (Simulation only - NO database writes)")}");
        Console.WriteLine($" Quality        : {quality} | Max Dimension: {maxSize}px");
        Console.WriteLine($" Purge Azure    : {(purgeAzureThumbs && isExecute ? "YES (Will delete old Azure thumbs)" : "NO")}");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        // 1. Fetch images needing conversion
        Console.WriteLine("\n[1/3] Loading non-WebP pictures from database...");
        var picturesToConvert = new List<PictureRecord>();
        string azureConnString = null;
        string containerName = "rosecottagestore";

        await using (var conn = new SqlConnection(sqlConnString))
        {
            await conn.OpenAsync();

            // Load Azure settings (in case thumb purging is requested)
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

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT p.Id, p.MimeType, ISNULL(p.SeoFilename, ''), pb.BinaryData
                    FROM [Picture] p
                    INNER JOIN [PictureBinary] pb ON p.Id = pb.PictureId
                    WHERE p.MimeType NOT IN ('image/webp', 'image/svg+xml')
                    ORDER BY p.Id ASC";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var mime = reader.GetString(1);
                    var seo = reader.GetString(2);
                    var binary = (byte[])reader[3];

                    picturesToConvert.Add(new PictureRecord(id, mime, seo, binary));
                }
            }
        }

        Console.WriteLine($" -> Found {picturesToConvert.Count:N0} pictures to convert to WebP.");
        if (picturesToConvert.Count == 0)
        {
            Console.WriteLine("All pictures are already WebP or SVG. Nothing to do.");
            return 0;
        }

        // Initialize Azure client if purging requested
        BlobContainerClient containerClient = null;
        if (isExecute && purgeAzureThumbs && !string.IsNullOrWhiteSpace(azureConnString))
        {
            var blobServiceClient = new BlobServiceClient(azureConnString);
            containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        // 2. Perform Conversion Simulation / Execution
        Console.WriteLine($"\n[2/3] {(isExecute ? "Converting and updating database" : "Simulating conversion in memory")}...");

        var results = new List<ConversionResult>();
        long totalOriginalBytes = 0;
        long totalWebpBytes = 0;
        int successCount = 0;
        int failedCount = 0;

        for (int i = 0; i < picturesToConvert.Count; i++)
        {
            var pic = picturesToConvert[i];
            totalOriginalBytes += pic.BinaryData.Length;

            try
            {
                var webpBytes = ConvertToWebp(pic.BinaryData, maxSize, quality);
                totalWebpBytes += webpBytes.Length;
                successCount++;

                var savings = pic.BinaryData.Length - webpBytes.Length;
                var pct = (double)savings / pic.BinaryData.Length * 100.0;

                results.Add(new ConversionResult(pic.Id, pic.SeoFilename, pic.MimeType, pic.BinaryData.Length, webpBytes.Length, savings, pct, true, "OK"));

                // If execute mode, update DB and purge Azure thumbs
                if (isExecute)
                {
                    await using var updateConn = new SqlConnection(sqlConnString);
                    await updateConn.OpenAsync();

                    await using var updateCmd = updateConn.CreateCommand();
                    updateCmd.CommandText = @"
                        UPDATE [PictureBinary] SET BinaryData = @webpData WHERE PictureId = @id;
                        UPDATE [Picture] SET MimeType = 'image/webp' WHERE Id = @id;";
                    updateCmd.Parameters.Add(new SqlParameter("@webpData", webpBytes));
                    updateCmd.Parameters.Add(new SqlParameter("@id", pic.Id));
                    await updateCmd.ExecuteNonQueryAsync();

                    // Purge legacy thumbs in Azure for this picture
                    if (containerClient != null)
                    {
                        var prefix = $"{pic.Id:0000000}";
                        await foreach (var blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, default))
                        {
                            if (!blob.Name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            {
                                await containerClient.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                totalWebpBytes += pic.BinaryData.Length;
                results.Add(new ConversionResult(pic.Id, pic.SeoFilename, pic.MimeType, pic.BinaryData.Length, pic.BinaryData.Length, 0, 0, false, ex.Message));
            }

            if ((i + 1) % 50 == 0 || i + 1 == picturesToConvert.Count)
            {
                Console.Write($"\r -> Processed {i + 1:N0} / {picturesToConvert.Count:N0} images...");
            }
        }

        Console.WriteLine("\n -> Processing complete!");

        // 3. Summarize findings & Export report
        var totalSavings = totalOriginalBytes - totalWebpBytes;
        var totalPct = totalOriginalBytes > 0 ? (double)totalSavings / totalOriginalBytes * 100.0 : 0;

        Console.WriteLine("\n[3/3] Migration Summary:");
        Console.WriteLine($"  Total Pictures Evaluated : {picturesToConvert.Count,8:N0}");
        Console.WriteLine($"  Successful Conversions   : {successCount,8:N0}");
        if (failedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed Images            : {failedCount,8:N0}");
            Console.ResetColor();
        }
        Console.WriteLine($"  Original Database Size   : {FormatBytes(totalOriginalBytes),10}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  New WebP Database Size   : {FormatBytes(totalWebpBytes),10}");
        Console.WriteLine($"  Total Space Saved        : {FormatBytes(totalSavings),10} ({totalPct:F1}% reduction)");
        Console.ResetColor();

        // Write report
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "database_webp_migration_report.csv");
        await using (var writer = new StreamWriter(reportPath))
        {
            await writer.WriteLineAsync("PictureId,SeoFilename,OriginalMimeType,OriginalBytes,WebpBytes,SavingsBytes,SavingsPercent,Success,Notes");
            foreach (var r in results)
            {
                await writer.WriteLineAsync($"{r.PictureId},\"{r.SeoFilename}\",\"{r.OriginalMimeType}\",{r.OriginalBytes},{r.WebpBytes},{r.SavingsBytes},{r.SavingsPercent:F1},{r.Success},\"{r.Notes.Replace("\"", "\"\"")}\"");
            }
        }
        Console.WriteLine($"\n  Detailed migration log written to:\n  -> {reportPath}");

        if (!isExecute)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n==================================================================");
            Console.WriteLine(" DRY RUN FINISHED: No database or Azure changes were made.");
            Console.WriteLine(" To perform live database conversion, run with: --execute");
            Console.WriteLine(" To also purge old JPEG/PNG thumbs in Azure:    --execute --purge-azure-thumbs");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
        }

        return 0;
    }

    private static byte[] ConvertToWebp(byte[] binaryData, int maxSize, int quality)
    {
        using var stream = new MemoryStream(binaryData);
        using var codec = SKCodec.Create(stream);

        SKBitmap bitmap;
        if (codec != null)
        {
            var origin = codec.EncodedOrigin;
            var decoded = SKBitmap.Decode(codec);
            bitmap = origin != SKEncodedOrigin.TopLeft ? AutoOrient(decoded, origin) : decoded;
        }
        else
        {
            bitmap = SKBitmap.Decode(binaryData);
        }

        if (bitmap == null)
            throw new Exception("Unable to decode image bytes.");

        using (bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            // Resize if oversized
            if (Math.Max(width, height) > maxSize)
            {
                float scale = (float)maxSize / Math.Max(width, height);
                width = Math.Max(1, (int)(width * scale));
                height = Math.Max(1, (int)(height * scale));

                var samplingOption = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                using var resized = bitmap.Resize(new SKImageInfo(width, height), samplingOption);
                using var skImage = SKImage.FromBitmap(resized);
                using var data = skImage.Encode(SKEncodedImageFormat.Webp, quality);
                return data.ToArray();
            }
            else
            {
                using var skImage = SKImage.FromBitmap(bitmap);
                using var data = skImage.Encode(SKEncodedImageFormat.Webp, quality);
                return data.ToArray();
            }
        }
    }

    private static SKBitmap AutoOrient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        SKBitmap rotated;
        switch (origin)
        {
            case SKEncodedOrigin.BottomRight:
                using (var surface = new SKCanvas(bitmap))
                {
                    surface.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    surface.DrawBitmap(bitmap.Copy(), 0, 0, SKSamplingOptions.Default);
                }
                return bitmap;
            case SKEncodedOrigin.RightTop:
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(rotated.Width, 0);
                    surface.RotateDegrees(90);
                    surface.DrawBitmap(bitmap.Copy(), 0, 0, SKSamplingOptions.Default);
                }
                return rotated;
            case SKEncodedOrigin.LeftBottom:
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(0, rotated.Height);
                    surface.RotateDegrees(270);
                    surface.DrawBitmap(bitmap.Copy(), 0, 0, SKSamplingOptions.Default);
                }
                return rotated;
            default:
                return bitmap;
        }
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

public record PictureRecord(int Id, string MimeType, string SeoFilename, byte[] BinaryData);

public record ConversionResult(int PictureId, string SeoFilename, string OriginalMimeType, long OriginalBytes, long WebpBytes, long SavingsBytes, double SavingsPercent, bool Success, string Notes);
