using System;

namespace JPEG;

public static class DCT
{
    private const int N = 8;

    private static readonly double[,] _cosTable = new double[N, N];

    private static readonly double[] _alpha =
    {
        1 / Math.Sqrt(2), 1, 1, 1, 1, 1, 1, 1
    };

    static DCT()
    {
        FillCosTable();
    }

    private static void FillCosTable()
    {
        for (int x = 0; x < N; x++)
        {
            for (int u = 0; u < N; u++)
            {
                _cosTable[x, u] =
                    Math.Cos((2.0 * x + 1.0) * u * Math.PI / (2 * N));
            }
        }
    }

    public static void DCT2D(double[] input, double[] output, double[] temp)
    {
        double beta = Beta();

        for (int u = 0; u < N; u++)
        {
            for (int y = 0; y < N; y++)
            {
                double sum = 0;

                for (int x = 0; x < N; x++)
                {
                    sum += input[x * N + y] * _cosTable[x, u];
                }

                temp[u * N + y] = sum;
            }
        }

        for (int u = 0; u < N; u++)
        {
            for (int v = 0; v < N; v++)
            {
                double sum = 0;

                for (int y = 0; y < N; y++)
                {
                    sum += temp[u * N + y] * _cosTable[y, v];
                }

                output[u * N + v] = sum * beta;
            }
        }
    }

    public static void IDCT2D(double[] coeffs, double[] output, double[] temp)
    {
        double beta = Beta();
        
        for (int x = 0; x < N; x++)
        {
            for (int v = 0; v < N; v++)
            {
                double sum = 0;

                for (int u = 0; u < N; u++)
                {
                    sum += coeffs[u * N + v] *
                           _cosTable[x, u] *
                           _alpha[u];
                }

                temp[x * N + v] = sum;
            }
        }

        for (int x = 0; x < N; x++)
        {
            for (int y = 0; y < N; y++)
            {
                double sum = 0;

                for (int v = 0; v < N; v++)
                {
                    sum += temp[x * N + v] *
                           _cosTable[y, v] *
                           _alpha[v];
                }

                output[x * N + y] = sum * beta;
            }
        }
    }

    private static double Beta()
    {
        return 1.0 / 8 + 1.0 / 8;
    }
}
