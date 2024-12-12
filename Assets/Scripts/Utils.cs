using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {

    public static float fBM(float x, float y, int oct, float persistance) {

        float total = 0.0f;
        float frequency = 1.0f;
        float amplitude = 1.0f;
        float maxValue = 0.0f;

        for (int i = 0; i < oct; ++i) {

            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistance;
            frequency *= 2.0f;
        }

        return total / maxValue;
    }

    public static float Map(float value, float originalMin, float originalMax, float targetMin, float targetMax) {

        return (value - originalMin) * (targetMax - targetMin) / (originalMax - originalMin) + targetMin;
    }

    public static System.Random r = new System.Random();
    public static void Shuffle<T>(this IList<T> list) {

        int n = list.Count;

        while (n > 1) {

            n--;
            int k = r.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
