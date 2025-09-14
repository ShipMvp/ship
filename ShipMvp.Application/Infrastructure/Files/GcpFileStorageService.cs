
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShipMvp.Domain.Files;
using Google.Cloud.Storage.V1;
using ShipMvp.Application.Infrastructure.Gcp;
using ShipMvp.Core.Abstractions;

namespace ShipMvp.Application.Infrastructure.Files;

/// <summary>
/// Google Cloud Storage implementation of IFileStorageService
/// </summary>
public class GcpFileStorageService : IFileStorageService
{

    private readonly ILogger<GcpFileStorageService> _logger;
    private readonly string _projectId;
    private readonly string _defaultBucket;
    private readonly StorageClient _storageClient;
    private readonly string? _credentialsPath;

    public GcpFileStorageService(
        IConfiguration configuration,
        ILogger<GcpFileStorageService> logger)
    {
        _logger = logger;
        _projectId = configuration["Gcp:ProjectId"] ?? "demo-project";
        _defaultBucket = configuration["Gcp:Storage:DefaultBucket"] ?? "shipmvp-files";
        _credentialsPath = configuration["Gcp:CredentialsPath"];

        _logger.LogInformation("Initializing GCP Storage Service - Project: {ProjectId}, Bucket: {DefaultBucket}, CredentialsPath: {CredentialsPath}",
            _projectId, _defaultBucket, _credentialsPath ?? "Default credentials");

        try
        {
            var credential = GcpCredentialFactory.Create(configuration);
            _storageClient = StorageClient.Create(credential);
            _logger.LogInformation("GCP Storage Client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GCP Storage Client. Check credentials configuration.");
            throw new InvalidOperationException("Failed to initialize GCP Storage Client", ex);
        }
    }

    public async Task<string> UploadAsync(
        string containerName,
        string fileName,
        Stream fileStream,
        string contentType,
        bool isPublic = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading file to GCS: {Container}/{FileName}", containerName, fileName);
            var bucketName = containerName ?? _defaultBucket;
            await _storageClient.UploadObjectAsync(
                bucketName,
                fileName,
                contentType,
                fileStream,
                cancellationToken: cancellationToken
            );
            var storagePath = $"gs://{bucketName}/{fileName}";
            _logger.LogInformation("File uploaded successfully to GCS: {StoragePath}", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to GCS: {Container}/{FileName}", containerName, fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(
        string containerName,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading file from GCS: {Container}/{FileName}", containerName, fileName);

            // Parse GCS URI if fileName contains gs:// prefix
            var (bucketName, objectName) = ParseGcsPath(containerName, fileName);

            _logger.LogInformation("Parsed GCS path - Bucket: {Bucket}, Object: {Object}", bucketName, objectName);

            // First check if the object exists to provide better error messages
            try
            {
                var obj = await _storageClient.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken);
                _logger.LogInformation("Object found: {ObjectName}, Size: {Size} bytes", obj.Name, obj.Size);
            }
            catch (Google.GoogleApiException gex) when (gex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Object not found in GCS: {Bucket}/{Object}", bucketName, objectName);
                throw new FileNotFoundException($"File not found in GCS: {bucketName}/{objectName}");
            }
            catch (Google.GoogleApiException gex) when (gex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("Authentication failed for GCS access. Check credentials and permissions.");
                throw new UnauthorizedAccessException($"Authentication failed for GCS bucket: {bucketName}. Error: {gex.Message}");
            }
            catch (Google.GoogleApiException gex)
            {
                _logger.LogError(gex, "GCP API error during object check: Status={Status}, Message={Message}",
                    gex.HttpStatusCode, gex.Message);
                throw;
            }

            var ms = new MemoryStream();
            await _storageClient.DownloadObjectAsync(
                bucketName,
                objectName,
                ms,
                cancellationToken: cancellationToken
            );
            ms.Position = 0;
            _logger.LogInformation("Successfully downloaded {Size} bytes from GCS: {Bucket}/{Object}",
                ms.Length, bucketName, objectName);
            return ms;
        }
        catch (Google.GoogleApiException gex)
        {
            _logger.LogError(gex, "GCP API error downloading file: Status={Status}, Message={Message}, InnerException={Inner}",
                gex.HttpStatusCode, gex.Message, gex.InnerException?.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from GCS: {Container}/{FileName}", containerName, fileName);
            throw;
        }
    }

