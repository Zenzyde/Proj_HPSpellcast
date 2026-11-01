using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PatternDetector;

public class SpellListItem : MonoBehaviour
{
	[SerializeField]
	private Image image;

	[SerializeField]
	private Text nameText;

	public void Set(
		PatternTemplate template,
		Sprite sprite)
	{
		nameText.text = template.name;
		image.sprite = sprite;
		image.preserveAspect = true;
	}
}