using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LOMAssistant.Features.AnytimeSave
{
    // Semantic contract over the game code Anytime Save actually relies on: do not
    // trust version numbers, MVIDs or metadata tokens. Replay itself is guarded at
    // run time (story scripts are hashed per save, queries are replayed from the
    // trace and any divergence stops the restore, native saves are blocked while
    // replaying), so unrelated game changes must not disable the feature.
    //   FullTypes:   layout and every method body (nested state machines included).
    //   BodyMethods: method bodies we replace or whose input we rewrite.
    //   Members:     fields/methods used through reflection; only their signatures.
    public static class CompatibilityContract
    {
        public static string Digest(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
        {
            foreach (var t in types) { yield return t; foreach (var n in Types(t.NestedTypes)) yield return n; }
        }
        static string Value(object value)
        {
            if (value == null) return "null";
            var bytes = value as byte[];
            return bytes != null ? Convert.ToBase64String(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        static string Attributes(ICustomAttributeProvider item)
        {
            return String.Join(";", item.CustomAttributes.Select(a => a.AttributeType.FullName + ":" + Convert.ToBase64String(a.GetBlob())).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        }
        static string Body(MethodDefinition method)
        {
            var s = new StringBuilder(method.FullName + "|" + ((int)method.Attributes) + "|" + ((int)method.ImplAttributes) + "|" + Attributes(method));
            foreach (var p in method.Parameters) s.Append("\nP:").Append(p.Name).Append(':').Append(((int)p.Attributes)).Append(':').Append(Value(p.Constant));
            if (!method.HasBody) return s.ToString();
            var b = method.Body;
            s.Append("\nInit:").Append(b.InitLocals);
            foreach (var v in b.Variables) s.Append("\nV:").Append(v.VariableType.FullName);
            foreach (var i in b.Instructions)
            {
                // Cecil resolves member operands to symbolic names, not tokens.
                var branch = i.Operand as Instruction;
                var branches = i.Operand as Instruction[];
                string operand = branch != null ? "@" + b.Instructions.IndexOf(branch) : branches != null
                    ? String.Join(",", branches.Select(x => "@" + b.Instructions.IndexOf(x)).ToArray()) : Value(i.Operand);
                s.Append('\n').Append(i.OpCode.Name).Append(':').Append(operand.Length).Append(':').Append(operand);
            }
            foreach (var e in b.ExceptionHandlers)
                s.Append("\nEH:").Append(((int)e.HandlerType)).Append(':').Append(b.Instructions.IndexOf(e.TryStart)).Append(':').Append(b.Instructions.IndexOf(e.TryEnd)).Append(':').Append(b.Instructions.IndexOf(e.HandlerStart)).Append(':').Append(b.Instructions.IndexOf(e.HandlerEnd)).Append(':').Append(b.Instructions.IndexOf(e.FilterStart)).Append(':').Append(e.CatchType);
            return s.ToString();
        }
        static readonly string[] Files = { "Mortal.Core.dll", "Mortal.Story.dll", "Mortal.Free.dll", "Fungus.dll", "mscorlib.dll", "UnityEngine.CoreModule.dll" };
        static readonly string[] FullTypes =
        {
            "Mortal.Core.SaveSystem", "Mortal.Story.DiceMenuDialog", "Mortal.Story.DiceCheckResult", "Mortal.Story.DiceCheckAddition",
            "System.Random", "UnityEngine.Random",
        };
        static readonly string[] BodyMethods =
        {
            "Mortal.Core.CustomMenuDialog::InteractableCacheButton", "Mortal.Core.CustomMenuDialog::get_CachedButtons",
            "Fungus.LuaEnvironment::LoadLuaFunction",
        };
        static readonly string[] Members =
        {
            "Mortal.Core.GameSave::StartStoryScript",
            "Mortal.Core.PlayerStatManagerData::get_Instance", "Mortal.Core.PlayerStatManagerData::get_CurrentStoryScript",
            "Mortal.Core.PlayerStatManagerData::SetStoryScript", "Mortal.Core.PlayerStatManagerData::SetStartScript",
            "Mortal.Core.SceneController::get_Instance", "Mortal.Core.SceneController::get_IsPrepare", "Mortal.Core.SceneController::get_IsLoading",
            "Mortal.Core.SceneController::LoadTitle", "Mortal.Core.SceneController::LoadNewScene",
            "Mortal.Core.SoundManager::get_Instance", "Mortal.Core.SoundManager::_currentMusic", "Mortal.Core.SoundManager::_currentEnvSound",
            "Mortal.Core.SoundManager::PlayMusic", "Mortal.Core.SoundManager::PlayEnvSound", "Mortal.Core.SoundManager::StopMusic",
            "Mortal.Free.FreeManager::get_Instance", "Mortal.Free.FreeManager::_rand",
            "Mortal.Free.FreeManager::get_IsProcessing", "Mortal.Free.FreeManager::get_EnableHotKey",
        };
        static bool IsFull(string type)
        {
            return FullTypes.Any(t => type == t || type.StartsWith(t + "/", StringComparison.Ordinal));
        }
        public static SortedDictionary<string, string> Snapshot(string managed)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Files)
            using (var module = ModuleDefinition.ReadModule(Path.Combine(managed, file)))
            foreach (var type in Types(module.Types))
            {
                string prefix = file + "|";
                if (IsFull(type.FullName))
                {
                    var shape = new StringBuilder(type.FullName + "|" + ((int)type.Attributes) + "|" + type.BaseType + "|" + type.PackingSize + "|" + type.ClassSize + "|" + Attributes(type));
                    foreach (var face in type.Interfaces) shape.Append("\nI:").Append(face.InterfaceType.FullName);
                    // Preserve field order: native/Unity serialization may depend on it.
                    foreach (var f in type.Fields) shape.Append("\nF:").Append(f.FullName).Append('|').Append(((int)f.Attributes)).Append('|').Append(f.Offset).Append('|').Append(Value(f.Constant)).Append('|').Append(Value(f.InitialValue)).Append('|').Append(Attributes(f));
                    result[prefix + "T:" + type.FullName] = Digest(shape.ToString());
                    foreach (var method in type.Methods) result[prefix + "M:" + method.FullName] = Digest(Body(method));
                    continue;
                }
                foreach (var method in type.Methods)
                    if (BodyMethods.Contains(type.FullName + "::" + method.Name)) result[prefix + "M:" + method.FullName] = Digest(Body(method));
                foreach (string member in Members)
                {
                    if (!member.StartsWith(type.FullName + "::", StringComparison.Ordinal)) continue;
                    string name = member.Substring(type.FullName.Length + 2);
                    var signatures = type.Fields.Where(f => f.Name == name).Select(f => f.FullName + "|" + (f.IsStatic ? "static" : "instance"))
                        .Concat(type.Methods.Where(m => m.Name == name).Select(m => m.FullName + "|" + (m.IsStatic ? "static" : "instance")))
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (signatures.Length != 0) result[prefix + "S:" + member] = Digest(String.Join("\n", signatures));
                }
            }
            return result;
        }
        public static string Serialize(SortedDictionary<string, string> snapshot)
        { return String.Join("\n", snapshot.Select(p => p.Value + "\t" + p.Key).ToArray()) + "\n"; }
        public static SortedDictionary<string, string> Parse(string text)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                var at = line.IndexOf('\t');
                if (at != 64) throw new InvalidDataException("Invalid compatibility contract");
                result.Add(line.Substring(at + 1).TrimEnd('\r'), line.Substring(0, at));
            }
            if (result.Count < 50) throw new InvalidDataException("Incomplete compatibility contract");
            return result;
        }
        public static List<string> Differences(SortedDictionary<string, string> expected, SortedDictionary<string, string> current)
        {
            var differences = new List<string>();
            foreach (var p in expected) { string value; if (!current.TryGetValue(p.Key, out value) || value != p.Value) differences.Add(p.Key); }
            return differences;
        }
    }
}

