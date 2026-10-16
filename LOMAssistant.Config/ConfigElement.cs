using System;

namespace LOMAssistant.Config;

public class ConfigElement<T> : IConfigElement
{
	public Action<T> OnValueChanged;

	private T m_value;

	public string Name { get; }

	public string Description { get; }

	public Type ElementType => typeof(T);

	public Action OnValueChangedNotify { get; set; }

	public object DefaultValue { get; }

	public T Value
	{
		get
		{
			return m_value;
		}
		set
		{
			SetValue(value);
		}
	}

	object IConfigElement.BoxedValue
	{
		get
		{
			return m_value;
		}
		set
		{
			SetValue((T)value);
		}
	}

	public ConfigElement(string name, string description, T defaultValue)
	{
		Name = name;
		Description = description;
		m_value = defaultValue;
		DefaultValue = defaultValue;
		ConfigManager.RegisterConfigElement(this);
	}

	private void SetValue(T value)
	{
		if (!object.Equals(m_value, value))
		{
			m_value = value;
			ConfigManager.SetConfigValue(this, value);
			OnValueChanged?.Invoke(value);
			OnValueChangedNotify?.Invoke();
			ConfigManager.SaveConfig();
		}
	}

	public object GetLoaderConfigValue()
	{
		return ConfigManager.GetConfigValue(this);
	}

	public void RevertToDefaultValue()
	{
		Value = (T)DefaultValue;
	}
}
