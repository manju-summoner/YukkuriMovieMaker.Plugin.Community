using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Controls.AvalonEdit.AutoCompletionStrategy;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaAutoCompletionStrategy : IAutoCompletionStrategy
    {
        private static readonly string[] s_keywords =
        [
            "and", "break", "do", "else", "elseif", "end", "false", "for",
            "function", "goto", "if", "in", "local", "nil", "not", "or",
            "repeat", "return", "then", "true", "until", "while"
        ];

        private static readonly string[] s_globals =
        [
            "time", "frame", "totalframe", "framerate",
            "timelineframe", "timelinetime", "layer",
            "obj", "scene",
            "math", "string", "table", "bit32",
            "type", "tostring", "tonumber", "select", "error", "assert", "print",
            "ipairs", "pairs", "next",
            "unpack",
            "setmetatable", "getmetatable", "rawget", "rawset", "rawequal", "rawlen",
            "pcall", "xpcall",
        ];

        private static readonly (string Prefix, string[] Members)[] s_namespaces =
        [
            ("obj", [
                "obj.w", "obj.h", "obj.cx", "obj.cy",
                "obj.x", "obj.y", "obj.z",
                "obj.ox", "obj.oy",
                "obj.zoom", "obj.alpha", "obj.aspect",
                "obj.rx", "obj.ry", "obj.rz",
                "obj.track0", "obj.track1", "obj.track2", "obj.track3",
                "obj.getpixel", "obj.setpixel",
                "obj.getpixeldata"
            ]),
            ("math", [
                "math.abs", "math.ceil", "math.cos", "math.exp", "math.floor",
                "math.fmod", "math.huge", "math.log", "math.max", "math.min",
                "math.modf", "math.pi", "math.random", "math.randomseed",
                "math.sin", "math.sqrt", "math.tan", "math.atan", "math.atan2",
                "math.deg", "math.rad", "math.ldexp", "math.frexp",
                "math.sinh", "math.cosh", "math.tanh", "math.type",
                "math.tointeger", "math.maxinteger", "math.mininteger"
            ]),
            ("string", [
                "string.format", "string.len", "string.sub", "string.upper",
                "string.lower", "string.rep", "string.reverse", "string.find",
                "string.match", "string.gmatch", "string.gsub", "string.byte",
                "string.char", "string.dump", "string.pack", "string.unpack",
                "string.packsize"
            ]),
            ("table", [
                "table.insert", "table.remove", "table.sort",
                "table.concat", "table.unpack", "table.move"
            ]),
            ("bit32", [
                "bit32.band", "bit32.bor", "bit32.bxor", "bit32.bnot",
                "bit32.lshift", "bit32.rshift", "bit32.arshift",
                "bit32.extract", "bit32.replace",
                "bit32.btest", "bit32.countlz", "bit32.countrz"
            ]),
            ("scene", [
                "scene.width", "scene.height"
            ]),
        ];

        private static readonly string[] s_staticCandidates =
            s_keywords
            .Concat(s_globals)
            .Concat(s_namespaces.SelectMany(n => n.Members))
            .Distinct()
            .OrderBy(x => x.Length)
            .ToArray();

        private static readonly Regex s_identifierPattern = new(
            @"[a-zA-Z_][a-zA-Z_0-9]*$",
            RegexOptions.Compiled);

        private static readonly Regex s_functionPattern = new(
            @"(?<!local\s)\bfunction\s+([a-zA-Z_][a-zA-Z_0-9]*(?:\.[a-zA-Z_][a-zA-Z_0-9]*)*)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex s_localFunctionPattern = new(
            @"\blocal\s+function\s+([a-zA-Z_][a-zA-Z_0-9]*)\s*\(",
            RegexOptions.Compiled);

        public IEnumerable<string> GetCompletionItems(string input, string line, string sourceCode)
        {
            if (string.IsNullOrEmpty(input))
                return [];

            var lastDot = line.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var beforeDot = line[..lastDot];
                var nsMatch = s_identifierPattern.Match(beforeDot);
                if (!nsMatch.Success)
                    return [];

                var ns = nsMatch.Value;
                var afterDot = input == "." ? "" : line[(lastDot + 1)..];

                foreach (var (prefix, members) in s_namespaces)
                {
                    if (ns == prefix)
                        return FilterPrefix(members, prefix + "." + afterDot);
                }
                return [];
            }

            if (input[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
            {
                var userFunctions = GetUserDefinedFunctions(sourceCode);
                return s_staticCandidates
                    .Concat(userFunctions)
                    .Where(x => x.StartsWith(input, System.StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .OrderBy(x => x.Length);
            }

            return [];
        }

        private static IEnumerable<string> GetUserDefinedFunctions(string sourceCode)
        {
            foreach (Match m in s_functionPattern.Matches(sourceCode))
                yield return m.Groups[1].Value;

            foreach (Match m in s_localFunctionPattern.Matches(sourceCode))
                yield return m.Groups[1].Value;
        }

        private static IEnumerable<string> FilterPrefix(IEnumerable<string> items, string prefix) =>
            items.Where(x => x.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
    }
}
