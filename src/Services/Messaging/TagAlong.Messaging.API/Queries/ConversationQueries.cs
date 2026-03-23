using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Queries;

public record GetConversationByIdQuery(Guid ConversationId) : IQuery<ConversationDto?>;

public class GetConversationByIdQueryHandler : IQueryHandler<GetConversationByIdQuery, ConversationDto?>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserLookupService _userLookup;

    public GetConversationByIdQueryHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IUserLookupService userLookup)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userLookup = userLookup;
    }

    public async Task<Result<ConversationDto?>> Handle(GetConversationByIdQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null) return Result.Success<ConversationDto?>(null);

        var messages = await _messageRepository.GetByConversationIdAsync(conversation.Id, 1, 1, cancellationToken);
        var lastMessage = messages.FirstOrDefault();

        var (senderName, travelerName) = await ResolveNamesAsync(conversation, cancellationToken);
        return Result.Success<ConversationDto?>(MapToDto(conversation, lastMessage, senderName, travelerName));
    }

    private async Task<(string? senderName, string? travelerName)> ResolveNamesAsync(
        Conversation conversation, CancellationToken cancellationToken)
    {
        var senderTask = _userLookup.GetDisplayNameAsync(conversation.SenderId, cancellationToken);
        var travelerTask = _userLookup.GetDisplayNameAsync(conversation.TravelerId, cancellationToken);
        await Task.WhenAll(senderTask, travelerTask);
        return (senderTask.Result, travelerTask.Result);
    }

    private static ConversationDto MapToDto(Conversation conversation, Message? lastMessage,
        string? senderName, string? travelerName)
    {
        MessageDto? lastMessageDto = lastMessage != null
            ? new MessageDto(
                lastMessage.Id,
                lastMessage.ConversationId,
                lastMessage.SenderId,
                lastMessage.Content,
                lastMessage.MessageType.ToString(),
                lastMessage.ProposedPrice,
                lastMessage.SentAt,
                lastMessage.ReadAt)
            : null;

        return new ConversationDto(
            conversation.Id,
            conversation.PackageRequestId,
            conversation.SenderId,
            conversation.TravelerId,
            senderName,
            travelerName,
            conversation.Status.ToString(),
            conversation.CreatedAt,
            conversation.UpdatedAt,
            lastMessageDto);
    }
}

public record GetUserConversationsQuery(Guid UserId, int Page, int PageSize) : IQuery<IEnumerable<ConversationDto>>;

public class GetUserConversationsQueryHandler : IQueryHandler<GetUserConversationsQuery, IEnumerable<ConversationDto>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserLookupService _userLookup;

    public GetUserConversationsQueryHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IUserLookupService userLookup)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userLookup = userLookup;
    }

    public async Task<Result<IEnumerable<ConversationDto>>> Handle(GetUserConversationsQuery request, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetByUserIdAsync(request.UserId, request.Page, request.PageSize, cancellationToken);

        // Collect unique user IDs and resolve all names in parallel
        var userIds = conversations
            .SelectMany(c => new[] { c.SenderId, c.TravelerId })
            .Distinct()
            .ToList();

        var nameTasks = userIds.ToDictionary(
            id => id,
            id => _userLookup.GetDisplayNameAsync(id, cancellationToken));
        await Task.WhenAll(nameTasks.Values);
        var names = nameTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

        var result = new List<ConversationDto>();
        foreach (var conversation in conversations)
        {
            var messages = await _messageRepository.GetByConversationIdAsync(conversation.Id, 1, 1, cancellationToken);
            var lastMessage = messages.FirstOrDefault();
            names.TryGetValue(conversation.SenderId, out var senderName);
            names.TryGetValue(conversation.TravelerId, out var travelerName);
            result.Add(MapToDto(conversation, lastMessage, senderName, travelerName));
        }

        return Result.Success<IEnumerable<ConversationDto>>(result);
    }

    private static ConversationDto MapToDto(Conversation conversation, Message? lastMessage,
        string? senderName, string? travelerName)
    {
        MessageDto? lastMessageDto = lastMessage != null
            ? new MessageDto(
                lastMessage.Id,
                lastMessage.ConversationId,
                lastMessage.SenderId,
                lastMessage.Content,
                lastMessage.MessageType.ToString(),
                lastMessage.ProposedPrice,
                lastMessage.SentAt,
                lastMessage.ReadAt)
            : null;

        return new ConversationDto(
            conversation.Id,
            conversation.PackageRequestId,
            conversation.SenderId,
            conversation.TravelerId,
            senderName,
            travelerName,
            conversation.Status.ToString(),
            conversation.CreatedAt,
            conversation.UpdatedAt,
            lastMessageDto);
    }
}

public record GetMessagesQuery(Guid ConversationId, int Page, int PageSize) : IQuery<IEnumerable<MessageDto>>;

public class GetMessagesQueryHandler : IQueryHandler<GetMessagesQuery, IEnumerable<MessageDto>>
{
    private readonly IMessageRepository _messageRepository;

    public GetMessagesQueryHandler(IMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<Result<IEnumerable<MessageDto>>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var messages = await _messageRepository.GetByConversationIdAsync(request.ConversationId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(messages.Select(m => new MessageDto(
            m.Id,
            m.ConversationId,
            m.SenderId,
            m.Content,
            m.MessageType.ToString(),
            m.ProposedPrice,
            m.SentAt,
            m.ReadAt)));
    }
}
