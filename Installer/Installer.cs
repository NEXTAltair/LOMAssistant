using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LOMAssistantInstaller;

/// <summary>
/// 咖啡伴侣一键安装器。放在发布压缩包根目录，和 BepInEx\、winhttp.dll 等安装文件放在一起。
/// 1. 自动从 Steam 库里找游戏目录（找不到可手动选）。
/// 2. 列出 BepInEx\plugins 里的其他 Mod，已知会冲突的默认勾选，安装时移到「已停用的Mod」文件夹（不删除）。
/// 3. 先从 GitHub 下载最新版安装；连不上或下载失败，就安装压缩包里自带的这一版。
/// 4. 不覆盖 BepInEx\config 里的配置。
/// </summary>
internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		string game = null;
		string autoLog = null;
		for (int i = 0; i < args.Length - 1; i++)
		{
			if (args[i] == "--game")
			{
				game = args[i + 1];
			}
			else if (args[i] == "--diag")
			{
				InstallerForm.DiagLogArg = args[i + 1]; // 测试用：导出诊断后把路径写到这个文件并退出
			}
			else if (args[i] == "--auto")
			{
				autoLog = args[i + 1]; // 测试用：自动安装，把日志写到这个文件后退出
			}
		}
		Application.Run(new InstallerForm(game, autoLog));
	}
}

internal class InstallerForm : Form
{
	private const string Repo = "kevinnie2003/LOMAssistant";
	private const string ModDll = @"BepInEx\plugins\LOMAssistant\LOMAssistant.dll";
	private const string DisabledFolder = @"BepInEx\已停用的Mod";

	// 文件名包含这些字样的插件，已知和本 Mod 改的是同一批游戏代码
	private static readonly string[][] KnownConflicts =
	{
		new[] { "ScriptTrainer", "多功能 Mod（Jim97）" },
		new[] { "ControlMyDestiny", "知天命" },
		new[] { "ZhaoHuoSystem", "赵活系统" },
		new[] { "DiceMaster", "骰子大师" },
		new[] { "MortalAnytimeSave", "随时存档（独立版，本 Mod 已内置）" },
		new[] { "骰子", "骰子大师" },
		new[] { "LOMAssistant", "活侠传助手 / 本 Mod 的旧版" },
		new[] { "活侠传助手", "活侠传助手" },
	};

	// 配色取自游戏界面：墨色标题栏、米色底、朱红按钮
	private static readonly Color Ink = Color.FromArgb(43, 38, 34);
	private static readonly Color Paper = Color.FromArgb(246, 242, 234);
	private static readonly Color Card = Color.FromArgb(255, 253, 249);
	private static readonly Color Accent = Color.FromArgb(140, 47, 38);
	private static readonly Color AccentHover = Color.FromArgb(166, 60, 48);
	private static readonly Color Muted = Color.FromArgb(120, 110, 98);

