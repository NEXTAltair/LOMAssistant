using System;
using Mortal.Story;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LOMAssistant.Features.Dice;

public class DiceManualSelectionButton : MonoBehaviour, IPointerClickHandler, IEventSystemHandler, IPointerEnterHandler, IPointerExitHandler
{
	public int index;

	public DiceOption DiceOption => base.gameObject.GetComponent<DiceOption>();

	public event Action<PointerEventData, int> PointerClickEvent;

	public void OnPointerClick(PointerEventData eventData)
	{
		this.PointerClickEvent?.Invoke(eventData, index);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		DiceOption diceOption = DiceOption;
		if (diceOption != null)
		{
			diceOption.Select();
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		DiceOption diceOption = DiceOption;
		if (diceOption != null)
		{
			diceOption.UnSelect();
		}
	}
}
