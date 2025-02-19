﻿using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global
// ReSharper disable UseRawString
#pragma warning disable CA2211
#pragma warning disable SYSLIB1045
namespace Script.Util;

public static class RegexPatterns
{
    public static Regex JavTitle = new(@"\b[A-Z0-9]{3,6}\-[A-Z0-9]{3,6}\b", RegexOptions.Compiled);
    public static Regex SizeRegex = new(@"(\d+((\.|,)\d+)?)\s*(MB|GB|KB)<", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}