    public async Task DeleteAsync(
        string containerName,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting file from GCS: {Container}/{FileName}", containerName, fileName);
            var bucketName = containerName ?? _defaultBucket;
            await _storageClient.DeleteObjectAsync(
                bucketName,
                fileName,
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("File deleted successfully from GCS: {Container}/{FileName}", containerName, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from GCS: {Container}/{FileName}", containerName, fileName);
            throw;
        }
    }

    public Task<string> GetSignedUrlAsync(
        string containerName,
        string fileName,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = containerName ?? _defaultBucket;
            // For demo: return a gs:// URL (real signed URL requires IAM credentials and URL signer)
            var signedUrl = $"gs://{bucketName}/{fileName}?expires={DateTime.UtcNow.Add(expiration):yyyy-MM-ddTHH:mm:ssZ}";
            _logger.LogInformation("Generated GCS signed URL for: {Container}/{FileName}", containerName, fileName);
            return Task.FromResult(signedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating signed URL for GCS: {Container}/{FileName}", containerName, fileName);
            throw;
        }
    }

    public string GetPublicUrl(string containerName, string fileName)
    {
        var bucketName = containerName ?? _defaultBucket;
        // Return a public GCS URL (if object is public)
        return $"https://storage.googleapis.com/{bucketName}/{fileName}";
    }

    public async Task<bool> ExistsAsync(
        string containerName,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (bucketName, objectName) = ParseGcsPath(containerName, fileName);
            var obj = await _storageClient.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken);
            return obj != null;
        }
        catch (Google.GoogleApiException ex) when (ex.Error.Code == 404)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse GCS path to extract bucket name and object name
    /// Handles both regular paths and gs:// URIs
    /// </summary>
    private (string bucketName, string objectName) ParseGcsPath(string containerName, string fileName)
    {
        // If fileName starts with gs://, parse the URI
        if (fileName.StartsWith("gs://"))
        {
            var uri = new Uri(fileName);
            var bucketName = uri.Host;
            var objectName = uri.LocalPath.TrimStart('/');

            _logger.LogDebug("Parsed GCS URI: {Uri} -> Bucket: {Bucket}, Object: {Object}",
                fileName, bucketName, objectName);

            return (bucketName, objectName);
        }

        // Otherwise use the provided containerName and fileName as-is
        var bucket = containerName ?? _defaultBucket;

        _logger.LogDebug("Using direct path: Container: {Container}, File: {File} -> Bucket: {Bucket}, Object: {Object}",
            containerName, fileName, bucket, fileName);

        return (bucket, fileName);
    }

    /// <summary>
    /// Test method to verify GCP Storage connectivity and credentials
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing GCP Storage connection to bucket: {Bucket}", _defaultBucket);

            // Try to list objects in the bucket (this tests both connectivity and permissions)
            var objects = _storageClient.ListObjectsAsync(_defaultBucket, options: new ListObjectsOptions { PageSize = 1 });
            await foreach (var obj in objects.WithCancellation(cancellationToken))
            {
                _logger.LogInformation("Connection test successful. Found object: {ObjectName}", obj.Name);
                return true;
            }

            _logger.LogInformation("Connection test successful. Bucket is empty but accessible.");
            return true;
        }
        catch (Google.GoogleApiException gex)
        {
            _logger.LogError(gex, "GCP Storage connection test failed: Status={Status}, Message={Message}",
                gex.HttpStatusCode, gex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GCP Storage connection test failed with unexpected error");
            return false;
        }
    }
}
