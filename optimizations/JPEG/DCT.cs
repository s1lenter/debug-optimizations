using System;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	private static readonly double[,] _cosTable = new double[8, 8];
	private const int DCT_SIZE = 8;
	
	private static readonly double[] _alpha =
	[
		1 / Math.Sqrt(2), 1, 1, 1, 1, 1, 1, 1
	];

	static DCT()
	{
		FillCosTable();
	}

	private static void FillCosTable()
	{
		for (int x = 0; x < 8; x++)
		{
			for (int u = 0; u < 8; u++)
			{
				_cosTable[x, u] = Math.Cos((2d * x + 1d) * u * Math.PI / (2 * DCT_SIZE));
			}
		}
	}
	
	public static void DCT2D(double[,] input, double[,] output, double[,] temp)
	{
		int N = 8;
		var beta = Beta(N, N);

		for (int u = 0; u < N; u++)
		{
			for (int y = 0; y < N; y++)
			{
				double sum = 0;

				for (int x = 0; x < N; x++)
					sum += input[x, y] * _cosTable[x, u];

				temp[u, y] = sum;
			}
		}

		for (int u = 0; u < N; u++)
		{
			for (int v = 0; v < N; v++)
			{
				double sum = 0;

				for (int y = 0; y < N; y++)
					sum += temp[u, y] * _cosTable[y, v];

				output[u, v] = sum * beta;
			}
		}
	}

	public static void IDCT2D(double[,] coeffs, double[,] output)
	{
		int N = 8;

		var temp = new double[N, N];
		var beta = Beta(N, N);

		for (int x = 0; x < N; x++)
		{
			for (int v = 0; v < N; v++)
			{
				double sum = 0;

				for (int u = 0; u < N; u++)
					sum += coeffs[u, v] * _cosTable[x, u] * _alpha[u];

				temp[x, v] = sum;
			}
		}

		for (int x = 0; x < N; x++)
		{
			for (int y = 0; y < N; y++)
			{
				double sum = 0;

				for (int v = 0; v < N; v++)
					sum += temp[x, v] * _cosTable[y, v] * _alpha[v];

				output[x, y] = sum * beta;
			}
		}
	}

	private static double Beta(int height, int width)
	{
		return 1d / width + 1d / height;
	}
}