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
    // Conservative semantic contract: do not trust version numbers, MVIDs or
    // metadata tokens. Keep all existing methods and type layouts in the four
    // gameplay assemblies, including compiler-generated coroutine state machines.
    // New unrelated types/methods are allowed; changed existing logic is not.
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
        public static SortedDictionary<string, string> Snapshot(string managed)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in new[] { "Mortal.Core.dll", "Mortal.Story.dll", "Mortal.Free.dll", "Fungus.dll", "mscorlib.dll", "UnityEngine.CoreModule.dll" })
            using (var module = ModuleDefinition.ReadModule(Path.Combine(managed, file)))
            foreach (var type in Types(module.Types))
            {
                if (file == "mscorlib.dll" && type.FullName != "System.Random") continue;
                if (file == "UnityEngine.CoreModule.dll" && type.FullName != "UnityEngine.Random/State" && type.FullName != "UnityEngine.Random") continue;
                string prefix = file + "|";
                var shape = new StringBuilder(type.FullName + "|" + ((int)type.Attributes) + "|" + type.BaseType + "|" + type.PackingSize + "|" + type.ClassSize + "|" + Attributes(type));
                foreach (var face in type.Interfaces) shape.Append("\nI:").Append(face.InterfaceType.FullName);
                // Preserve field order: native/Unity serialization may depend on it.
                foreach (var f in type.Fields) shape.Append("\nF:").Append(f.FullName).Append('|').Append(((int)f.Attributes)).Append('|').Append(f.Offset).Append('|').Append(Value(f.Constant)).Append('|').Append(Value(f.InitialValue)).Append('|').Append(Attributes(f));
                result[prefix + "T:" + type.FullName] = Digest(shape.ToString());
                foreach (var method in type.Methods) result[prefix + "M:" + method.FullName] = Digest(Body(method));
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
            if (result.Count < 100) throw new InvalidDataException("Incomplete compatibility contract");
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

