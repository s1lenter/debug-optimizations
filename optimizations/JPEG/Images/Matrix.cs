using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace JPEG.Images;

class Matrix
{
	public readonly double[,] Y;
	public readonly double[,] Cb;
	public readonly double[,] Cr;

	public readonly int Height;
	public readonly int Width;

	public Matrix(int height, int width)
	{
		Height = height;
		Width = width;

		Y = new double[height, width];
		Cb = new double[height, width];
		Cr = new double[height, width];
	}

	public static unsafe explicit operator Matrix(Bitmap bmp)
	{
		var height = bmp.Height - bmp.Height % 8;
		var width = bmp.Width - bmp.Width % 8;

		var matrix = new Matrix(height, width);

		var rect = new Rectangle(0, 0, width, height);

		var data = bmp.LockBits(
			rect,
			ImageLockMode.ReadOnly,
			System.Drawing.Imaging.PixelFormat.Format24bppRgb);

		int stride = data.Stride;

		byte* ptr = (byte*)data.Scan0;

		for (int y = 0; y < height; y++)
		{
			byte* row = ptr + y * stride;

			for (int x = 0; x < width; x++)
			{
				byte b = row[x * 3 + 0];
				byte g = row[x * 3 + 1];
				byte r = row[x * 3 + 2];

				matrix.Y[y, x]  = 0.299 * r + 0.587 * g + 0.114 * b;
				matrix.Cb[y, x] = -0.1687 * r - 0.3313 * g + 0.5 * b + 128;
				matrix.Cr[y, x] = 0.5 * r - 0.4187 * g - 0.0813 * b + 128;
			}
		}

		bmp.UnlockBits(data);

		return matrix;
	}

	public static unsafe explicit operator Bitmap(Matrix matrix)
	{
		var bmp = new Bitmap(matrix.Width, matrix.Height,
			System.Drawing.Imaging.PixelFormat.Format24bppRgb);

		var rect = new Rectangle(0, 0, matrix.Width, matrix.Height);

		var data = bmp.LockBits(rect, ImageLockMode.WriteOnly,
			System.Drawing.Imaging.PixelFormat.Format24bppRgb);

		int stride = data.Stride;
		byte* ptr = (byte*)data.Scan0;

		for (int y = 0; y < matrix.Height; y++)
		{
			byte* row = ptr + y * stride;

			for (int x = 0; x < matrix.Width; x++)
			{
				double Y = matrix.Y[y, x];
				double Cb = matrix.Cb[y, x];
				double Cr = matrix.Cr[y, x];

				double r = Y + 1.402 * (Cr - 128);
				double g = Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128);
				double b = Y + 1.772 * (Cb - 128);

				row[x * 3 + 2] = (byte)Math.Clamp(r, 0, 255);
				row[x * 3 + 1] = (byte)Math.Clamp(g, 0, 255);
				row[x * 3 + 0] = (byte)Math.Clamp(b, 0, 255);
			}
		}

		bmp.UnlockBits(data);

		return bmp;
	}

	public static int ToByte(double d)
	{
		var val = (int)d;
		if (val > byte.MaxValue)
			return byte.MaxValue;
		if (val < byte.MinValue)
			return byte.MinValue;
		return val;
	}
}