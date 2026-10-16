using System;
using System.Reflection;
using HarmonyLib;

namespace LOMAssistant;

public static class ReflectionExtensions
{
	public static T GetFieldValue<T>(this object instance, string fieldname)
	{
		T result;
		try
		{
			FieldInfo fieldInfo = AccessTools.Field(instance.GetType(), fieldname);
			return (T)((fieldInfo != null) ? fieldInfo.GetValue(instance) : null);
		}
		catch (Exception arg)
		{
			DicePlugin.LogError($"Error getting field {fieldname} from {instance.GetType().Name}: {arg}");
			result = default(T);
		}
		return result;
	}

	public static T GetClassFieldValue<T>(this Type type, string fieldname)
	{
		FieldInfo fieldInfo = AccessTools.Field(type, fieldname);
		return (T)((fieldInfo != null) ? fieldInfo.GetValue(null) : null);
	}

	public static void SetPrivateField(this object instance, string fieldname, object value)
	{
		FieldInfo fieldInfo = AccessTools.Field(instance.GetType(), fieldname);
		if (!(fieldInfo == null))
		{
			fieldInfo.SetValue(instance, value);
		}
	}

	public static T CallPrivateMethod<T>(this object instance, string methodname, params object[] param)
	{
		return (T)AccessTools.Method(instance.GetType(), methodname).Invoke(instance, param);
	}

	public static void CallPrivateMethod(this object instance, string methodname, params object[] param)
	{
		MethodInfo methodInfo = AccessTools.Method(instance.GetType(), methodname);
		if (!(methodInfo == null))
		{
			methodInfo.Invoke(instance, param);
		}
	}

	public static void SetPrivateProperty(this object instance, string propertyname, object value)
	{
		PropertyInfo propertyInfo = AccessTools.Property(instance.GetType(), propertyname);
		if (!(propertyInfo == null))
		{
			propertyInfo.SetValue(instance, value);
		}
	}

	public static T GetPrivateProperty<T>(this object instance, string propertyname)
	{
		PropertyInfo propertyInfo = AccessTools.Property(instance.GetType(), propertyname);
		return (T)((propertyInfo != null) ? propertyInfo.GetValue(instance) : null);
	}

	public static T ShallowCopy<T>(this T source) where T : class, new()
	{
		if (source == null)
		{
			return null;
		}
		Type type = source.GetType();
		T val = new T();
		FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo obj in fields)
		{
			object value = obj.GetValue(source);
			obj.SetValue(val, value);
		}
		PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (propertyInfo.CanRead && propertyInfo.CanWrite)
			{
				object value2 = propertyInfo.GetValue(source);
				propertyInfo.SetValue(val, value2);
			}
		}
		return val;
	}
}
