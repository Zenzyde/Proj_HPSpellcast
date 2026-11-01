using System;
using System.Collections.Generic;
using UnityEngine;

// Variant implementation of the $1 Unistroke Recognizer
// https://depts.washington.edu/acelab/proj/dollar/index.html
public static class DollarOneMatcher
{
	public static float Match(
		IReadOnlyList<Vector2> candidate,
		IReadOnlyList<Vector2> template)
	{
		if (candidate == null)
			throw new ArgumentNullException(nameof(candidate));

		if (template == null)
			throw new ArgumentNullException(nameof(template));

		if (candidate.Count != template.Count)
			throw new ArgumentException(
				"Candidate and template must contain the same number of points.");

		float distance =
			PathDistance(
				candidate,
				template);

		// Convert average point distance into
		// a similarity score where 1 = perfect.
		float halfDiagonal =
			0.5f * Mathf.Sqrt(2f);

		float score =
			1f - (distance / halfDiagonal);

		return Mathf.Clamp01(score);
	}

	private static float PathDistance(
		IReadOnlyList<Vector2> points1,
		IReadOnlyList<Vector2> points2)
	{
		float distance = 0f;

		for (int i = 0; i < points1.Count; i++)
		{
			distance +=
				Vector2.Distance(
					points1[i],
					points2[i]);
		}

		return distance / points1.Count;
	}
}