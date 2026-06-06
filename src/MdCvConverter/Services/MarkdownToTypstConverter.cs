using System.Text;
using System.Text.RegularExpressions;

namespace MdCvConverter.Services;

public sealed class MarkdownMetadata
{
    public string? Author { get; init; }
    public string? Position { get; init; }
    public string? SidebarComponentsTypst { get; init; }
}

public static class MarkdownToTypstConverter
{
    private static readonly Regex FrontMatterKeyValuePattern = new(
        "^(?<key>[A-Za-z0-9_-]+)\\s*:\\s*(?<value>.+?)\\s*$",
        RegexOptions.Compiled);

    private static readonly Regex SidebarSectionPattern = new(
        "^##\\s+Sidebar:\\s*(?<title>.+?)\\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SidebarIconLinePattern = new(
        "^\\s*(?:[-*]\\s+)?\\[icon:(?<icon>[a-z0-9-]+)(?<modifiers>(?:\\s*,\\s*[a-z-]+)*)\\]\\s+(?<text>.+?)\\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SidebarSkillLevelLinePattern = new(
        "^\\s*[-*]\\s+\\[level:(?<level>\\d{1,3})%?\\s*,\\s*asset:(?<asset>[^\\]]+)\\]\\s+(?<text>.+?)\\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SidebarSkillGroupHeaderPattern = new(
        "^###\\s+Skill-Group:\\s*(?<name>[^\\[]+?)\\s*\\[icon:(?<icon>[a-z0-9-]+)(?<modifiers>(?:\\s*,\\s*[a-z-]+)*)\\]\\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> IconAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["at"] = "envelope"
    };

    public static string Convert(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var startIndex = GetContentStartIndex(lines);
        var sidebarSections = ExtractSidebarSections(lines, startIndex);
        var sidebarLineIndexes = GetSidebarLineIndexes(sidebarSections);
        return BuildBodyComponentsTypst(lines, startIndex, sidebarLineIndexes);
    }

    public static MarkdownMetadata ExtractMetadata(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var frontMatter = TryReadFrontMatter(lines);
        frontMatter.TryGetValue("author", out var frontMatterAuthor);
        frontMatter.TryGetValue("position", out var frontMatterPosition);
        var startIndex = GetContentStartIndex(lines);
        var sidebarSections = ExtractSidebarSections(lines, startIndex);

        var author = NormalizeValue(frontMatterAuthor) ?? FindFirstHeading(lines);
        var position = NormalizeValue(frontMatterPosition) ?? FindPositionLine(lines);

        return new MarkdownMetadata
        {
            Author = author,
            Position = position,
            SidebarComponentsTypst = BuildSidebarComponentsTypst(sidebarSections)
        };
    }

