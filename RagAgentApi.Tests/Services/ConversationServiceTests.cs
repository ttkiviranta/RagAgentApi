using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using RagAgentApi.Services;

namespace RagAgentApi.Tests.Services;

/// <summary>
/// Unit tests for ConversationService
/// </summary>
public class ConversationServiceTests : IDisposable
{
    private readonly RagDbContext _context;
    private readonly Mock<ILogger<ConversationService>> _loggerMock;
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        var options = new DbContextOptionsBuilder<RagDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Use TestDbContext to avoid JsonDocument/Vector type mapping issues with InMemory provider
        _context = new TestDbContext(options);
        _loggerMock = new Mock<ILogger<ConversationService>>();
        _service = new ConversationService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateConversationAsync Tests

    [Fact]
    public async Task CreateConversationAsync_ShouldCreateNewConversation()
    {
        // Act
        var conversation = await _service.CreateConversationAsync();

        // Assert
        conversation.Should().NotBeNull();
        conversation.Id.Should().NotBeEmpty();
        conversation.Status.Should().Be("active");
        conversation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateConversationAsync_WithUserId_ShouldSetUserId()
    {
        // Arrange
        var userId = "test-user-123";

        // Act
        var conversation = await _service.CreateConversationAsync(userId: userId);

        // Assert
        conversation.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateConversationAsync_WithTitle_ShouldSetTitle()
    {
        // Arrange
        var title = "Test Conversation";

        // Act
        var conversation = await _service.CreateConversationAsync(title: title);

        // Assert
        conversation.Title.Should().Be(title);
    }

    [Fact]
    public async Task CreateConversationAsync_ShouldPersistToDatabase()
    {
        // Act
        var conversation = await _service.CreateConversationAsync();

        // Assert
        var fromDb = await _context.Conversations.FindAsync(conversation.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Id.Should().Be(conversation.Id);
    }

    #endregion

    #region AddMessageAsync Tests

    [Fact]
    public async Task AddMessageAsync_ShouldAddMessageToConversation()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();

        // Act
        var message = await _service.AddMessageAsync(
            conversation.Id,
            "user",
            "Hello, World!");

        // Assert
        message.Should().NotBeNull();
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hello, World!");
        message.ConversationId.Should().Be(conversation.Id);
    }

    [Fact]
    public async Task AddMessageAsync_ShouldIncrementMessageCount()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        var initialCount = conversation.MessageCount;

        // Act
        await _service.AddMessageAsync(conversation.Id, "user", "First message");
        await _service.AddMessageAsync(conversation.Id, "assistant", "Second message");

        // Assert
        var updated = await _context.Conversations.FindAsync(conversation.Id);
        updated!.MessageCount.Should().Be(initialCount + 2);
    }

    [Fact]
    public async Task AddMessageAsync_ShouldUpdateLastMessageAt()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        var initialTime = conversation.LastMessageAt;
        await Task.Delay(10);

        // Act
        await _service.AddMessageAsync(conversation.Id, "user", "Test message");

        // Assert
        var updated = await _context.Conversations.FindAsync(conversation.Id);
        updated!.LastMessageAt.Should().BeOnOrAfter(initialTime);
    }

    [Fact]
    public async Task AddMessageAsync_WithUserMessage_ShouldSetTitleIfEmpty()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        var messageContent = "What is machine learning?";

        // Act
        await _service.AddMessageAsync(conversation.Id, "user", messageContent);

        // Assert
        var updated = await _context.Conversations.FindAsync(conversation.Id);
        updated!.Title.Should().Be(messageContent);
    }

    [Fact]
    public async Task AddMessageAsync_WithLongUserMessage_ShouldTruncateTitle()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        var longMessage = new string('a', 100);

        // Act
        await _service.AddMessageAsync(conversation.Id, "user", longMessage);

        // Assert
        var updated = await _context.Conversations.FindAsync(conversation.Id);
        updated!.Title.Should().HaveLength(53); // 50 chars + "..."
        updated.Title.Should().EndWith("...");
    }

    [Fact]
    public async Task AddMessageAsync_WithNonExistentConversation_ShouldThrowException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _service.AddMessageAsync(nonExistentId, "user", "Test");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{nonExistentId}*");
    }

    #endregion

    #region GetConversationHistoryAsync Tests

    [Fact]
    public async Task GetConversationHistoryAsync_ShouldReturnConversationWithMessages()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        await _service.AddMessageAsync(conversation.Id, "user", "Question");
        await _service.AddMessageAsync(conversation.Id, "assistant", "Answer");

        // Act
        var result = await _service.GetConversationHistoryAsync(conversation.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Conversation.Id.Should().Be(conversation.Id);
        result.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_WithLimit_ShouldLimitMessages()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        await _service.AddMessageAsync(conversation.Id, "user", "Message 1");
        await _service.AddMessageAsync(conversation.Id, "assistant", "Message 2");
        await _service.AddMessageAsync(conversation.Id, "user", "Message 3");

        // Act
        var result = await _service.GetConversationHistoryAsync(conversation.Id, limit: 2);

        // Assert
        result!.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetConversationHistoryAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ShouldOrderMessagesByCreatedAt()
    {
        // Arrange
        var conversation = await _service.CreateConversationAsync();
        await _service.AddMessageAsync(conversation.Id, "user", "First");
        await Task.Delay(10);
        await _service.AddMessageAsync(conversation.Id, "assistant", "Second");

        // Act
        var result = await _service.GetConversationHistoryAsync(conversation.Id);

        // Assert
        result!.Messages[0].Content.Should().Be("First");
        result.Messages[1].Content.Should().Be("Second");
    }

    #endregion

    #region GetUserConversationsAsync Tests

    [Fact]
    public async Task GetUserConversationsAsync_ShouldReturnActiveConversations()
    {
        // Arrange
        await _service.CreateConversationAsync(title: "Conversation 1");
        await _service.CreateConversationAsync(title: "Conversation 2");

        // Act
        var conversations = await _service.GetUserConversationsAsync();

        // Assert
        conversations.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserConversationsAsync_WithUserId_ShouldFilterByUser()
    {
        // Arrange
        await _service.CreateConversationAsync(userId: "user1", title: "User1 Conv");
        await _service.CreateConversationAsync(userId: "user2", title: "User2 Conv");

        // Act
        var conversations = await _service.GetUserConversationsAsync(userId: "user1");

        // Assert
        conversations.Should().HaveCount(1);
        conversations[0].UserId.Should().Be("user1");
    }

    [Fact]
    public async Task GetUserConversationsAsync_ShouldOrderByLastMessageAtDescending()
    {
        // Arrange
        var conv1 = await _service.CreateConversationAsync(title: "Older");
        await Task.Delay(50);
        var conv2 = await _service.CreateConversationAsync(title: "Newer");

        // Act
        var conversations = await _service.GetUserConversationsAsync();

        // Assert
        conversations[0].Title.Should().Be("Newer");
        conversations[1].Title.Should().Be("Older");
    }

    [Fact]
    public async Task GetUserConversationsAsync_WithLimit_ShouldLimitResults()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await _service.CreateConversationAsync(title: $"Conv {i}");
        }

        // Act
        var conversations = await _service.GetUserConversationsAsync(limit: 3);

        // Assert
        conversations.Should().HaveCount(3);
    }

    #endregion
}
