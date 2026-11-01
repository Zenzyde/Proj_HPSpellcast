using UnityEngine;
using System.Collections.Generic;
using System;

public static class GestureImageParser
{
	private static readonly Vector2Int[] Neighbours8 =
	{
		new(-1, -1),
		new( 0, -1),
		new( 1, -1),
		new(-1,  0),
		new( 1,  0),
		new(-1,  1),
		new( 0,  1),
		new( 1, 1)
	};

	// ---------------------------------------------------------
	// Zhang-Suen thinning
	// ---------------------------------------------------------

	private static bool[,] Skeletonize(
		bool[,] source)
	{
		int width = source.GetLength(0);
		int height = source.GetLength(1);

		bool[,] image =
			(bool[,])source.Clone();

		bool changed;

		do
		{
			changed = false;

			var remove =
				new List<Vector2Int>();

			// Step 1
			for (int y = 1; y < height - 1; y++)
			{
				for (int x = 1; x < width - 1; x++)
				{
					if (!image[x, y])
						continue;

					if (ShouldRemove(
						image,
						x,
						y,
						true))
					{
						remove.Add(
							new Vector2Int(x, y));
					}
				}
			}

			foreach (Vector2Int point in remove)
			{
				image[
					point.x,
					point.y] = false;

				changed = true;
			}

			remove.Clear();

			// Step 2
			for (int y = 1; y < height - 1; y++)
			{
				for (int x = 1; x < width - 1; x++)
				{
					if (!image[x, y])
						continue;

					if (ShouldRemove(
						image,
						x,
						y,
						false))
					{
						remove.Add(
							new Vector2Int(x, y));
					}
				}
			}

			foreach (Vector2Int point in remove)
			{
				image[
					point.x,
					point.y] = false;

				changed = true;
			}

		} while (changed);

		return image;
	}

	private static bool ShouldRemove(
		bool[,] image,
		int x,
		int y,
		bool firstStep)
	{
		bool p2 = image[x, y + 1];
		bool p3 = image[x + 1, y + 1];
		bool p4 = image[x + 1, y];
		bool p5 = image[x + 1, y - 1];
		bool p6 = image[x, y - 1];
		bool p7 = image[x - 1, y - 1];
		bool p8 = image[x - 1, y];
		bool p9 = image[x - 1, y + 1];

		int neighbours =
			Count(
				p2, p3, p4, p5,
				p6, p7, p8, p9);

		if (neighbours < 2 ||
			neighbours > 6)
		{
			return false;
		}

		int transitions =
			CountTransitions(
				p2, p3, p4, p5,
				p6, p7, p8, p9);

		if (transitions != 1)
			return false;

		if (firstStep)
		{
			if (p2 && p4 && p6)
				return false;

			if (p4 && p6 && p8)
				return false;
		}
		else
		{
			if (p2 && p4 && p8)
				return false;

			if (p2 && p6 && p8)
				return false;
		}

		return true;
	}

	private static int Count(
		params bool[] values)
	{
		int count = 0;

		foreach (bool value in values)
		{
			if (value)
				count++;
		}

		return count;
	}

	private static int CountTransitions(
		params bool[] values)
	{
		int transitions = 0;

		for (int i = 0; i < values.Length; i++)
		{
			bool current =
				values[i];

			bool next =
				values[
					(i + 1) %
					values.Length];

			if (!current && next)
				transitions++;
		}

		return transitions;
	}

	// Controls how aggressively RGB values are grouped together.
	// Higher = fewer color groups.
	private const int ColorQuantization = 16;

	// How close a pixel must be to the selected core color.
	// RGB distance is normalized to 0..1.
	private const float ColorTolerance = 0.35f;

	// Ignore extremely small color groups.
	private const int MinimumColorCount = 5;

	// Remove disconnected noise after creating the mask.
	private const int MinimumComponentSize = 10;

