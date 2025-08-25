using ShipMvp.Domain.Integrations;
using ShipMvp.Core.Security;
using Microsoft.Extensions.Logging;

namespace ShipMvp.Domain.Integrations;

public interface IIntegrationManager
{
    Task<Integration?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Integration?> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Integration>> GetByIntegrationTypeAsync(IntegrationType type, CancellationToken cancellationToken = default);
    Task<IntegrationCredential> CreateOrUpdateGenericCredentialAsync(
        Guid userId, 
        Guid integrationId, 
        string userInfo, 
        Dictionary<string, string> credentials,
        CancellationToken cancellationToken = default);
    
    // New methods for GmailApiManager
    Task<IEnumerable<IntegrationCredential>> GetUserCredentialsAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);
    Task<IntegrationCredential?> GetCredentialByIdAsync(Guid credentialId, CancellationToken cancellationToken = default);
    Task UpdateCredentialsAsync(Guid credentialId, Dictionary<string, string> credentials, CancellationToken cancellationToken = default);
}

public class IntegrationManager : IIntegrationManager
{
    private readonly IIntegrationRepository _repository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<IntegrationManager> _logger;

    public IntegrationManager(IIntegrationRepository repository, IEncryptionService encryptionService, ILogger<IntegrationManager> logger)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public Task<Integration?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _repository.GetByNameAsync(name, cancellationToken);
    }

    public Task<Integration?> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        return _repository.GetByTypeAsync(type, cancellationToken);
    }

    public Task<IEnumerable<Integration>> GetByIntegrationTypeAsync(IntegrationType type, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIntegrationTypeAsync(type, cancellationToken);
    }

    public async Task<IntegrationCredential> CreateOrUpdateGenericCredentialAsync(
        Guid userId, 
        Guid integrationId, 
        string userInfo, 
        Dictionary<string, string> credentials,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntegrationManager: Starting CreateOrUpdateGenericCredentialAsync for user {UserId}, integration {IntegrationId}, userInfo {UserInfo}", 
            userId, integrationId, userInfo);

        var existing = await _repository.GetCredentialByUserInfoAndIntegrationIdAsync(userInfo, integrationId, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("IntegrationManager: Found existing credential with ID {CredentialId}, updating generic credentials", existing.Id);
            
            // Update each credential field
            foreach (var kvp in credentials)
            {
                var isEncrypted = ShouldEncryptField(kvp.Key);
                existing.SetCredentialField(kvp.Key, kvp.Value, isEncrypted);
            }
            
            // Encrypt sensitive fields
            await EncryptCredentialFieldsAsync(existing);
            
            _logger.LogInformation("IntegrationManager: Calling repository to update credential {CredentialId}", existing.Id);
            await _repository.UpdateCredentialAsync(existing, cancellationToken);
            _logger.LogInformation("IntegrationManager: Successfully updated credential {CredentialId}", existing.Id);
            return existing;
        }
        else
        {
            _logger.LogInformation("IntegrationManager: No existing credential found, creating new one");
            
            var newCredential = IntegrationCredential.Create(
                userId: userId,
                integrationId: integrationId,
                userInfo: userInfo
            );
            
            // Set each credential field
            foreach (var kvp in credentials)
            {
                var isEncrypted = ShouldEncryptField(kvp.Key);
                newCredential.SetCredentialField(kvp.Key, kvp.Value, isEncrypted);
            }
            
            _logger.LogInformation("IntegrationManager: Created new credential with ID {CredentialId}", newCredential.Id);
            
            // Encrypt sensitive fields
            await EncryptCredentialFieldsAsync(newCredential);
            
            _logger.LogInformation("IntegrationManager: Calling repository to add new credential {CredentialId}", newCredential.Id);
            await _repository.AddCredentialAsync(newCredential, cancellationToken);
            _logger.LogInformation("IntegrationManager: Successfully added new credential {CredentialId}", newCredential.Id);
            return newCredential;
        }
    }

    public async Task<IEnumerable<IntegrationCredential>> GetUserCredentialsAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntegrationManager: Getting credentials for user {UserId}, integration {IntegrationId}", userId, integrationId);
        
        var credential = await _repository.GetByUserAndIntegrationAsync(userId, integrationId, cancellationToken);
        if (credential == null)
        {
            _logger.LogInformation("IntegrationManager: No credentials found for user {UserId}, integration {IntegrationId}", userId, integrationId);
            return Enumerable.Empty<IntegrationCredential>();
        }

        // Decrypt sensitive fields for return
        await DecryptCredentialFieldsAsync(credential);
        
        return new[] { credential };
    }

    public async Task<IntegrationCredential?> GetCredentialByIdAsync(Guid credentialId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntegrationManager: Getting credential by ID {CredentialId}", credentialId);
        
        var credential = await _repository.GetCredentialByIdAsync(credentialId, cancellationToken);
        
        if (credential == null)
        {
            _logger.LogWarning("IntegrationManager: Credential {CredentialId} not found", credentialId);
            return null;
        }

        // Decrypt sensitive fields for return
        await DecryptCredentialFieldsAsync(credential);
        
        return credential;
    }

    public async Task UpdateCredentialsAsync(Guid credentialId, Dictionary<string, string> credentials, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntegrationManager: Updating credentials for credential {CredentialId}", credentialId);
        
        var credential = await GetCredentialByIdAsync(credentialId, cancellationToken);
        if (credential == null)
        {
            throw new InvalidOperationException($"Credential with ID {credentialId} not found");
        }

        // Update each credential field
        foreach (var kvp in credentials)
        {
            var isEncrypted = ShouldEncryptField(kvp.Key);
            credential.SetCredentialField(kvp.Key, kvp.Value, isEncrypted);
        }
        
        // Encrypt sensitive fields
        await EncryptCredentialFieldsAsync(credential);
        
        await _repository.UpdateCredentialAsync(credential, cancellationToken);
        _logger.LogInformation("IntegrationManager: Successfully updated credentials for credential {CredentialId}", credentialId);
    }

    private bool ShouldEncryptField(string fieldKey)
    {
        // Define which fields should be encrypted
        var encryptedFields = new[]
        {
            "access_token", "refresh_token", "api_key", "client_secret", 
            "deployment", "endpoint", "organization", "api_secret"
        };
        
        return encryptedFields.Contains(fieldKey.ToLowerInvariant());
    }

    private async Task EncryptCredentialFieldsAsync(IntegrationCredential credential)
    {
        foreach (var field in credential.CredentialFields.Where(f => f.IsEncrypted && !string.IsNullOrEmpty(f.Value)))
        {
            field.Value = _encryptionService.Encrypt(field.Value);
        }
    }

    private async Task DecryptCredentialFieldsAsync(IntegrationCredential credential)
    {
        foreach (var field in credential.CredentialFields.Where(f => f.IsEncrypted && !string.IsNullOrEmpty(f.Value)))
        {
            try
            {
                field.Value = _encryptionService.Decrypt(field.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt credential field {FieldKey} for credential {CredentialId}", field.Key, credential.Id);
                // Don't throw, just log the error and leave the field encrypted
            }
        }
    }
}