using Microsoft.AspNetCore.Http;
using System;
// Local lightweight DTO to avoid direct dependency on EmailMessages module

namespace ShipMvp.Application.Email;

/// <summary>
/// DTO for draft email data returned to the UI for review
/// </summary>
public record DraftEmailDto(
    Guid? Id,
    string To,
    string Subject,
    string Body,
    string? Cc = null,
    string? Bcc = null,
    string? FromName = null,
    DateTime? FollowUpAt = null,
    string? LabelIds = null);

/// <summary>
/// DTO for generating email content
/// </summary>
public record GenerateEmailDto(
    string? CustomPrompt,
    string? Website); 

/// <summary>
/// DTO for sending email
/// </summary>
public record SendEmailDto(
    Guid ProjectId,
    DraftEmailDto Draft);

/// <summary>
/// DTO for manually saving email drafts
/// </summary>
public record SaveDraftEmailDto(
    Guid ProjectId,
    DraftEmailDto Draft,
    Guid? AttachmentId = null);

/// <summary>
/// DTO for Gmail metadata associated with an email message
/// </summary>
public record EmailGmailMetadataDto(
    string? GmailMessageId,
    string? GmailThreadId,
    string? LabelIds = null)
{
    /// <summary>
    /// Direct Gmail URL to this message
    /// </summary>
    public string? GmailMessageUrl => !string.IsNullOrEmpty(GmailMessageId) 
        ? $"https://mail.google.com/mail/u/0/#all/{GmailMessageId}" 
        : null;
    
    /// <summary>
    /// Gmail thread URL for conversation view
    /// </summary>
    public string? GmailThreadUrl => !string.IsNullOrEmpty(GmailThreadId) 
        ? $"https://mail.google.com/mail/u/0/#all/{GmailThreadId}" 
        : null;
}

/// <summary>
/// DTO for processing incoming emails
/// </summary>
public record IncomingEmailDto(
    string MessageId,
    string Subject,
    string FromAddress,
    string ToAddresses,
    DateTime ReceivedAt,
    string? ThreadId = null,
    string? InReplyToMessageId = null,
    string? References = null,
    string? FromName = null,
    string? CcAddresses = null,
    string? BccAddresses = null,
    string? BodyHtml = null,
    string? BodyText = null,
    List<ComposerEmailAttachmentDto>? Attachments = null);

/// <summary>
/// Minimal attachment DTO used solely by the composer to avoid circular project references.
/// </summary>
public record ComposerEmailAttachmentDto(
    Guid Id,
    string FileName,
    string? ContentType = null,
    int SizeInBytes = 0);

/// <summary>
/// Result of processing an incoming email
/// </summary>
public record IncomingEmailProcessResult(
    bool WasProcessed,
    string? Reason = null,
    Guid? CreatedEmailId = null,
    Guid? OriginalEmailId = null,
    bool IsReply = false,
    string? ProcessingDetails = null);

/// <summary>
/// Gmail push notification DTO from Google Cloud Pub/Sub
/// </summary>
public record GmailPushNotificationDto(
    GmailPushMessageDto? Message,
    string? Subscription);

/// <summary>
/// Gmail push message DTO containing the base64-encoded data
/// </summary>
public record GmailPushMessageDto(
    string? Data,
    Dictionary<string, string>? Attributes,
    string? MessageId,
    string? PublishTime);

/// <summary>
/// Decoded Gmail push notification data
/// </summary>
public record GmailPushData(
    string? EmailAddress,
    string? HistoryId);

/// <summary>
/// Gmail message data fetched from Gmail API
/// </summary>
public record GmailMessageDto(
    string Id,
    string ThreadId,
    List<string>? LabelIds,
    string? Snippet,
    GmailPayloadDto? Payload,
    long? InternalDate,
    long? HistoryId);

/// <summary>
/// Gmail message payload containing headers and body
/// </summary>
public record GmailPayloadDto(
    string? PartId,
    string? MimeType,
    string? Filename,
    List<GmailHeaderDto>? Headers,
    GmailBodyDto? Body,
    List<GmailPayloadDto>? Parts);

/// <summary>
/// Gmail message header
/// </summary>
public record GmailHeaderDto(
    string? Name,
    string? Value);

/// <summary>
/// Gmail message body data
/// </summary>
public record GmailBodyDto(
    string? AttachmentId,
    int? Size,
    string? Data);

/// <summary>
/// Gmail history changes response
/// </summary>
public record GmailHistoryChanges(
    List<string> NewMessageIds,
    List<string> DeletedMessageIds);

/// <summary>
/// Gmail integration information for processing
/// </summary>
public record GmailIntegrationInfo(
    string Email,
    string AccessToken,
    Guid UserId,
    Guid CredentialId);
