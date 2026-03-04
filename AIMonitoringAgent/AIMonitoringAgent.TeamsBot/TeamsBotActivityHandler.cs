using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.TeamsBot;

public class TeamsBotActivityHandler : ActivityHandler
{
    private readonly ITeamsBotService _botService;
    private readonly ILogger<TeamsBotActivityHandler> _logger;

    public TeamsBotActivityHandler(
        ITeamsBotService botService,
        ILogger<TeamsBotActivityHandler> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message received: {Text}", turnContext.Activity.Text);

        var reply = await _botService.HandleActivityAsync(turnContext.Activity);

        if (reply is IMessageActivity messageReply)
        {
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }
    }

    protected override async Task OnConversationUpdateActivityAsync(
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Conversation update activity received");

        if (turnContext.Activity.MembersAdded != null)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var reply = await _botService.HandleActivityAsync(turnContext.Activity);

                    if (reply is IMessageActivity messageReply)
                    {
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                }
            }
        }
    }

    protected override async Task OnMembersAddedAsync(
        List<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    "Welcome to the AI Monitoring Agent! I can help you analyze and track application errors.",
                    cancellationToken: cancellationToken);
            }
        }
    }
}