    private static Dictionary<string, string> TryReadFrontMatter(string[] lines)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Length < 3 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            return values;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.Equals(line, "---", StringComparison.Ordinal))
            {
                return values;
            }

            var match = FrontMatterKeyValuePattern.Match(line);
            if (match.Success)
            {
                var key = match.Groups["key"].Value;
                var value = TrimQuotes(match.Groups["value"].Value.Trim());
                values[key] = value;
            }
        }

        return values;
    }

    private static int GetContentStartIndex(string[] lines)
    {
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            return 0;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), "---", StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static string? FindFirstHeading(string[] lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var text = line[2..].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? FindPositionLine(string[] lines)
    {
        foreach (var raw in lines.Take(30))
        {
            var line = raw.Trim();
            if (line.StartsWith("position:", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeValue(line[9..]);
            }
        }

        return null;
    }

    private static List<SidebarSection> ExtractSidebarSections(string[] lines, int startIndex)
    {
        var sections = new List<SidebarSection>();

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var match = SidebarSectionPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var title = NormalizeValue(match.Groups["title"].Value) ?? "Sidebar";
            var contentStart = i + 1;
            var endExclusive = contentStart;

            while (endExclusive < lines.Length)
            {
                var candidate = lines[endExclusive].Trim();
                if (candidate.StartsWith("# ", StringComparison.Ordinal)
                    || candidate.StartsWith("## ", StringComparison.Ordinal))
                {
                    break;
                }

                endExclusive++;
            }

            var contentLines = lines
                .Skip(contentStart)
                .Take(endExclusive - contentStart)
                .ToList();

            sections.Add(new SidebarSection(title, i, endExclusive, contentLines));
            i = endExclusive - 1;
        }

        return sections;
    }

    private static HashSet<int> GetSidebarLineIndexes(IEnumerable<SidebarSection> sections)
    {
        var indexes = new HashSet<int>();
        foreach (var section in sections)
        {
            for (var i = section.StartIndex; i < section.EndExclusive; i++)
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    private static string BuildSidebarComponentsTypst(IEnumerable<SidebarSection> sections)
    {
        var sb = new StringBuilder();

        foreach (var section in sections)
        {
            sb.AppendLine($"#sidebar-section(title: \"{EscapeTypstString(section.Title)}\")[");
            var content = ConvertSidebarSectionContent(section.ContentLines);
            if (!string.IsNullOrWhiteSpace(content))
            {
                foreach (var line in content.TrimEnd().Split('\n'))
                {
                    sb.Append("  ");
                    sb.AppendLine(line);
                }
            }

            sb.AppendLine("]");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string ConvertSidebarSectionContent(List<string> lines)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < lines.Count; i++)
        {
            if (TryParseSidebarSkillGroupHeader(lines[i], out var group))
            {
                var skillItems = new List<string>();
                var j = i + 1;
                while (j < lines.Count)
                {
                    var candidate = lines[j].Trim();
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        j++;
                        continue;
                    }

                    if (candidate.StartsWith("### ", StringComparison.Ordinal)
                        || TryParseSidebarSkillLevelLine(lines[j], out _)
                        || TryParseSidebarIconLine(lines[j], out _))
                    {
                        break;
                    }

                    if (candidate.StartsWith("- ", StringComparison.Ordinal) || candidate.StartsWith("* ", StringComparison.Ordinal))
                    {
                        var skill = candidate[2..].Trim();
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            skillItems.Add(skill);
                        }
                    }

                    j++;
                }

                var iconSolid = group!.Solid ? "icon-solid: true, " : string.Empty;
                sb.AppendLine("#skill-group(");
                sb.Append("  name: \"");
                sb.Append(EscapeTypstString(group.Name));
                sb.AppendLine("\",");
                sb.Append("  icon: \"");
                sb.Append(group.Icon);
                sb.AppendLine("\",");
                if (group.Solid)
                {
                    sb.AppendLine("  icon-solid: true,");
                }

                sb.AppendLine("  skills: (");
                foreach (var skill in skillItems)
                {
                    sb.Append("    \"");
                    sb.Append(EscapeTypstString(skill));
                    sb.AppendLine("\",");
                }

                sb.AppendLine("  ),");
                sb.AppendLine(")");
                sb.AppendLine();

                i = Math.Max(i, j - 1);
                continue;
            }

            if (TryParseSidebarSkillLevelLine(lines[i], out var skillLevelItem))
            {
                var skillLevelItems = new List<SidebarSkillLevelItem> { skillLevelItem! };
                while (i + 1 < lines.Count && TryParseSidebarSkillLevelLine(lines[i + 1], out var nextSkillLevel))
                {
                    skillLevelItems.Add(nextSkillLevel!);
                    i++;
                }

                sb.AppendLine("#skill-levels((");
                foreach (var item in skillLevelItems)
                {
                    sb.Append("  (icon: image(asset-path(\"");
                    sb.Append(EscapeTypstString(item.AssetPath));
                    sb.Append("\")), text: [");
                    sb.Append(ConvertInlineTypst(item.Text));
                    sb.AppendLine("], level: ");
                    sb.Append(item.Level);
                    sb.AppendLine("%),");
                }

                sb.AppendLine("))");
                sb.AppendLine();
                continue;
            }

            if (!TryParseSidebarIconLine(lines[i], out var iconItem))
            {
                AppendConvertedLine(sb, lines[i]);
                continue;
            }

            var iconItems = new List<SidebarIconItem> { iconItem! };
            while (i + 1 < lines.Count && TryParseSidebarIconLine(lines[i + 1], out var nextItem))
            {
                iconItems.Add(nextItem!);
                i++;
            }

            sb.AppendLine("#icon-list((");
            foreach (var item in iconItems)
            {
                var iconSolid = item.Solid ? "icon-solid: true, " : string.Empty;
                sb.Append("  (icon: \"");
                sb.Append(item.Icon);
                sb.Append("\", ");
                sb.Append(iconSolid);
                sb.Append("text: [");
                sb.Append(ConvertInlineTypst(item.Text));
                sb.AppendLine("]),");
            }

            sb.AppendLine("))");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildBodyComponentsTypst(string[] lines, int startIndex, HashSet<int> excludedIndexes)
    {
        var bodyLines = new List<string>();
        for (var i = startIndex; i < lines.Length; i++)
        {
            if (!excludedIndexes.Contains(i))
            {
                bodyLines.Add(lines[i]);
            }
        }

        if (!bodyLines.Any(line => line.TrimStart().StartsWith("## ", StringComparison.Ordinal)))
        {
            return ConvertLines(bodyLines);
        }

        var sb = new StringBuilder();
        var index = 0;
        while (index < bodyLines.Count)
        {
            var line = bodyLines[index].Trim();
            if (!line.StartsWith("## ", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            var sectionTitle = line[3..].Trim();
            var sectionContent = new List<string>();
            index++;
            while (index < bodyLines.Count && !bodyLines[index].Trim().StartsWith("## ", StringComparison.Ordinal))
            {
                sectionContent.Add(bodyLines[index]);
                index++;
            }

            sb.AppendLine($"#section(title: \"{EscapeTypstString(sectionTitle)}\")[");
            var elements = BuildSectionElementsTypst(sectionContent);
            if (!string.IsNullOrWhiteSpace(elements))
            {
                foreach (var elementLine in elements.TrimEnd().Split('\n'))
                {
                    sb.Append("  ");
                    sb.AppendLine(elementLine);
                }
            }

            sb.AppendLine("]");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildSectionElementsTypst(List<string> sectionContent)
    {
        var sb = new StringBuilder();
        var hasElementHeadings = sectionContent.Any(line => line.TrimStart().StartsWith("### ", StringComparison.Ordinal));

        if (!hasElementHeadings)
        {
            AppendSectionElement(sb, "Overview", null, sectionContent);
            return sb.ToString();
        }

        var index = 0;
        while (index < sectionContent.Count)
        {
            var line = sectionContent[index].Trim();
            if (!line.StartsWith("### ", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            var headingText = line[4..].Trim();
            var (title, info) = ParseElementHeading(headingText);

            var contentLines = new List<string>();
            index++;
            while (index < sectionContent.Count && !sectionContent[index].Trim().StartsWith("### ", StringComparison.Ordinal))
            {
                contentLines.Add(sectionContent[index]);
                index++;
            }

            AppendSectionElement(sb, title, info, contentLines);
        }

        return sb.ToString();
    }

    private static void AppendSectionElement(StringBuilder sb, string title, string? info, List<string> contentLines)
    {
        sb.AppendLine("#section-element(");
        sb.Append("  title: \"");
        sb.Append(EscapeTypstString(title));
        sb.AppendLine("\",");
        if (!string.IsNullOrWhiteSpace(info))
        {
            sb.Append("  info: [_");
            sb.Append(ConvertInlineTypst(info));
            sb.AppendLine("_],");
        }

        sb.AppendLine("  [");
        var content = ConvertBodyElementContent(contentLines);
        if (!string.IsNullOrWhiteSpace(content))
        {
            foreach (var line in content.TrimEnd().Split('\n'))
            {
                sb.Append("    ");
                sb.AppendLine(line);
            }
        }

        sb.AppendLine("  ],");
        sb.AppendLine(")");
        sb.AppendLine();
    }

    private static string ConvertBodyElementContent(List<string> lines)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParseSidebarIconLine(lines[i], out var iconItem))
            {
                AppendConvertedLine(sb, lines[i]);
                continue;
            }

            var iconItems = new List<SidebarIconItem> { iconItem! };
            while (i + 1 < lines.Count && TryParseSidebarIconLine(lines[i + 1], out var nextIconItem))
            {
                iconItems.Add(nextIconItem!);
                i++;
            }

            sb.AppendLine("#icon-list((");
            foreach (var item in iconItems)
            {
                var iconSolid = item.Solid ? "icon-solid: true, " : string.Empty;
                sb.Append("  (icon: \"");
                sb.Append(item.Icon);
                sb.Append("\", ");
                sb.Append(iconSolid);
                sb.Append("text: [");
                sb.Append(ConvertInlineTypst(item.Text));
                sb.AppendLine("]),");
            }

            sb.AppendLine("))");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static (string Title, string? Info) ParseElementHeading(string headingText)
    {
        var separatorIndex = headingText.LastIndexOf('|');
        if (separatorIndex <= 0 || separatorIndex == headingText.Length - 1)
        {
            return (headingText, null);
        }

        var title = headingText[..separatorIndex].Trim();
        var info = headingText[(separatorIndex + 1)..].Trim();
        return (title, string.IsNullOrWhiteSpace(info) ? null : info);
    }

    private static string ConvertLines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            AppendConvertedLine(sb, raw);
        }

        return sb.ToString();
    }

    private static void AppendConvertedLine(StringBuilder sb, string raw)
    {
        var line = raw.TrimEnd();

        if (string.IsNullOrWhiteSpace(line))
        {
            sb.AppendLine();
            return;
        }

        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            sb.AppendLine($"=== {ConvertInlineTypst(line[4..])}");
            return;
        }

        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            sb.AppendLine($"== {ConvertInlineTypst(line[3..])}");
            return;
        }

        if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            sb.AppendLine($"= {ConvertInlineTypst(line[2..])}");
            return;
        }

        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
        {
            sb.AppendLine($"- {ConvertInlineTypst(line[2..])}");
            return;
        }

        sb.AppendLine(ConvertInlineTypst(line));
    }

    private static string ConvertInlineTypst(string text)
    {
        var normalized = ReplaceHtmlEntities(text);
        var sb = new StringBuilder();
        var lastIndex = 0;

        // Process links and bold together
        var allMatches = new List<(int Index, int Length, char Type, Match Match)>();
        
        foreach (Match linkMatch in LinkPattern.Matches(normalized))
        {
            allMatches.Add((linkMatch.Index, linkMatch.Length, 'L', linkMatch));
        }

        foreach (Match boldMatch in BoldPattern.Matches(normalized))
        {
            // Only add bold if it doesn't overlap with a link
            var overlapsLink = allMatches.Any(m => m.Type == 'L' && 
                !(boldMatch.Index + boldMatch.Length <= m.Index || boldMatch.Index >= m.Index + m.Length));
            
            if (!overlapsLink)
            {
                allMatches.Add((boldMatch.Index, boldMatch.Length, 'B', boldMatch));
            }
        }

        allMatches = allMatches.OrderBy(m => m.Index).ToList();

        foreach (var item in allMatches)
        {
            var match = item.Match;
            sb.Append(Escape(normalized[lastIndex..match.Index]));

            if (item.Type == 'L')
            {
                // Process link: [text](url)
                var linkText = match.Groups[1].Value;
                var linkUrl = match.Groups[2].Value;
                
                // If text matches URL, display with underline styling
                if (linkText.Equals(linkUrl, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("#underline(link(\"");
                    sb.Append(linkUrl.Replace("\\", "\\\\").Replace("\"", "\\\""));
                    sb.Append("\")[");
                    sb.Append(linkUrl.Replace("/", "\\/"));
                    sb.Append("])");
                }
                else
                {
                    // Otherwise use link(url)[text] for custom display text
                    sb.Append("link(\"");
                    sb.Append(linkUrl.Replace("\\", "\\\\").Replace("\"", "\\\""));
                    sb.Append("\")[");
                    sb.Append(ConvertBoldInText(linkText));
                    sb.Append("]");
                }
            }
            else if (item.Type == 'B')
            {
                // Process bold: **text**
                sb.Append("#highlight[");
                sb.Append(Escape(match.Groups[1].Value));
                sb.Append("]");
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < normalized.Length)
        {
            sb.Append(Escape(normalized[lastIndex..]));
        }

        return sb.ToString();
    }

    private static string ConvertBoldInText(string text)
    {
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match boldMatch in BoldPattern.Matches(text))
        {
            sb.Append(Escape(text[lastIndex..boldMatch.Index]));
            sb.Append("#highlight[");
            sb.Append(Escape(boldMatch.Groups[1].Value));
            sb.Append("]");
            lastIndex = boldMatch.Index + boldMatch.Length;
        }

        if (lastIndex < text.Length)
        {
            sb.Append(Escape(text[lastIndex..]));
        }

        return sb.ToString();
    }

    private static string ReplaceHtmlEntities(string text)
    {
        return text
            .Replace("&emsp;", "\u2003", StringComparison.OrdinalIgnoreCase)
            .Replace("&ensp;", "\u2002", StringComparison.OrdinalIgnoreCase)
            .Replace("&nbsp;", "\u00A0", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Regex BoldPattern = new(
        @"\*\*(.+?)\*\*",
        RegexOptions.Compiled);

    private static readonly Regex LinkPattern = new(
        @"\[([^\[\]]+)\]\(([^\)]+)\)",
        RegexOptions.Compiled);

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2)
        {
            if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    private static bool TryParseSidebarIconLine(string line, out SidebarIconItem? item)
    {
        var match = SidebarIconLinePattern.Match(line);
        if (!match.Success)
        {
            item = null;
            return false;
        }

        var icon = match.Groups["icon"].Value;
        var text = match.Groups["text"].Value.Trim();
        var modifiers = match.Groups["modifiers"].Value;
        var solid = modifiers.Contains("solid", StringComparison.OrdinalIgnoreCase);
        if (IconAliases.TryGetValue(icon, out var alias))
        {
            icon = alias;
        }

        item = new SidebarIconItem(icon, solid, text);
        return true;
    }

    private static bool TryParseSidebarSkillLevelLine(string line, out SidebarSkillLevelItem? item)
    {
        var match = SidebarSkillLevelLinePattern.Match(line);
        if (!match.Success)
        {
            item = null;
            return false;
        }

        if (!int.TryParse(match.Groups["level"].Value, out var level))
        {
            item = null;
            return false;
        }

        level = Math.Clamp(level, 0, 100);
        var asset = match.Groups["asset"].Value.Trim();
        var text = match.Groups["text"].Value.Trim();

        item = new SidebarSkillLevelItem(level, asset, text);
        return true;
    }

    private static bool TryParseSidebarSkillGroupHeader(string line, out SidebarSkillGroupHeader? header)
    {
        var match = SidebarSkillGroupHeaderPattern.Match(line);
        if (!match.Success)
        {
            header = null;
            return false;
        }

        var name = NormalizeValue(match.Groups["name"].Value) ?? "Skills";
        var icon = match.Groups["icon"].Value;
        var modifiers = match.Groups["modifiers"].Value;
        var solid = modifiers.Contains("solid", StringComparison.OrdinalIgnoreCase);
        if (IconAliases.TryGetValue(icon, out var alias))
        {
            icon = alias;
        }

        header = new SidebarSkillGroupHeader(name, icon, solid);
        return true;
    }

    private static string EscapeTypstString(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string Escape(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("#", "\\#", StringComparison.Ordinal)
            .Replace("@", "\\@", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    private sealed class SidebarSection
    {
        public SidebarSection(string title, int startIndex, int endExclusive, List<string> contentLines)
        {
            Title = title;
            StartIndex = startIndex;
            EndExclusive = endExclusive;
            ContentLines = contentLines;
        }

        public string Title { get; }
        public int StartIndex { get; }
        public int EndExclusive { get; }
        public List<string> ContentLines { get; }
    }

    private sealed class SidebarIconItem
    {
        public SidebarIconItem(string icon, bool solid, string text)
        {
            Icon = icon;
            Solid = solid;
            Text = text;
        }

        public string Icon { get; }
        public bool Solid { get; }
        public string Text { get; }
    }

    private sealed class SidebarSkillLevelItem
    {
        public SidebarSkillLevelItem(int level, string assetPath, string text)
        {
            Level = level;
            AssetPath = assetPath;
            Text = text;
        }

        public int Level { get; }
        public string AssetPath { get; }
        public string Text { get; }
    }

    private sealed class SidebarSkillGroupHeader
    {
        public SidebarSkillGroupHeader(string name, string icon, bool solid)
        {
            Name = name;
            Icon = icon;
            Solid = solid;
        }

        public string Name { get; }
        public string Icon { get; }
        public bool Solid { get; }
    }
}