	public static Vector2[] ExtractPointsV4(
		Texture2D texture,
		float alphaThreshold = 0.01f)
	{
		if (texture == null)
			throw new ArgumentNullException(nameof(texture));

		Color coreColor =
			FindCoreColorV2(
				texture,
				alphaThreshold);

		Debug.Log(
			$"Selected gesture core color: " +
			$"{coreColor.r:F2}, " +
			$"{coreColor.g:F2}, " +
			$"{coreColor.b:F2}");

		bool[,] mask =
			CreateColorMaskV2(
				texture,
				coreColor,
				alphaThreshold,
				ColorTolerance);

		RemoveSmallComponentsV2(
			mask,
			MinimumComponentSize);

		bool[,] skeleton =
			Skeletonize(mask);

		RemoveSmallComponentsV2(
			skeleton,
			MinimumComponentSize);

		Vector2[] path =
			TraceSkeletonV2(skeleton);

		if (path.Length < 2)
		{
			throw new InvalidOperationException(
				"Could not extract a gesture path from the image.");
		}

		return path;
	}

	// ------------------------------------------------------------
	// COLOR DETECTION
	// ------------------------------------------------------------

	private static Color FindCoreColorV2(
	Texture2D texture,
	float alphaThreshold)
	{
		var clusters =
			new Dictionary<ColorKey, ColorCluster>();

		Color[] pixels =
			texture.GetPixels();

		for (int y = 0; y < texture.height; y++)
		{
			for (int x = 0; x < texture.width; x++)
			{
				Color pixel =
					pixels[y * texture.width + x];

				if (pixel.a <= alphaThreshold)
					continue;

				ColorKey key =
					QuantizeColor(pixel);

				if (!clusters.TryGetValue(
						key,
						out ColorCluster cluster))
				{
					cluster =
						new ColorCluster(key);

					clusters.Add(
						key,
						cluster);
				}

				cluster.Add(pixel);
			}
		}

		if (clusters.Count == 0)
		{
			throw new InvalidOperationException(
				"Image contains no visible pixels.");
		}

		ColorCluster? bestCluster = null;
		float bestScore = float.MinValue;

		foreach (ColorCluster cluster in clusters.Values)
		{
			if (cluster.Count < MinimumColorCount)
				continue;

			float frequencyScore =
				Mathf.Sqrt(cluster.Count);

			float alphaScore =
				cluster.AverageAlpha;

			/*
			 * Alpha is deliberately given a strong influence.
			 *
			 * A color that is mostly transparent should have
			 * a much harder time becoming the selected core.
			 */
			float opacityScore =
				alphaScore * alphaScore;

			float score =
				frequencyScore *
				opacityScore;

			if (score > bestScore)
			{
				bestScore = score;
				bestCluster = cluster;
			}
		}

		if (bestCluster == null)
		{
			throw new InvalidOperationException(
				"Could not find a suitable gesture color.");
		}

		Color result =
			bestCluster.AverageColor;

		Debug.Log(
			$"Selected core color: " +
			$"RGB({result.r:F2}, {result.g:F2}, {result.b:F2}), " +
			$"pixels={bestCluster.Count}, " +
			$"alpha={bestCluster.AverageAlpha:F2}, " +
			$"score={bestScore:F2}");

		return result;
	}

	private sealed class ColorCluster
	{
		private float totalR;
		private float totalG;
		private float totalB;
		private float totalAlpha;

		public ColorKey Key { get; }

		public int Count { get; private set; }

		public float AverageAlpha =>
			Count == 0
				? 0f
				: totalAlpha / Count;

		public Color AverageColor =>
			Count == 0
				? Color.clear
				: new Color(
					totalR / Count,
					totalG / Count,
					totalB / Count,
					AverageAlpha);

		public ColorCluster(ColorKey key)
		{
			Key = key;
		}

		public void Add(Color color)
		{
			totalR += color.r;
			totalG += color.g;
			totalB += color.b;
			totalAlpha += color.a;

			Count++;
		}
	}

