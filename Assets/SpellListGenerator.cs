using System.Collections.Generic;
using UnityEngine;
using static PatternDetector;

public class SpellListGenerator : MonoBehaviour
{
	[Header("UI")]
	[SerializeField]
	private Transform content;

	[SerializeField]
	private SpellListItem itemPrefab;

	[Header("Preview")]
	[SerializeField]
	private int textureSize = 256;

	[SerializeField]
	private int lineThickness = 4;

	[SerializeField]
	private float padding = 20f;

	private readonly List<Sprite> generatedSprites = new();

	public void Generate(
		IReadOnlyList<PatternTemplate> templates)
	{
		Clear();

		foreach (PatternTemplate template in templates)
		{
			Sprite sprite =
				CreatePathSprite(template.points);

			SpellListItem item =
				Instantiate(
					itemPrefab,
					content);

			item.Set(
				template,
				sprite);

			generatedSprites.Add(sprite);
		}
	}

	private Sprite CreatePathSprite(
		IReadOnlyList<Vector2> points)
	{
		Texture2D texture =
			new Texture2D(
				textureSize,
				textureSize,
				TextureFormat.RGBA32,
				false);

		texture.filterMode =
			FilterMode.Bilinear;

		Color[] pixels =
			new Color[
				textureSize * textureSize];

		// Transparent background.
		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = Color.clear;

		texture.SetPixels(pixels);

		DrawPath(
			texture,
			points);

		texture.Apply();

		return Sprite.Create(
			texture,
			new Rect(
				0,
				0,
				texture.width,
				texture.height),
			new Vector2(0.5f, 0.5f),
			100f);
	}

	private void DrawPath(
		Texture2D texture,
		IReadOnlyList<Vector2> points)
	{
		if (points.Count < 2)
			return;

		// Find bounds.
		float minX = float.MaxValue;
		float minY = float.MaxValue;
		float maxX = float.MinValue;
		float maxY = float.MinValue;

		foreach (Vector2 point in points)
		{
			minX = Mathf.Min(minX, point.x);
			minY = Mathf.Min(minY, point.y);
			maxX = Mathf.Max(maxX, point.x);
			maxY = Mathf.Max(maxY, point.y);
		}

		float width = maxX - minX;
		float height = maxY - minY;

		float largest =
			Mathf.Max(width, height);

		if (largest <= Mathf.Epsilon)
			return;

		float availableSize =
			textureSize - padding * 2f;

		float scale =
			availableSize / largest;

		Vector2 offset =
			new Vector2(
				(textureSize - width * scale) / 2f,
				(textureSize - height * scale) / 2f);

		for (int i = 1; i < points.Count; i++)
		{
			Vector2 a =
				TransformPoint(
					points[i - 1],
					minX,
					minY,
					scale,
					offset);

			Vector2 b =
				TransformPoint(
					points[i],
					minX,
					minY,
					scale,
					offset);

			DrawLine(
				texture,
				a,
				b);
		}
	}

	private Vector2 TransformPoint(
		Vector2 point,
		float minX,
		float minY,
		float scale,
		Vector2 offset)
	{
		return new Vector2(
			(point.x - minX) * scale + offset.x,
			(point.y - minY) * scale + offset.y);
	}

	private void DrawLine(
		Texture2D texture,
		Vector2 start,
		Vector2 end)
	{
		int x0 = Mathf.RoundToInt(start.x);
		int y0 = Mathf.RoundToInt(start.y);

		int x1 = Mathf.RoundToInt(end.x);
		int y1 = Mathf.RoundToInt(end.y);

		int dx = Mathf.Abs(x1 - x0);
		int dy = Mathf.Abs(y1 - y0);

		int sx = x0 < x1 ? 1 : -1;
		int sy = y0 < y1 ? 1 : -1;

		int error = dx - dy;

		while (true)
		{
			DrawCircle(
				texture,
				x0,
				y0,
				lineThickness);

			if (x0 == x1 && y0 == y1)
				break;

			int error2 = 2 * error;

			if (error2 > -dy)
			{
				error -= dy;
				x0 += sx;
			}

			if (error2 < dx)
			{
				error += dx;
				y0 += sy;
			}
		}
	}

	private void DrawCircle(
		Texture2D texture,
		int centerX,
		int centerY,
		int radius)
	{
		int radiusSquared =
			radius * radius;

		for (int x = -radius; x <= radius; x++)
		{
			for (int y = -radius; y <= radius; y++)
			{
				if (x * x + y * y > radiusSquared)
					continue;

				int px = centerX + x;
				int py = centerY + y;

				if (px < 0 ||
					px >= texture.width ||
					py < 0 ||
					py >= texture.height)
				{
					continue;
				}

				texture.SetPixel(
					px,
					py,
					Color.white);
			}
		}
	}

	private void Clear()
	{
		for (int i = content.childCount - 1; i >= 0; i--)
		{
			Destroy(content.GetChild(i).gameObject);
		}

		foreach (Sprite sprite in generatedSprites)
		{
			if (sprite == null)
				continue;

			Texture2D texture =
				sprite.texture;

			Destroy(sprite);

			if (texture != null)
				Destroy(texture);
		}

		generatedSprites.Clear();
	}
}