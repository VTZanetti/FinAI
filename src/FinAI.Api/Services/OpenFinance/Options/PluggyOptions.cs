namespace FinAI.Api.Services.OpenFinance.Options;

public sealed class PluggyOptions
{
    public const string SectionName = "Pluggy";

    public string BaseUrl { get; set; } = "https://api.pluggy.ai";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int ImportSinceDays { get; set; } = 90;
    public int PageSize { get; set; } = 100;
    public bool AutoClassify { get; set; } = true;
    public bool ScheduleEnabled { get; set; } = false;
    public int ScheduleIntervalHours { get; set; } = 24;
}