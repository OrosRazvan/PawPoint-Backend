using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using System.Linq.Expressions;

namespace PawPoint.Services.Services
{
    public sealed class ContactMessageService(
    Context db,
    IEmailService emailService,
    ITemplateRenderer templateRenderer,
    INotificationService notificationService,
    IPiiEncryptionService pii) : IContactMessageService
    {
        private readonly Context _db = db;
        private readonly IEmailService _emailService = emailService;
        private readonly ITemplateRenderer _templateRenderer = templateRenderer;
        private readonly INotificationService _notificationService = notificationService;
        private readonly IPiiEncryptionService _pii = pii;

        public async Task<ContactMessageItemResponse> CreateAsync(int userId, CreateContactMessageRequest request)
        {
            ValidateCreateRequest(request);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            var message = new ContactMessage
            {
                UserId = userId,
                Email = user.Email,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ContactMessages.Add(message);
            await _db.SaveChangesAsync();

            await NotifyAdminsAboutNewMessageAsync(user.FullName, message.Title, message.Id);

            return await GetByIdAsync(message.Id);
        }

        public async Task<IReadOnlyList<ContactMessageItemResponse>> GetMyMessagesAsync(int userId)
        {
            return await _db.ContactMessages
                .Include(x => x.User)
                .Include(x => x.Replies)
                    .ThenInclude(r => r.SenderUser)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapToResponse())
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ContactMessageItemResponse>> GetAllAsync()
        {
            return await _db.ContactMessages
                .IgnoreQueryFilters()
                .Include(x => x.User)
                .Include(x => x.Replies)
                    .ThenInclude(r => r.SenderUser)
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapToResponse())
                .ToListAsync();
        }

        public async Task<ContactMessageItemResponse> GetByIdAsync(int id)
        {
            var item = await _db.ContactMessages
                .IgnoreQueryFilters()
                .Include(x => x.User)
                .Include(x => x.Replies)
                    .ThenInclude(r => r.SenderUser)
                .Where(x => x.Id == id)
                .Select(MapToResponse())
                .FirstOrDefaultAsync();

            if (item is null)
                throw new InvalidOperationException("Contact message not found.");

            return item;
        }

        public async Task<ContactMessageReplyResponse> ReplyAsAdminAsync(int adminUserId, int messageId, ReplyContactMessageRequest request)
        {
            ValidateReplyRequest(request);

            var admin = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == adminUserId);

            if (admin is null)
                throw new InvalidOperationException("Admin user not found.");

            var message = await _db.ContactMessages
                .IgnoreQueryFilters()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == messageId);

            if (message is null)
                throw new InvalidOperationException("Contact message not found.");

            var reply = new ContactMessageReply
            {
                ContactMessageId = message.Id,
                SenderUserId = adminUserId,
                SenderType = "Admin",
                Message = request.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            message.Status = "Answered";
            message.UpdatedAt = DateTime.UtcNow;

            _db.ContactMessageReplies.Add(reply);
            await _db.SaveChangesAsync();

            var toEmail = DecryptOrRaw(message.Email);
            await TrySendReplyEmailAsync(toEmail, message.Title, request.Message.Trim());

            await _notificationService.CreateNotificationAsync(
                message.UserId,
                new NotificationCreateRequest(
                    Type: NotificationTypeEnum.ContactMessageReplyReceived,
                    UserId: message.UserId,
                    Title: "PawPoint support replied to your message",
                    Content: $"Subject: {message.Title}"
                ),
                systemRun: true
            );

            return new ContactMessageReplyResponse(
                Id: reply.Id,
                SenderUserId: reply.SenderUserId,
                SenderType: reply.SenderType,
                SenderName: admin.FullName,
                Message: reply.Message,
                CreatedAt: reply.CreatedAt
            );
        }

        private static void ValidateCreateRequest(CreateContactMessageRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required.", nameof(request.Title));

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required.", nameof(request.Description));

            if (request.Title.Trim().Length > 200)
                throw new ArgumentException("Title too long.", nameof(request.Title));

            if (request.Description.Trim().Length > 4000)
                throw new ArgumentException("Description too long.", nameof(request.Description));
        }

        private static void ValidateReplyRequest(ReplyContactMessageRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Message))
                throw new ArgumentException("Reply message is required.", nameof(request.Message));

            if (request.Message.Trim().Length > 4000)
                throw new ArgumentException("Reply too long.", nameof(request.Message));
        }

        private static Expression<Func<ContactMessage, ContactMessageItemResponse>> MapToResponse()
        {
            return x => new ContactMessageItemResponse(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.Email,
                x.Title,
                x.Description,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.Replies
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new ContactMessageReplyResponse(
                        r.Id,
                        r.SenderUserId,
                        r.SenderType,
                        r.SenderUser.FullName,
                        r.Message,
                        r.CreatedAt
                    ))
                    .ToList()
            );
        }

        private async Task TrySendReplyEmailAsync(string toEmail, string originalTitle, string replyText)
        {
            try
            {
                var html = $"""
                    <div style="font-family:Arial,sans-serif;">
                        <h2>PawPoint Support Reply</h2>
                        <p><strong>Regarding:</strong> {originalTitle}</p>
                        <p>{replyText}</p>
                    </div>
                    """;

                var text = $"PawPoint Support Reply\n\nRegarding: {originalTitle}\n\n{replyText}";

                await _emailService.SendEmailAsync(
                    toEmail: toEmail,
                    title: $"Reply to your PawPoint message: {originalTitle}",
                    textBody: text,
                    htmlBody: html
                );
            }
            catch
            {
            }
        }

        private async Task NotifyAdminsAboutNewMessageAsync(string userFullName, string title, int messageId)
        {
            var adminUsers = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.Role == UserRoleEnum.Admin)
                .ToListAsync();

            foreach (var admin in adminUsers)
            {
                await _notificationService.CreateNotificationAsync(
                    admin.Id,
                    new NotificationCreateRequest(
                        Type: NotificationTypeEnum.ContactMessageReceived,
                        UserId: admin.Id,
                        Title: "New contact message received",
                        Content: $"{userFullName}: {title} (#{messageId})"
                    ),
                    systemRun: true
                );
            }
        }

        private string DecryptOrRaw(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return _pii.Decrypt(value);
            }
            catch
            {
                return value;
            }
        }
    }
}