using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static PatternDetector;

public static class PatternTemplateCompiler
{
	public static List<PatternTemplate> Compile(PatternDatabase database, Func<string, byte[]> loadImageBytes)
	{
		var templates = new List<PatternTemplate>();

		Debug.Log($"Compiling pattern templates from JSON, containing {database.Spells.Count} spells.");

		foreach (var spell in database.Spells)
		{
			if (string.IsNullOrEmpty(spell.Image))
				continue;

			byte[] data = loadImageBytes(spell.Image);

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

			if (!texture.LoadImage(data))
			{
				UnityEngine.Object.Destroy(texture);
				throw new InvalidOperationException($"Failed to load spell image '{spell.Image}'.");
			}

			Vector2[] points = GestureImageParser.ExtractPointsV4(texture);

			UnityEngine.Object.Destroy(texture);

			if (points.Length < 2)
			{
				throw new InvalidOperationException($"Spell '{spell.Name}' contains too few gesture points.");
			}

			Vector2[] normalized = DollarOnePreprocessor.Normalize(points, 32);

			templates.Add(new PatternTemplate(spell.Name, normalized));
		}

		return templates;
	}
}