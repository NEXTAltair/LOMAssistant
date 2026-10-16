using System;

namespace LOMAssistant.Config;

public interface IConfigElement
{
	string Name { get; }

	string Description { get; }

	Type ElementType { get; }

	object BoxedValue { get; set; }

	object DefaultValue { get; }

	Action OnValueChangedNotify { get; set; }

	object GetLoaderConfigValue();

	void RevertToDefaultValue();
}