	private readonly string _payload = AppDomain.CurrentDomain.BaseDirectory;
	private readonly TextBox _gameBox = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
	private readonly Label _versions = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
	private readonly Label _pluginsNone = new Label { AutoSize = true, Text = "没有发现其他 Mod。", ForeColor = Color.FromArgb(120, 110, 98) };
	private readonly CheckedListBox _plugins = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, Height = 84 };
	private readonly TextBox _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
	private readonly CheckBox _skipLogo = new CheckBox { Text = "跳过游戏开头Logo（取消勾选恢复Logo）", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 0, 6) };
	private readonly Button _install = new Button { Text = "一键安装 / 更新", Dock = DockStyle.Fill, Height = 46, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
	private readonly Button _uninstall = new Button { Text = "卸载本 Mod", Dock = DockStyle.Fill, Height = 34, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
	private readonly Button _export = new Button { Text = "导出诊断（Mod 不生效时用，打包日志发给作者）", Dock = DockStyle.Fill, Height = 34, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
	private readonly Button _browse = new Button { Text = "选择…", AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
	private readonly List<string> _pluginPaths = new List<string>();

	private string _game;
	private Version _latest;
	private string _latestUrl;
	private bool _onlineDone;

	private readonly string _autoLog;
	internal static string DiagLogArg;
	private readonly string _diagLog = DiagLogArg;

	public InstallerForm(string game, string autoLog)
	{
		_autoLog = autoLog;
		Text = "咖啡伴侣 安装器";
		Font = new Font("Microsoft YaHei UI", 10f);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(640, 760);
		MinimumSize = new Size(560, 680);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Paper;

		// 标题栏
		var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Ink, Padding = new Padding(18, 12, 18, 12) };
		var title = new Label { Text = "咖啡伴侣", AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold), Margin = new Padding(0) };
		var subtitle = new Label { Text = "一键安装 / 更新 · 自动检测冲突 Mod · 不会覆盖你的设置", AutoSize = true, ForeColor = Color.FromArgb(201, 191, 175), Margin = new Padding(2, 4, 0, 0) };
		header.Controls.Add(title);
		header.Controls.Add(subtitle);

		var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 16), ColumnCount = 2, BackColor = Paper };
		body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

		var logCard = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(8) };
		logCard.Controls.Add(_log);
		Control[] rows =
		{
			Heading("游戏目录"),
			null, // 目录框 + 选择按钮
			_versions,
			Heading("其他 Mod"),
			new Label { AutoSize = true, ForeColor = Muted, Text = "勾选的会在安装时移到「BepInEx\\已停用的Mod」，不会删除；标了「已知冲突」的建议勾选。" },
			_pluginsNone,
			_plugins,
			new Panel { Height = 12, Dock = DockStyle.Fill },
			_skipLogo,
			_install,
			_export,
			_uninstall,
			Heading("日志"),
			logCard,
		};
		body.RowCount = rows.Length;
		for (int i = 0; i < rows.Length; i++)
		{
			body.RowStyles.Add(i == rows.Length - 1 ? new RowStyle(SizeType.Percent, 100f) : new RowStyle(SizeType.AutoSize));
			if (rows[i] == null)
			{
				body.Controls.Add(_gameBox, 0, i);
				body.Controls.Add(_browse, 1, i);
			}
			else
			{
				body.Controls.Add(rows[i], 0, i);
				body.SetColumnSpan(rows[i], 2);
			}
		}

		_log.BackColor = Card;
		_log.ForeColor = Color.FromArgb(70, 62, 55);
		_gameBox.BackColor = Card;
		_plugins.BackColor = Card;
		_install.BackColor = Accent;
		_install.ForeColor = Color.White;
		_install.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
		_install.FlatAppearance.BorderSize = 0;
		_install.FlatAppearance.MouseOverBackColor = AccentHover;
		_install.FlatAppearance.MouseDownBackColor = Ink;
		_export.BackColor = Card;
		_export.ForeColor = Ink;
		_export.Margin = new Padding(0, 6, 0, 0);
		_export.FlatAppearance.BorderColor = Color.FromArgb(200, 190, 175);
		_uninstall.BackColor = Card;
		_uninstall.ForeColor = Muted;
		_uninstall.Margin = new Padding(0, 6, 0, 0);
		_uninstall.FlatAppearance.BorderColor = Color.FromArgb(200, 190, 175);
		_browse.BackColor = Card;
		_browse.FlatAppearance.BorderColor = Color.FromArgb(200, 190, 175);
		_browse.Margin = new Padding(8, 3, 0, 3);

		Controls.Add(body);
		Controls.Add(header);

		_gameBox.TabStop = false;
		ActiveControl = _install;
		_browse.Click += delegate { BrowseGame(); };
		_install.Click += delegate { StartInstall(); };
		_export.Click += delegate { ExportDiagnostics(); };
		_uninstall.Click += delegate { Uninstall(); };

		Log("咖啡伴侣 安装器");
		SetGame(game ?? FindGame());
		if (_game == null)
		{
			Log("没有自动找到游戏目录，请点「选择…」手动选择游戏目录（里面有 Mortal.exe）。");
		}
		CheckOnline();
		if (_autoLog != null)
		{
			Shown += delegate { StartInstall(); };
		}
		else if (_diagLog != null)
		{
			Shown += delegate { ExportDiagnostics(); };
		}
	}

	private Label Heading(string text)
	{
		return new Label { Text = text, AutoSize = true, ForeColor = Ink, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 8, 0, 4) };
	}

	// 一行写明：已安装哪版，这次会装哪版、从哪来
	private void UpdateVersions()
	{
		Version installed = _game != null ? DllVersion(Path.Combine(_game, ModDll)) : null;
		Version bundled = BundledVersion();
		string target;
		if (!_onlineDone)
		{
			target = "正在检查 GitHub…";
		}
		else if (_latest != null && (bundled == null || _latest > bundled && Short(_latest) != Short(bundled)))
		{
			target = Short(_latest) + "（从 GitHub 下载）";
		}
		else if (bundled != null)
		{
			target = Short(bundled) + (_latest != null ? "（已是最新）" : "（压缩包自带，GitHub 连不上）");
		}
		else
		{
			target = "无（连不上 GitHub，且这个安装器不在解压后的压缩包里）";
		}
		if (installed != null && !target.StartsWith("正在") && target.StartsWith(Short(installed) + "（"))
		{
			target = Short(installed) + "（已是最新）";
		}
		_versions.Text = "已安装：" + (_game == null ? "-" : installed != null ? Short(installed) : "未安装") + "　　将安装：" + target;
	}

	private void Log(string message)
	{
		if (InvokeRequired)
		{
			BeginInvoke(new Action<string>(Log), message);
			return;
		}
		_log.AppendText(message + Environment.NewLine);
	}

	private static string Short(Version v)
	{
		return v.Major + "." + v.Minor + "." + Math.Max(0, v.Build);
	}

	private Version BundledVersion()
	{
		return DllVersion(Path.Combine(_payload, ModDll));
	}

	private static Version DllVersion(string path)
	{
		try
		{
			return File.Exists(path) ? AssemblyName.GetAssemblyName(path).Version : null;
		}
		catch
		{
			return null;
		}
	}

	// ---------------- 游戏目录 ----------------

	private static bool IsGameDir(string dir)
	{
		return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Mortal.exe"));
	}

	private string FindGame()
	{
		if (IsGameDir(_payload))
		{
			return _payload.TrimEnd('\\');
		}
		var steamRoots = new List<string>();
		foreach (string value in new[]
		{
			RegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
			RegistryValue(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
			RegistryValue(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
		})
		{
			if (!string.IsNullOrEmpty(value))
			{
				steamRoots.Add(value.Replace('/', '\\'));
			}
		}
		var libraries = new List<string>(steamRoots);
		foreach (string root in steamRoots)
		{
			string vdf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
			try
			{
				if (File.Exists(vdf))
				{
					foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
					{
						libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
					}
				}
			}
			catch
			{
			}
		}
		foreach (string lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			string dir = Path.Combine(lib, @"steamapps\common\LegendOfMortal");
			if (IsGameDir(dir))
			{
				return dir;
			}
		}
		return null;
	}

	private static string RegistryValue(RegistryKey hive, string key, string name)
	{
		try
		{
			using (RegistryKey k = hive.OpenSubKey(key))
			{
				return k != null ? k.GetValue(name) as string : null;
			}
		}
		catch
		{
			return null;
		}
	}

	private void BrowseGame()
	{
		using (var dialog = new FolderBrowserDialog { Description = "选择《活侠传》游戏目录（里面有 Mortal.exe）" })
		{
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			if (!IsGameDir(dialog.SelectedPath))
			{
				MessageBox.Show(this, "这个文件夹里没有 Mortal.exe，不是游戏目录。\n\nSteam 中右键《活侠传》→ 管理 → 浏览本地文件，打开的就是游戏目录。", "选错了", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			SetGame(dialog.SelectedPath);
		}
	}

	// Steam 在注册表里存的路径是全小写（d:/steam），这里换成磁盘上的实际大小写，只为显示好看
	private static string RealCase(string path)
	{
		// 从盘符开始逐级往下找实际名字（不能往上递归：「C:」会被当成 C 盘的当前目录，导致无限递归）
		try
		{
			string full = Path.GetFullPath(path);
			string root = Path.GetPathRoot(full);
			string current = root.ToUpperInvariant();
			foreach (string part in full.Substring(root.Length).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string[] match = Directory.GetDirectories(current, part);
				current = Path.Combine(current, match.Length > 0 ? Path.GetFileName(match[0]) : part);
			}
			return current;
		}
		catch
		{
			return path;
		}
	}

	private void SetGame(string dir)
	{
		_game = IsGameDir(dir) ? RealCase(dir) : null;
		_gameBox.Text = _game ?? "（未找到，请点「选择…」）";
		UpdateVersions();
		RefreshPlugins();
		if (_game != null)
		{
			Log("游戏目录：" + _game);
		}
	}

	private void RefreshPlugins()
	{
		_plugins.Items.Clear();
		_pluginPaths.Clear();
		string plugins = _game != null ? Path.Combine(_game, @"BepInEx\plugins") : null;
		if (plugins != null && Directory.Exists(plugins))
		{
			CollectPlugins(plugins);
		}
		string patchers = _game != null ? Path.Combine(_game, @"BepInEx\patchers") : null;
		if (patchers != null && Directory.Exists(patchers))
		{
			CollectPatchers(patchers);
		}
		_plugins.Visible = _pluginPaths.Count > 0;
		_pluginsNone.Visible = _pluginPaths.Count == 0;
	}

	private void CollectPlugins(string plugins)
	{
		string ours = Path.Combine(plugins, "LOMAssistant") + "\\";
		foreach (string dll in Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories))
		{
			if (dll.StartsWith(ours, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string rel = dll.Substring(plugins.Length + 1);
			string known = null;
			foreach (string[] pair in KnownConflicts)
			{
				if (rel.IndexOf(pair[0], StringComparison.OrdinalIgnoreCase) >= 0)
				{
					known = pair[1];
					break;
				}
			}
			_pluginPaths.Add(dll);
			_plugins.Items.Add(rel + (known != null ? "　【已知冲突：" + known + "】" : ""), known != null);
		}
	}

	// 本 Mod 不用预加载补丁，patchers 里的 DLL 都是别的 Mod 留下的。有它们时 BepInEx 预加载阶段会去加载，
	// 在部分电脑上直接失败（Could not run preloader），整个 BepInEx 都起不来，所以默认停用
	private void CollectPatchers(string patchers)
	{
		foreach (string dll in Directory.GetFiles(patchers, "*.dll", SearchOption.AllDirectories))
		{
			_pluginPaths.Add(dll);
			_plugins.Items.Add(@"patchers\" + dll.Substring(patchers.Length + 1) + "　【预加载补丁：本 Mod 不需要，可能导致 BepInEx 启动失败】", true);
		}
	}

	// ---------------- 卸载 ----------------

	// 删掉本 Mod 和它的配置，恢复开头 Logo。还有别的 Mod 就把 BepInEx 加载器留给它们；没有了就连加载器一起删，
	// 游戏目录回到没装 Mod 的样子。「已停用的Mod」、别的 Mod 的配置和存档（在游戏存档文件夹里，含随时存档、剧情回顾收录）都不动
	private void Uninstall()
	{
		if (_game == null)
		{
			MessageBox.Show(this, "请先选择游戏目录。", "卸载", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (Process.GetProcessesByName("Mortal").Length > 0)
		{
			MessageBox.Show(this, "游戏正在运行，请先退出游戏再卸载。", "卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}
		RefreshPlugins();
		int others = _pluginPaths.Count;
		string question = "确定卸载咖啡伴侣吗？\n\n"
			+ (others > 0
				? "还有 " + others + " 个别的 Mod，BepInEx 加载器会留给它们用，只删本 Mod。"
				: "没有别的 Mod 了，BepInEx 加载器也一起删掉，游戏回到没装 Mod 的样子。")
			+ "\n\n存档不受影响（随时存档、剧情回顾收录的节点也留着，以后重装还能接着用）。";
		if (MessageBox.Show(this, question, "卸载", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
		{
			return;
		}
		try
		{
			try
			{
				Log(SplashPatch.Apply(_game, false));
			}
			catch (Exception e)
			{
				Log("恢复开头 Logo 失败（" + e.Message + "）。");
			}
			DeletePath(@"BepInEx\plugins\LOMAssistant");
			DeletePath(@"BepInEx\config\LOMAssistant.cfg");
			if (others == 0)
			{
				foreach (string rel in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt", @"BepInEx\core", @"BepInEx\cache", @"BepInEx\LogOutput.log", @"BepInEx\ErrorLog.log" })
				{
					DeletePath(rel);
				}
				// 剩下的文件夹里没有文件了就删掉（「已停用的Mod」、别的 Mod 的配置还在的话会留下）
				foreach (string rel in new[] { @"BepInEx\plugins", @"BepInEx\patchers", @"BepInEx\config", "BepInEx" })
				{
					string dir = Path.Combine(_game, rel);
					if (Directory.Exists(dir) && Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
					{
						DeletePath(rel);
					}
				}
			}
			bool disabled = Directory.Exists(Path.Combine(_game, DisabledFolder));
			Finish(true, "已卸载咖啡伴侣。" + (disabled ? "\n\n以前停用的别的 Mod 还在「BepInEx\\已停用的Mod」里，想用的话自己挪回去。" : ""), "卸载");
		}
		catch (UnauthorizedAccessException)
		{
			AskElevate();
		}
		catch (Exception e)
		{
			Finish(false, "卸载没有完成：" + e.Message + "\n\n请确认游戏已退出，再试一次。", "卸载");
		}
	}

	private void DeletePath(string rel)
	{
		string path = Path.Combine(_game, rel);
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive: true);
		}
		else if (File.Exists(path))
		{
			File.Delete(path);
		}
		else
		{
			return;
		}
		Log("已删除 " + rel);
	}

	// ---------------- 联网检查 ----------------

	private void CheckOnline()
	{
		var thread = new Thread(() =>
		{
			try
			{
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
				var request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/releases/latest");
				request.UserAgent = "LOMAssistant-Installer";
				request.Timeout = 8000;
				request.ReadWriteTimeout = 8000;
				string json;
				using (var response = request.GetResponse())
				using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
				{
					json = reader.ReadToEnd();
				}
				Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9.]+)\"");
				Match url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+LOMAssistant\\.zip)\"");
				if (tag.Success && url.Success)
				{
					_latest = new Version(tag.Groups[1].Value);
					_latestUrl = url.Groups[1].Value;
				}
			}
			catch (Exception e)
			{
				Log("连不上 GitHub（" + e.Message + "），将安装压缩包自带的版本。");
			}
			_onlineDone = true;
			BeginInvoke(new Action(() =>
			{
				UpdateVersions();
			}));
		}) { IsBackground = true };
		thread.Start();
	}

	// ---------------- 导出诊断 ----------------

	private void ExportDiagnostics()
	{
		try
		{
			string path = Diagnostics.Export(_game, _log.Text);
			Log("已导出诊断：" + path);
			try
			{
				Clipboard.SetText(path);
			}
			catch
			{
			}
			if (_diagLog != null)
			{
				File.WriteAllText(_diagLog, path, Encoding.UTF8);
				Close();
				return;
			}
			MessageBox.Show(this, "已导出到桌面：\n" + Path.GetFileName(path) + "\n\n路径已复制。反馈问题时把这个压缩包发给作者（QQ 群 496887060）。", "导出诊断", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception e)
		{
			Log("导出诊断失败：" + e.Message);
			MessageBox.Show(this, "导出失败：" + e.Message, "导出诊断", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	// ---------------- 安装 ----------------

	private void StartInstall()
	{
		if (_game == null)
		{
			MessageBox.Show(this, "请先选择游戏目录。", "安装", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (Process.GetProcessesByName("Mortal").Length > 0)
		{
			MessageBox.Show(this, "游戏正在运行，请先退出游戏再安装。", "安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}
		var disable = new List<string>();
		for (int i = 0; i < _pluginPaths.Count; i++)
		{
			if (_plugins.GetItemChecked(i))
			{
				disable.Add(_pluginPaths[i]);
			}
		}
		_install.Enabled = false;
		_browse.Enabled = false;
		var thread = new Thread(() => Install(disable)) { IsBackground = true };
		thread.Start();
	}

	private void Install(List<string> disable)
	{
		try
		{
			for (int i = 0; i < 40 && !_onlineDone; i++)
			{
				Thread.Sleep(250);
			}
			// 已经是最新版（且加载器在）就不再下载、覆盖，只处理勾选的冲突 Mod
			Version installed = DllVersion(Path.Combine(_game, ModDll));
			Version newest = BundledVersion();
			if (_latest != null && (newest == null || _latest > newest))
			{
				newest = _latest;
			}
			// 混进了 BepInEx 5 的文件时，加载器多半被换掉了，版本一样也要重装一遍
			bool mixed = File.Exists(Path.Combine(_game, @"BepInEx\core\BepInEx.dll")) || File.Exists(Path.Combine(_game, @"BepInEx\core\BepInEx.Preloader.dll"));
			if (installed != null && newest != null && File.Exists(Path.Combine(_game, "winhttp.dll")) && !mixed
				&& (Short(installed) == Short(newest) || installed > newest))
			{
				DisablePlugins(disable);
				Logo();
				Finish(true, "已经是最新版本 " + Short(installed) + "，不需要重新安装。" + (disable.Count > 0 ? "\n\n勾选的 Mod 已移到「BepInEx\\已停用的Mod」。" : ""));
				return;
			}
			string source = ChooseSource();
			if (source == null)
			{
				Finish(false, "没有可安装的文件：压缩包里没有安装文件，也没能从 GitHub 下载。请把整个压缩包解压后再运行安装器。");
				return;
			}
			DisablePlugins(disable);
			RemoveBepInEx5Core();
			int copied = CopyTree(source, _game);
			Log("已复制 " + copied + " 个文件。");
			Version now = DllVersion(Path.Combine(_game, ModDll));
			if (now == null || !File.Exists(Path.Combine(_game, "winhttp.dll")))
			{
				Finish(false, "安装后检查失败：游戏目录里缺少 Mod 文件。");
				return;
			}
			Logo();
			Finish(true, "安装完成，当前版本 " + Short(now) + "。\n\n正常启动游戏即可，游戏里按 F8 打开设置，Tab 打开咖啡伴侣窗口。");
		}
		catch (UnauthorizedAccessException e)
		{
			// 写进日志是哪个文件被拒绝（只读、杀毒软件拦截、文件夹权限），导出诊断时看得到
			Log("写入被拒绝：" + e.Message);
			BeginInvoke(new Action(AskElevate));
		}
		catch (Exception e)
		{
			Finish(false, "安装失败：" + e.Message);
		}
	}

	private void Logo()
	{
		try
		{
			Log(SplashPatch.Apply(_game, _skipLogo.Checked));
		}
		catch (Exception e)
		{
			Log("设置开头 Logo 失败（" + e.Message + "），不影响 Mod 使用。");
		}
	}

	// 有更新的版本就从 GitHub 下载；否则（或下载失败）用压缩包自带的
	private string ChooseSource()
	{
		Version bundled = BundledVersion();
		bool hasBundled = bundled != null;
		if (_latest != null && _latestUrl != null && (bundled == null || Short(_latest) != Short(bundled) && _latest > bundled))
		{
			try
			{
				return Download();
			}
			catch (Exception e)
			{
				Log("下载最新版失败（" + e.Message + "）。");
			}
		}
		if (hasBundled)
		{
			Log("安装压缩包自带的版本 " + Short(bundled) + "。");
			return _payload;
		}
		return null;
	}

	private string Download()
	{
		string temp = Path.Combine(Path.GetTempPath(), "LOMAssistant_update");
		if (Directory.Exists(temp))
		{
			Directory.Delete(temp, recursive: true);
		}
		Directory.CreateDirectory(temp);
		string zip = Path.Combine(temp, "LOMAssistant.zip");
		Log("正在从 GitHub 下载 " + Short(_latest) + "…");
		var request = (HttpWebRequest)WebRequest.Create(_latestUrl);
		request.UserAgent = "LOMAssistant-Installer";
		request.Timeout = 15000;
		request.ReadWriteTimeout = 15000;
		using (var response = request.GetResponse())
		using (Stream input = response.GetResponseStream())
		using (FileStream output = File.Create(zip))
		{
			input.CopyTo(output);
		}
		string extracted = Path.Combine(temp, "files");
		ZipFile.ExtractToDirectory(zip, extracted, Encoding.UTF8);
		if (DllVersion(Path.Combine(extracted, ModDll)) == null)
		{
			throw new InvalidDataException("下载的压缩包里没有 Mod 文件");
		}
		Log("下载完成。");
		return extracted;
	}

	private void DisablePlugins(List<string> dlls)
	{
		if (dlls.Count == 0)
		{
			return;
		}
		string plugins = Path.Combine(_game, @"BepInEx\plugins");
		string patchers = Path.Combine(_game, @"BepInEx\patchers");
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string dll in dlls)
		{
			// patchers 里的停用到单独的子文件夹，和插件分开
			bool isPatcher = dll.StartsWith(patchers + "\\", StringComparison.OrdinalIgnoreCase);
			string root = isPatcher ? patchers : plugins;
			string target = Path.Combine(_game, DisabledFolder, stamp, isPatcher ? "patchers" : "");
			// 在根目录就只移这个 DLL；在子文件夹里就把整个子文件夹移走
			string rel = dll.Substring(root.Length + 1);
			int slash = rel.IndexOf('\\');
			string item = slash < 0 ? dll : Path.Combine(root, rel.Substring(0, slash));
			if (!moved.Add(item) || !(File.Exists(item) || Directory.Exists(item)))
			{
				continue;
			}
			Directory.CreateDirectory(target);
			string dest = Path.Combine(target, Path.GetFileName(item));
			for (int n = 2; File.Exists(dest) || Directory.Exists(dest); n++)
			{
				dest = Path.Combine(target, Path.GetFileNameWithoutExtension(item) + "_" + n + Path.GetExtension(item));
			}
			if (Directory.Exists(item))
			{
				Directory.Move(item, dest);
			}
			else
			{
				File.Move(item, dest);
			}
			Log("已停用：" + (isPatcher ? @"patchers\" : "") + item.Substring(root.Length + 1) + "（移到 " + DisabledFolder + "）");
		}
	}

	// 有的 Mod（如立绘替换「邪神认知污染ON」）自带 BepInEx 5，照它的说明装会把 BepInEx 5 的核心文件混进来，
	// 和本 Mod 用的 BepInEx 6 混在一起游戏可能起不来。这些文件 BepInEx 6 用不到，删掉；共用的文件接下来会被覆盖成 BepInEx 6 的
	private void RemoveBepInEx5Core()
	{
		string core = Path.Combine(_game, @"BepInEx\core");
		var removed = new List<string>();
		foreach (string name in new[] { "BepInEx.dll", "BepInEx.xml", "BepInEx.Preloader.dll", "BepInEx.Preloader.xml", "BepInEx.Harmony.dll", "BepInEx.Harmony.xml", "HarmonyXInterop.dll", "0Harmony20.dll" })
		{
			string path = Path.Combine(core, name);
			if (File.Exists(path))
			{
				File.Delete(path);
				removed.Add(name);
			}
		}
		if (removed.Count > 0)
		{
			Log("清理了混进来的 BepInEx 5 文件：" + string.Join("、", removed.ToArray()));
		}
	}

	private int CopyTree(string source, string dest)
	{
		if (SamePath(source, dest))
		{
			Log("安装文件已经在游戏目录里，跳过复制。");
			return 0;
		}
		string self = Path.GetFileName(Application.ExecutablePath);
		int count = 0;
		foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
		{
			string rel = file.Substring(source.TrimEnd('\\').Length + 1);
			if (rel.Equals(self, StringComparison.OrdinalIgnoreCase)
				|| rel.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && rel.IndexOf('\\') < 0
				|| rel.Equals("使用说明.txt", StringComparison.OrdinalIgnoreCase)
				|| rel.StartsWith(@"BepInEx\config\", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string target = Path.Combine(dest, rel);
			Directory.CreateDirectory(Path.GetDirectoryName(target));
			// 目标文件是只读的话覆盖会被拒绝：先去掉只读
			if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
			{
				File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
				Log("已去掉只读属性：" + rel);
			}
			File.Copy(file, target, overwrite: true);
			count++;
		}
		return count;
	}

	private static bool SamePath(string a, string b)
	{
		return a != null && b != null && string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
	}

	private void Finish(bool ok, string message, string title = null)
	{
		Log(message.Replace("\n\n", " "));
		BeginInvoke(new Action(() =>
		{
			if (_autoLog != null)
			{
				File.WriteAllText(_autoLog, (ok ? "OK" : "FAIL") + Environment.NewLine + _log.Text, Encoding.UTF8);
				Close();
				return;
			}
			SetGame(_game);
			_install.Enabled = true;
			_browse.Enabled = true;
			MessageBox.Show(this, message, title ?? (ok ? "安装完成" : "安装失败"), MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
		}));
	}

	/// <summary>
	/// 写不进游戏目录。以前会自己用 runas 重新打开成管理员，但在一些玩家电脑上不起作用（一直重复弹窗），
	/// 手动右键「以管理员身份运行」才行：所以直接告诉玩家怎么做。已经是管理员还写不进去，多半是杀毒软件拦的。
	/// </summary>
	private void AskElevate()
	{
		_install.Enabled = true;
		_browse.Enabled = true;
		bool admin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
			.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
		Log("没有写入游戏目录的权限（安装器" + (admin ? "已经是" : "不是") + "以管理员身份运行）。");
		string message = admin
			? "已经以管理员身份运行，还是写不进游戏目录。\n\n多半是杀毒软件拦住了：先退出 360、火绒、电脑管家等杀毒软件（或把游戏目录加进信任），再点一次「一键安装 / 更新」。\n\n还不行的话，点「导出诊断」把桌面上的压缩包发到 QQ 群。"
			: "没有写入游戏目录的权限（游戏可能装在 Program Files 里，或者被杀毒软件拦住了）。\n\n请关掉安装器，右键「咖啡伴侣安装器.exe」→「以管理员身份运行」，再点「一键安装 / 更新」。\n\n还不行的话，先退出 360、火绒、电脑管家等杀毒软件（或把游戏目录加进信任）再试。";
		MessageBox.Show(this, message, "没有写入权限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}
}
