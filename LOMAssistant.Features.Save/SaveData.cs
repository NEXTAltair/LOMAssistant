using System.Collections.Generic;
using Mortal.Core;

namespace LOMAssistant.Features.Save;

public class SaveData
{
	public class PortraitData
	{
		public string toPosition;

		public string face;
	}

	public Dictionary<string, string> KV = new Dictionary<string, string>();

	public string CurView;

	public string CurStory;

	public string LastScript;

	public string RealLastScript;

	public string Scene;

	public GameSave lastSave;

	public GameSave initialSave;

	public Dictionary<string, PortraitData> portrait = new Dictionary<string, PortraitData>();

	public bool NeedSpeed
	{
		get
		{
			if (KV != null && KV.Count > 0 && Scene != "Free")
			{
				return Scene != "Battle";
			}
			return false;
		}
	}
}
