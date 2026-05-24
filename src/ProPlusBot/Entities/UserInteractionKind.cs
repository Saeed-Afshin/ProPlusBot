namespace ProPlusBot.Entities;

public enum UserInteractionKind
{
    LinkDetected = 0,
    SearchQuery = 1,
    SearchResultPick = 2,
    FormatListShown = 3,
    FormatListFailed = 4,
    FormatSelected = 5,
    DownloadQueued = 6,
    DownloadCompleted = 7,
    DownloadFailed = 8,
    QuotaOrAccessBlocked = 9,
    CallbackAction = 10,
    Other = 99
}
