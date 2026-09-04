using DesktopCountdown.Models;

namespace DesktopCountdown.Services;

/// <summary>当前所处阶段。</summary>
public enum LessonPhase
{
    /// <summary>未配置有效课程时间。</summary>
    NoSchedule,

    /// <summary>第一节上课之前。</summary>
    BeforeSchool,

    /// <summary>正在上课。</summary>
    InClass,

    /// <summary>课间休息。</summary>
    Break,

    /// <summary>今日课程结束。</summary>
    AfterSchool
}

/// <summary>课程提示窗口的一次计算结果。</summary>
public sealed class LessonState
{
    public LessonPhase Phase { get; init; }
    public string Primary { get; init; } = "";
    public string Secondary { get; init; } = "";
}

/// <summary>根据当前时间计算处于第几节课 / 课间，并生成展示文案。</summary>
public static class ScheduleService
{
    public static LessonState Evaluate(ScheduleConfig config, DateTime now)
    {
        var lessons = new List<(string Name, TimeSpan Start, TimeSpan End)>();
        foreach (var lesson in config.Lessons)
        {
            var start = lesson.StartTime;
            var end = lesson.EndTime;
            if (start is null || end is null || end <= start)
            {
                continue;
            }

            lessons.Add((lesson.Name, start.Value, end.Value));
        }

        lessons.Sort((a, b) => a.Start.CompareTo(b.Start));

        if (lessons.Count == 0)
        {
            return new LessonState
            {
                Phase = LessonPhase.NoSchedule,
                Primary = "未设置课程时间",
                Secondary = "请在设置中添加节次"
            };
        }

        var time = now.TimeOfDay;

        for (int i = 0; i < lessons.Count; i++)
        {
            var (name, start, end) = lessons[i];
            if (time >= start && time < end)
            {
                return new LessonState
                {
                    Phase = LessonPhase.InClass,
                    Primary = WithLessonSuffix(name, "当前是"),
                    Secondary = $"距下课还有 {DescribeRemaining(end - time)}"
                };
            }
        }

        for (int i = 0; i < lessons.Count; i++)
        {
            if (lessons[i].Start > time)
            {
                return new LessonState
                {
                    Phase = i == 0 ? LessonPhase.BeforeSchool : LessonPhase.Break,
                    Primary = WithLessonSuffix(lessons[i].Name, "下一节是"),
                    Secondary = $"距上课还有 {DescribeRemaining(lessons[i].Start - time)}"
                };
            }
        }

        return new LessonState
        {
            Phase = LessonPhase.AfterSchool,
            Primary = "今日课程已结束",
            Secondary = string.IsNullOrWhiteSpace(config.IdleText) ? "放学啦" : config.IdleText
        };
    }

    /// <summary>把剩余时间描述为中文文案，不足 1 分钟单独提示，超过 1 小时拆分为小时与分钟。</summary>
    public static string DescribeRemaining(TimeSpan remaining)
    {
        var totalSeconds = Math.Max(0, remaining.TotalSeconds);
        var minutes = (int)Math.Ceiling(totalSeconds / 60.0);

        if (minutes <= 0)
        {
            return "不足 1 分钟";
        }

        if (minutes < 60)
        {
            return $"{minutes} 分钟";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0 ? $"{hours} 小时" : $"{hours} 小时 {rest} 分钟";
    }

    /// <summary>拼装标题：名称为「早读」补「课」字，为「阅读课」不再重复。</summary>
    private static string WithLessonSuffix(string? name, string prefix)
    {
        var text = string.IsNullOrWhiteSpace(name) ? "下一节" : name.Trim();
        return text.EndsWith("课", StringComparison.Ordinal) ? prefix + text : prefix + text + "课";
    }
}
