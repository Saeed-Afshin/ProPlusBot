using ProPlusBot.Entities;

namespace ProPlusBot.Services.Media.State;

public sealed class ConversationStateBackendHolder
{
    public ConversationStateBackend Backend { get; private set; } = ConversationStateBackend.Memory;

    public void Set(ConversationStateBackend backend) => Backend = backend;
}
