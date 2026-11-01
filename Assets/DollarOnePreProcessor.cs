using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DollarOnePreprocessor
{
	public static Vector2[] Normalize(
		IReadOnlyList<Vector2> input,
		int targetCount = 64)
	{
		if (input == null || input.Count < 2)
			throw new ArgumentException(
				"A gesture requires at least two points.");

		Vector2[] points = Resample(input, targetCount);

		// RotateToZero(points);
		ScaleToSquare(points, 1f);
		TranslateToOrigin(points);

		return points;
	}

	private static Vector2[] Resample(
		IReadOnlyList<Vector2> input,
		int targetCount)
	{
		float pathLength = PathLength(input);

		if (pathLength <= Mathf.Epsilon)
			return Enumerable.Repeat(input[0], targetCount).ToArray();

		float interval =
			pathLength / (targetCount - 1);

		var result = new List<Vector2>
		{
			input[0]
		};

		float accumulated = 0f;

		for (int i = 1; i < input.Count; i++)
		{
			Vector2 previous = input[i - 1];
			Vector2 current = input[i];

			float segmentLength =
				Vector2.Distance(previous, current);

			if (segmentLength <= Mathf.Epsilon)
				continue;

			while (
				accumulated + segmentLength >= interval &&
				result.Count < targetCount)
			{
				float t =
					(interval - accumulated) /
					segmentLength;

				Vector2 point =
					Vector2.Lerp(previous, current, t);

				result.Add(point);

				previous = point;

				segmentLength =
					Vector2.Distance(previous, current);

				accumulated = 0f;

				if (segmentLength <= Mathf.Epsilon)
					break;
			}

			accumulated += segmentLength;
		}

		// Ensure we always have exactly targetCount points.
		while (result.Count < targetCount)
			result.Add(input[^1]);

		if (result.Count > targetCount)
			result.RemoveRange(
				targetCount,
				result.Count - targetCount);

		return result.ToArray();
	}

	private static float PathLength(
		IReadOnlyList<Vector2> points)
	{
		float length = 0f;

		for (int i = 1; i < points.Count; i++)
		{
			length +=
				Vector2.Distance(
					points[i - 1],
					points[i]);
		}

		return length;
	}

	private static void RotateToZero(
		Vector2[] points)
	{
		Vector2 centroid = Centroid(points);

		Vector2 firstDirection =
			points[0] - centroid;

		float angle =
			Mathf.Atan2(
				firstDirection.y,
				firstDirection.x);

		float rotation =
			-angle;

		float cos = Mathf.Cos(rotation);
		float sin = Mathf.Sin(rotation);

		for (int i = 0; i < points.Length; i++)
		{
			Vector2 p = points[i] - centroid;

			points[i] = new Vector2(
				p.x * cos - p.y * sin,
				p.x * sin + p.y * cos);
		}
	}

	private static void ScaleToSquare(
		Vector2[] points,
		float size)
	{
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

		float scale =
			Mathf.Max(width, height);

		if (scale <= Mathf.Epsilon)
			return;

		for (int i = 0; i < points.Length; i++)
		{
			points[i] =
				new Vector2(
					(points[i].x - minX) / scale * size,
					(points[i].y - minY) / scale * size);
		}
	}

	private static void TranslateToOrigin(
		Vector2[] points)
	{
		Vector2 centroid =
			Centroid(points);

		for (int i = 0; i < points.Length; i++)
			points[i] -= centroid;
	}

	private static Vector2 Centroid(
		IReadOnlyList<Vector2> points)
	{
		Vector2 centroid = Vector2.zero;

		foreach (Vector2 point in points)
			centroid += point;

		return centroid / points.Count;
	}
}