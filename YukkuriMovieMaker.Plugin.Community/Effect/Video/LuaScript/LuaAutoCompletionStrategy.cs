using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Controls.AvalonEdit.AutoCompletionStrategy;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed partial class LuaAutoCompletionStrategy : IAutoCompletionStrategy
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
            "obj", "scene", "anim", "ymm4",
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
                "obj.w", "obj.h", "obj.hw", "obj.hh", "obj.diagonal",
                "obj.cx", "obj.cy", "obj.cz",
                "obj.x", "obj.y", "obj.z",
                "obj.ox", "obj.oy", "obj.oz",
                "obj.sx", "obj.sy",
                "obj.zoom", "obj.alpha", "obj.aspect",
                "obj.rx", "obj.ry", "obj.rz",
                "obj.rxr", "obj.ryr", "obj.rzr",
                "obj.track0", "obj.track1", "obj.track2", "obj.track3",
                "obj.time", "obj.totaltime", "obj.t", "obj.frame", "obj.totalframe",
                "obj.framerate", "obj.layer", "obj.index", "obj.num",
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
                "scene.width", "scene.height", "scene.cx", "scene.cy"
            ]),
            ("anim", [
                "anim.tau", "anim.e", "anim.phi", "anim.sqrt2",
                "anim.lerp", "anim.smoothstep", "anim.smootherstep", "anim.clamp",
                "anim.map", "anim.norm", "anim.wrap", "anim.pingpong",
                "anim.sign", "anim.oscillate", "anim.triangle", "anim.square",
                "anim.duration", "anim.delay",
                "anim.ease_in", "anim.ease_out", "anim.ease_in_out", "anim.elastic", "anim.back",
                "anim.step", "anim.fract", "anim.bounce",
                "anim.hsv_to_rgb", "anim.rgb_to_hsv",
                "anim.len", "anim.dist", "anim.dot", "anim.normalize",
                "anim.noise", "anim.rand", "anim.polar", "anim.rotate", "anim.bezier"
            ]),
            ("ymm4", [
                "ymm4.group_index", "ymm4.group_count", "ymm4.group_ratio",
                "ymm4.timeline_totalframe", "ymm4.timeline_totaltime",
                "ymm4.is_saving", "ymm4.time_ratio",
                "ymm4.is_playing", "ymm4.is_paused", "ymm4.scene_id"
            ]),
        ];

        private static readonly string[] s_staticCandidates =
            s_keywords
            .Concat(s_globals)
            .Concat(s_namespaces.SelectMany(n => n.Members))
            .Distinct()
            .OrderBy(x => x.Length)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        [GeneratedRegex(@"[a-zA-Z_][a-zA-Z_0-9]*$", RegexOptions.None)]
        private static partial Regex IdentifierPattern();

        [GeneratedRegex(@"(?<!local\s)\bfunction\s+([a-zA-Z_][a-zA-Z_0-9]*(?:\.[a-zA-Z_][a-zA-Z_0-9]*)*)\s*\(", RegexOptions.None)]
        private static partial Regex FunctionPattern();

        [GeneratedRegex(@"\blocal\s+function\s+([a-zA-Z_][a-zA-Z_0-9]*)\s*\(", RegexOptions.None)]
        private static partial Regex LocalFunctionPattern();

        public IEnumerable<string> GetCompletionItems(string input, string line, string sourceCode)
        {
            if (string.IsNullOrEmpty(input))
                return [];

            var lastDot = line.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var beforeDot = line[..lastDot];
                var nsMatch = IdentifierPattern().Match(beforeDot);
                if (!nsMatch.Success)
                    return [];

                var ns = nsMatch.Value;
                foreach (var (prefix, members) in s_namespaces)
                {
                    if (ns == prefix)
                        return members;
                }
                return [];
            }

            if (input[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
            {
                var userFunctions = GetUserDefinedFunctions(sourceCode);
                return s_staticCandidates
                    .Concat(userFunctions)
                    .Distinct()
                    .OrderBy(x => x.Length)
                    .ThenBy(x => x, StringComparer.OrdinalIgnoreCase);
            }

            return [];
        }

        private static IEnumerable<string> GetUserDefinedFunctions(string sourceCode)
        {
            foreach (Match m in FunctionPattern().Matches(sourceCode))
                yield return m.Groups[1].Value;

            foreach (Match m in LocalFunctionPattern().Matches(sourceCode))
                yield return m.Groups[1].Value;
        }
    }
}
