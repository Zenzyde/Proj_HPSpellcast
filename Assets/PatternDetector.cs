using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class PatternDetector
{
	private readonly List<PatternTemplate> patternTemplates = new List<PatternTemplate>();
	private const int PointCount = 32;

	private PatternDatabase patterns;
	private List<MouseData> recordedPositions = new List<MouseData>();
	public struct MouseData
	{
		public double x;
		public double y;
	}

	public IReadOnlyList<PatternTemplate> templates => patternTemplates;

	public PatternMatch FindPattern()
	{
		if (patternTemplates.Count == 0)
			return new PatternMatch { Name = "None" };
		var data = recordedPositions.Select(md => new Vector2((float)md.x, (float)md.y)).ToArray();
		Vector2[] normalized = DollarOnePreprocessor.Normalize(data, PointCount);

		PatternTemplate? bestTemplate = null;
		float bestScore = 0f;

		foreach (var template in patternTemplates)
		{
			float score =
				DollarOneMatcher.Match(
					normalized,
					template.points);

			Debug.Log(
				$"{template.name}: {score:F3}");

			if (score > bestScore)
			{
				bestScore = score;
				bestTemplate = template;
			}
		}

		if (bestTemplate == null || bestScore < 0.75f)
			return new PatternMatch { Name = "None" };
		return new PatternMatch
		{
			Name = bestTemplate?.name ?? "None",
			Score = bestScore
		};
	}

	public IEnumerator LoadPatterns(string filePath, System.Action onComplete = null)
	{
		string jsonPath = Path.Combine(Application.streamingAssetsPath, filePath).Replace("\\", "/");

		using (UnityWebRequest request = UnityWebRequest.Get(jsonPath))
		{
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Failed to load patterns from '{jsonPath}': {request.error}");
				yield break;
			}

			patterns = JsonUtility.FromJson<PatternDatabase>(request.downloadHandler.text);
		}

		string imageDirectory = jsonPath[..(jsonPath.LastIndexOf('/') + 1)].Replace("\\", "/");

		var imageCache = new Dictionary<string, byte[]>();

		foreach (var spell in patterns.Spells)
		{
			if (string.IsNullOrEmpty(spell.Image) || imageCache.ContainsKey(spell.Image))
			{
				continue;
			}

			string imagePath = imageDirectory + spell.Image;

			using UnityWebRequest request = UnityWebRequest.Get(imagePath);

			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Failed to load spell image '{imagePath}': {request.error}");
				yield break;
			}

			imageCache[spell.Image] = request.downloadHandler.data;
		}

		patternTemplates.Clear();

		patternTemplates.AddRange(PatternTemplateCompiler.Compile(patterns, imageName => imageCache[imageName]));

		Debug.Log($"Loaded and compiled {patternTemplates.Count} patterns.");
		onComplete?.Invoke();
	}

	public void RecordPosition(Vector2 position)
	{
		recordedPositions.Add(new MouseData { x = position.x, y = position.y });
	}

	public void ClearRecordedPositions()
	{
		recordedPositions.Clear();
	}

	[System.Serializable]
	public sealed class PatternDatabase
	{
		public List<PatternDefinition> Spells;
	}

	[System.Serializable]
	public sealed class PatternDefinition
	{
		public string Name;
		public string Image;
	}

	public class PatternMatch
	{
		public string Name;
		public float Score;
	}

	public class PatternTemplate
	{
		public string name { get; }
		public Vector2[] points { get; }

		public PatternTemplate(string name, IEnumerable<Vector2> points)
		{
			this.name = name;
			this.points = points.ToArray();
		}
	}
}