﻿using System.Text.RegularExpressions;
using L2Dn.GameServer.Model;

namespace L2Dn.GameServer.Utilities;

public class BypassParser: StatSet
{
    private const String ALLOWED_CHARS = "a-zA-Z0-9-_`!@#%^&*()\\[\\]|\\\\/";
    private static readonly Regex _regex = new Regex(
        $"([{ALLOWED_CHARS}]*)=('([{ALLOWED_CHARS} ]*)'|[{ALLOWED_CHARS}]*)");
	
    public BypassParser(String bypass): base(new Map<string, object>())
    {
        process(bypass);
    }
	
    private void process(String bypass)
    {
        MatchCollection matches = _regex.Matches(bypass);
        foreach (Match match in matches)
        {
            String name = match.Groups[1].Value;
            String escapedValue = match.Groups[2].Value.Trim();
            String unescapedValue = match.Groups[3].Value;
            set(name, unescapedValue != null ? unescapedValue.Trim() : escapedValue);
        }
    }
}