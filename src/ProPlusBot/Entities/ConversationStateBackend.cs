using System.ComponentModel.DataAnnotations;

namespace ProPlusBot.Entities;

public enum ConversationStateBackend
{
    [Display(Name = "حافظه (RAM)")]
    Memory = 0,

    [Display(Name = "Redis")]
    Redis = 1
}
