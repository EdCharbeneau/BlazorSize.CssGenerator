using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;

namespace BlazorPro.BlazorSize.CssGenerator
{

    public static class Parser
    {
        public static string RemoveSpecialCharacters(this string str) => Regex.Replace(
            str.Trim()
            .TrimStart('(')
            .TrimEnd(')')
            .Replace(",", "or")
            .Replace(".","-")
                , "[^a-zA-Z0-9_.]+", "_", RegexOptions.Compiled)
            ;

        internal static Dictionary<string, string> Parse(this Dictionary<string, string> queries, SourceText cssText)
        {
            string text = cssText.ToString();

            Regex rx = new Regex(@"\[\s*?AppBreakpoint\s*?\(\s*?Name\s*?=\s*?""(\w+)\s*?""\s*?\)\s*?\].*\s@\s*?media(.[^\{]*)|@\s*?media(.[^\{]*)",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            MatchCollection matches = rx.Matches(text);

            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                bool hasAttribute = groups[1].Success;
                string name;
                string value;

                if (hasAttribute)
                {
                    name = groups[1].Value;
                    value = groups[2].Value;
                }
                else
                {
                    name = groups[3].Value.RemoveSpecialCharacters();
                    value = groups[3].Value.Trim();
                }

                if (!queries.ContainsKey(name))
                {
                    queries.Add(name, value);
                }
            }

            return queries;
        }

    }

    [Generator]
    public class CssGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var groups = GroupFilesByNamespace(context);

            foreach (string name in groups.Keys)
            {
                StringBuilder builder = new StringBuilder("using System;");
                builder.AppendLine($"namespace {name} {{");
                builder.AppendLine("public static class Breakpoints {");

                Dictionary<string, string> mediaQueries = new();
                foreach (var cssText in groups[name])
                {

                    // Parse and find media queries
                    mediaQueries.Parse(cssText);
                }

                var code = GenerateCode(mediaQueries);

                builder.Append(code);

                var codeFileName = $"{name}Breakpoints.cs";

                builder.AppendLine("}}");

                context.AddSource(codeFileName, SourceText.From(builder.ToString(), Encoding.UTF8));

            }

        }

        private static Dictionary<string, List<SourceText>> GroupFilesByNamespace(GeneratorExecutionContext context)
        {
            Dictionary<string, List<SourceText>> groups = new();

            foreach (AdditionalText file in context.AdditionalFiles)
            {
                if (Path.GetExtension(file.Path).Equals(".css", StringComparison.OrdinalIgnoreCase))
                {
                    SourceText cssText = file.GetText();
                    if (cssText is null || cssText.Length == 0)
                    {
                        throw new Exception($"Cannot load file {file.Path}");
                    }

                    context.AnalyzerConfigOptions.GetOptions(file)
                        .TryGetValue("build_metadata.AdditionalFiles.Namespace", out var generateNamespace);
                        var key = string.IsNullOrEmpty(generateNamespace) ? "Css" : generateNamespace;

                    Console.Write(generateNamespace);
                    if (groups.ContainsKey(key))
                    {
                        groups[key].Add(cssText);
                    }
                    else
                    {
                        groups.Add(key, new() { cssText });
                    }
                }
            }

            return groups;
        }

        private string GenerateCode(Dictionary<string, string> mediaQueries)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in mediaQueries)
            {
                sb.AppendLine($@"public const string {item.Key} = ""{item.Value}"";");
            }
            return sb.ToString();
        }

        public void Initialize(GeneratorInitializationContext context) { }
    }
}