	private static ColorKey QuantizeColor(
		Color color)
	{
		return new ColorKey(
			Mathf.RoundToInt(
				color.r * ColorQuantization),

			Mathf.RoundToInt(
				color.g * ColorQuantization),

			Mathf.RoundToInt(
				color.b * ColorQuantization));
	}

	private readonly struct ColorKey :
		IEquatable<ColorKey>
	{
		private readonly int r;
		private readonly int g;
		private readonly int b;

		public ColorKey(
			int r,
			int g,
			int b)
		{
			this.r = r;
			this.g = g;
			this.b = b;
		}

		public Color ToColor()
		{
			return new Color(
				r / (float)ColorQuantization,
				g / (float)ColorQuantization,
				b / (float)ColorQuantization,
				1f);
		}

		public bool Equals(ColorKey other)
		{
			return r == other.r &&
				   g == other.g &&
				   b == other.b;
		}

		public override bool Equals(
			object obj)
		{
			return obj is ColorKey other &&
				   Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				r,
				g,
				b);
		}
	}

	private static bool[,] CreateColorMaskV2(
	Texture2D texture,
	Color coreColor,
	float alphaThreshold,
	float colorTolerance)
	{
		bool[,] mask =
			new bool[
				texture.width,
				texture.height];

		Color[] pixels =
			texture.GetPixels();

		float maxColorDistance =
			colorTolerance * colorTolerance;

		for (int y = 0; y < texture.height; y++)
		{
			for (int x = 0; x < texture.width; x++)
			{
				Color pixel =
					pixels[y * texture.width + x];

				if (pixel.a <= alphaThreshold)
					continue;

				float dr =
					pixel.r - coreColor.r;

				float dg =
					pixel.g - coreColor.g;

				float db =
					pixel.b - coreColor.b;

				float colorDistance =
					dr * dr +
					dg * dg +
					db * db;

				if (colorDistance > maxColorDistance)
					continue;

				/*
				 * Core pixels should be relatively opaque.
				 *
				 * This prevents a large amount of faded
				 * anti-aliased glow from becoming the mask.
				 */
				float alphaFactor =
					Mathf.InverseLerp(
						alphaThreshold,
						1f,
						pixel.a);

				mask[x, y] =
					alphaFactor >= 0.5f;
			}
		}

		return mask;
	}

	// ------------------------------------------------------------
	// CONNECTED COMPONENT CLEANUP
	// ------------------------------------------------------------

	private static void RemoveSmallComponentsV2(
		bool[,] mask,
		int minimumSize)
	{
		int width =
			mask.GetLength(0);

		int height =
			mask.GetLength(1);

		bool[,] visited =
			new bool[width, height];

		var component =
			new List<Vector2Int>();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (!mask[x, y] ||
					visited[x, y])
				{
					continue;
				}

				component.Clear();

				FloodFillV2(
					mask,
					visited,
					new Vector2Int(x, y),
					component);

				if (component.Count < minimumSize)
				{
					foreach (
						Vector2Int point
						in component)
					{
						mask[
							point.x,
							point.y] = false;
					}
				}
			}
		}
	}

	private static void FloodFillV2(
		bool[,] mask,
		bool[,] visited,
		Vector2Int start,
		List<Vector2Int> component)
	{
		var queue =
			new Queue<Vector2Int>();

		queue.Enqueue(start);

		visited[
			start.x,
			start.y] = true;

		while (queue.Count > 0)
		{
			Vector2Int current =
				queue.Dequeue();

			component.Add(current);

			foreach (
				Vector2Int offset
				in Neighbours8)
			{
				int x =
					current.x + offset.x;

				int y =
					current.y + offset.y;

				if (x < 0 ||
					y < 0 ||
					x >= mask.GetLength(0) ||
					y >= mask.GetLength(1))
				{
					continue;
				}

				if (!mask[x, y] ||
					visited[x, y])
				{
					continue;
				}

				visited[x, y] = true;

				queue.Enqueue(
					new Vector2Int(x, y));
			}
		}
	}

	// ------------------------------------------------------------
	// SKELETON PATH TRACING
	// ------------------------------------------------------------

	private static Vector2[] TraceSkeletonV2(
		bool[,] skeleton)
	{
		List<Vector2Int> pixels =
			GetSkeletonPixelsV2(skeleton);

		if (pixels.Count < 2)
			return Array.Empty<Vector2>();

		Vector2Int start =
			FindBestStartV2(
				skeleton,
				pixels);

		List<Vector2Int> path =
			TraceFromV2(
				skeleton,
				start);

		if (path.Count < 2)
			return Array.Empty<Vector2>();

		return ConvertPathV2(path);
	}

	private static List<Vector2Int>
		GetSkeletonPixelsV2(
			bool[,] skeleton)
	{
		var pixels =
			new List<Vector2Int>();

		int width =
			skeleton.GetLength(0);

		int height =
			skeleton.GetLength(1);

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (skeleton[x, y])
				{
					pixels.Add(
						new Vector2Int(
							x,
							y));
				}
			}
		}

		return pixels;
	}

	private static Vector2Int FindBestStartV2(
		bool[,] skeleton,
		List<Vector2Int> pixels)
	{
		Vector2Int best =
			pixels[0];

		int bestDegree =
			int.MaxValue;

		foreach (Vector2Int pixel in pixels)
		{
			int degree =
				CountSkeletonNeighboursV2(
					skeleton,
					pixel);

			// Prefer an actual endpoint.
			if (degree == 1)
				return pixel;

			if (degree < bestDegree)
			{
				bestDegree = degree;
				best = pixel;
			}
		}

		return best;
	}

	private static List<Vector2Int> TraceFromV2(
		bool[,] skeleton,
		Vector2Int start)
	{
		var path =
			new List<Vector2Int>();

		var visited =
			new HashSet<Vector2Int>();

		Vector2Int current =
			start;

		while (true)
		{
			path.Add(current);
			visited.Add(current);

			Vector2Int? next =
				FindNextSkeletonPixelV2(
					skeleton,
					visited,
					current);

			if (!next.HasValue)
				break;

			current =
				next.Value;
		}

		return path;
	}

	private static Vector2Int?
		FindNextSkeletonPixelV2(
			bool[,] skeleton,
			HashSet<Vector2Int> visited,
			Vector2Int current)
	{
		Vector2Int? best =
			null;

		float bestDistance =
			float.MaxValue;

		foreach (
			Vector2Int offset
			in Neighbours8)
		{
			Vector2Int candidate =
				current + offset;

			if (candidate.x < 0 ||
				candidate.y < 0 ||
				candidate.x >= skeleton.GetLength(0) ||
				candidate.y >= skeleton.GetLength(1))
			{
				continue;
			}

			if (!skeleton[
					candidate.x,
					candidate.y])
			{
				continue;
			}

			if (visited.Contains(candidate))
				continue;

			float distance =
				offset.sqrMagnitude;

			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = candidate;
			}
		}

		return best;
	}

	private static int CountSkeletonNeighboursV2(
		bool[,] skeleton,
		Vector2Int point)
	{
		int count = 0;

		foreach (
			Vector2Int offset
			in Neighbours8)
		{
			int x =
				point.x + offset.x;

			int y =
				point.y + offset.y;

			if (x < 0 ||
				y < 0 ||
				x >= skeleton.GetLength(0) ||
				y >= skeleton.GetLength(1))
			{
				continue;
			}

			if (skeleton[x, y])
				count++;
		}

		return count;
	}

	private static Vector2[] ConvertPathV2(
		List<Vector2Int> path)
	{
		var result =
			new Vector2[path.Count];

		for (int i = 0;
			 i < path.Count;
			 i++)
		{
			result[i] =
				new Vector2(
					path[i].x,
					path[i].y);
		}

		return result;
	}
}