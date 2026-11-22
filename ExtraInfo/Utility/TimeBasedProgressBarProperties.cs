namespace ExtraInfo;

public class TimeBasedProgressBarProperties
{
    /// <summary>
    /// Words above the progress bar, for example:  <br/>
    /// [HeaderKey]  <br/>
    /// [ProgressBar]
    /// </summary>
    public string HeaderKey = "";

    /// <summary>
    /// Used as 100% percent
    /// </summary>
    public double HoursTotal;

    /// <summary>
    /// Used together with <see cref="HoursTotal"/> to show progress percent
    /// </summary>
    public double HoursLeft;

    /// <summary>
    /// If true, skips speed and calendar multipliers and treats values as real seconds and converts them to in-game time.
    /// </summary>
    public bool IsRawSeconds = false;

    /// <summary>
    /// If false, skips progress percent
    /// </summary>
    public bool ShowProgressBar = true;

    /// <summary>
    /// If false, skips in-game time
    /// </summary>
    public bool ShowInGameTime = true;

}