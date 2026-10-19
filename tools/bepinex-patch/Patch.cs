// 给 BepInEx.Unity.Mono.Preloader.dll 的 MonoAssemblyHelper 加备用的 Mono 库名。
// 原版只用 Doorstop 设的 DOORSTOP_MONO_LIB_PATH（完整路径）去找 Mono 运行库，游戏装在带中文等非 ASCII 字符的路径下会找不到，
// BepInEx 预加载直接失败（No matching entry point found for imageOpen，BepInEx/BepInEx#1016）。
// 实测（中文路径的文件夹链接启动）：环境变量的值是对的，但 MonoMod 用 ANSI 版 LoadLibrary 打开带中文的完整路径会失败；
// 按名字 mono-2.0-bdwgc.dll 能直接找到游戏已经加载的 Mono 库（不带 .dll 打不开）。
// 所以把 mono-2.0-bdwgc.dll 插在环境变量前面，正常路径和中文路径找到的都是同一个库。
// 用法：patch.exe <BepInEx.Unity.Mono.Preloader.dll> [--dump]
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
	private static readonly string[] Fallbacks = { "mono-2.0-bdwgc.dll" };

	private static int Main(string[] args)
	{
		string path = args[0];
		var resolver = new DefaultAssemblyResolver();
		resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(path));
		using (var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = !args.Contains("--dump") }))
		{
			var type = asm.MainModule.Types.First(t => t.FullName == "BepInEx.Unity.Mono.Preloader.Utils.MonoAssemblyHelper");
			var cctor = type.Methods.First(m => m.Name == ".cctor");
			var il = cctor.Body.Instructions;
			if (args.Contains("--dump"))
			{
				foreach (var ins in il) Console.WriteLine(ins);
				return 0;
			}
			if (il.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == Fallbacks[0]))
			{
				Console.WriteLine("already patched");
				return 0;
			}
			// 找到把 DOORSTOP_MONO_LIB_PATH 加进列表的那次 Add，在它后面照样再加几个备用名字
			int envIndex = il.ToList().FindIndex(i => (i.Operand as MemberReference) != null && ((MemberReference)i.Operand).Name.Contains("DOORSTOP_MONO_LIB_PATH"));
			if (envIndex < 0) { Console.WriteLine("env var load not found"); return 1; }
			var convert = il.Skip(envIndex + 1).First(i => i.OpCode == OpCodes.Call);
			var add = il.Skip(envIndex + 1).First(i => (i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Call) && ((MethodReference)i.Operand).Name == "Add");
			bool needsConvert = il.IndexOf(convert) < il.IndexOf(add);
			// 插在「dup; 读环境变量; 转换; Add」这一组的 dup 前面
			var before = il[envIndex - 1];
			if (before.OpCode != OpCodes.Dup) { Console.WriteLine("unexpected IL"); return 1; }
			var processor = cctor.Body.GetILProcessor();
			foreach (string name in Fallbacks)
			{
				var seq = new System.Collections.Generic.List<Instruction> { processor.Create(OpCodes.Dup), processor.Create(OpCodes.Ldstr, name) };
				if (needsConvert) seq.Add(processor.Create(OpCodes.Call, (MethodReference)convert.Operand));
				seq.Add(processor.Create(add.OpCode, (MethodReference)add.Operand));
				foreach (var ins in seq) processor.InsertBefore(before, ins);
			}
			asm.Write();
			Console.WriteLine("patched");
			return 0;
		}
	}
}
