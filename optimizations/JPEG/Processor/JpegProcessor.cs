using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using JPEG.Images;

namespace JPEG.Processor;

public class JpegProcessor : IJpegProcessor
{
    public static readonly JpegProcessor Init = new();
    public const int CompressionQuality = 70;
    private const int DCTSize = 8;

    public void Compress(string imagePath, string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        var imageMatrix = (Matrix)bmp;

        var compressionResult = Compress(imageMatrix, CompressionQuality);
        compressionResult.Save(compressedImagePath);
    }

    public void Uncompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);

        var uncompressedImage = Uncompress(compressedImage);
        var resultBmp = (Bitmap)uncompressedImage;
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private static CompressedImage Compress(Matrix matrix, int quality = 50)
{
    int blocksX = matrix.Width / DCTSize;
    int blocksY = matrix.Height / DCTSize;

    byte[][] rowResults = new byte[blocksY][];

    var quantizationMatrix = GetQuantizationMatrix(quality);

    Parallel.For(0, blocksY, y =>
    {
        var temp = new double[64];

        var channelFreqsY = new double[64];
        var channelFreqsCb = new double[64];
        var channelFreqsCr = new double[64];

        var subMatrixY = new double[64];
        var subMatrixCb = new double[64];
        var subMatrixCr = new double[64];

        byte[] localBytes = new byte[blocksX * 192];
        int writeIndex = 0;

        for (var x = 0; x < matrix.Width; x += DCTSize)
        {
            var currY = y * DCTSize;

            GetSubMatrixY(matrix, currY, x, subMatrixY);
            GetSubMatrixCb(matrix, currY, x, subMatrixCb);
            GetSubMatrixCr(matrix, currY, x, subMatrixCr);

            var cbSmall = Downsample(subMatrixCb);
            var crSmall = Downsample(subMatrixCr);

            subMatrixCb = Upsample(cbSmall);
            subMatrixCr = Upsample(crSmall);

            ShiftMatrixValues(subMatrixY, -128);

            DCT.DCT2D(subMatrixY, channelFreqsY, temp);

            var quantizedFreqsY = Quantize(channelFreqsY, quantizationMatrix);
            var quantizedBytesY = ZigZagScan(quantizedFreqsY);

            Array.Copy(quantizedBytesY, 0, localBytes, writeIndex, 64);
            writeIndex += 64;

            ShiftMatrixValues(subMatrixCb, -128);

            DCT.DCT2D(subMatrixCb, channelFreqsCb, temp);

            var quantizedFreqsCb = Quantize(channelFreqsCb, quantizationMatrix);
            var quantizedBytesCb = ZigZagScan(quantizedFreqsCb);

            Array.Copy(quantizedBytesCb, 0, localBytes, writeIndex, 64);
            writeIndex += 64;

            ShiftMatrixValues(subMatrixCr, -128);

            DCT.DCT2D(subMatrixCr, channelFreqsCr, temp);

            var quantizedFreqsCr = Quantize(channelFreqsCr, quantizationMatrix);
            var quantizedBytesCr = ZigZagScan(quantizedFreqsCr);

            Array.Copy(quantizedBytesCr, 0, localBytes, writeIndex, 64);
            writeIndex += 64;
        }

        rowResults[y] = localBytes;
    });

    int totalSize = blocksX * blocksY * 192;

    byte[] allQuantizedBytes = new byte[totalSize];

    int offset = 0;

    foreach (var row in rowResults)
    {
        Buffer.BlockCopy(row, 0, allQuantizedBytes, offset, row.Length);
        offset += row.Length;
    }

    long bitsCount;
    Dictionary<BitsWithLength, byte> decodeTable;

    var compressedBytes =
        HuffmanCodec.Encode(allQuantizedBytes, out decodeTable, out bitsCount);

    return new CompressedImage
    {
        Quality = quality,
        CompressedBytes = compressedBytes,
        BitsCount = bitsCount,
        DecodeTable = decodeTable,
        Height = matrix.Height,
        Width = matrix.Width
    };
}

    private static Matrix Uncompress(CompressedImage image)
    {
        var result = new Matrix(image.Height, image.Width);

        var quantizationMatrix = GetQuantizationMatrix(image.Quality);

        var decodedBytes =
            HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount);

        int offset = 0;
        
        var yChannel = new double[64];
        var cbChannel = new double[64];
        var crChannel = new double[64];

        for (var y = 0; y < image.Height; y += DCTSize)
        {
            for (var x = 0; x < image.Width; x += DCTSize)
            {

                DecodeBlock(decodedBytes, ref offset, quantizationMatrix, yChannel);
                DecodeBlock(decodedBytes, ref offset, quantizationMatrix, cbChannel);
                DecodeBlock(decodedBytes, ref offset, quantizationMatrix, crChannel);

                SetPixels(result, yChannel, cbChannel, crChannel, y, x);
            }
        }

        return result;
    }

    private static void DecodeBlock(
        byte[] decodedBytes,
        ref int offset,
        int[] quantizationMatrix,
        double[] output)
    {
        var quantized = new byte[64];
        var temp = new double[64];

        Buffer.BlockCopy(decodedBytes, offset, quantized, 0, 64);

        offset += 64;

        var freqs = ZigZagUnScan(quantized);

        var dequantized = DeQuantize(freqs, quantizationMatrix);

        DCT.IDCT2D(dequantized, output, temp);

        ShiftMatrixValues(output, 128);
    }

    private static double[] Upsample(double[] input)
    {
        var result = new double[64];

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                double v = input[y * 4 + x];

                result[(y * 2) * 8 + (x * 2)] = v;
                result[(y * 2 + 1) * 8 + (x * 2)] = v;
                result[(y * 2) * 8 + (x * 2 + 1)] = v;
                result[(y * 2 + 1) * 8 + (x * 2 + 1)] = v;
            }
        }

        return result;
    }

    private static double[] Downsample(double[] input)
    {
        var result = new double[16];

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                int yy = y * 2;
                int xx = x * 2;

                result[y * 4 + x] =
                    (input[yy * 8 + xx] +
                     input[(yy + 1) * 8 + xx] +
                     input[yy * 8 + (xx + 1)] +
                     input[(yy + 1) * 8 + (xx + 1)]) * 0.25;
            }
        }

        return result;
    }

    private static void ShiftMatrixValues(double[] subMatrix, int shiftValue)
    {
        for (int i = 0; i < 64; i++)
            subMatrix[i] += shiftValue;
    }

    private static void SetPixels(Matrix matrix, double[] yChannel, double[] cbChannel, double[] crChannel,
        int yOffset, int xOffset)
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 8 + x;

                matrix.Y[yOffset + y, xOffset + x] = yChannel[idx];
                matrix.Cb[yOffset + y, xOffset + x] = cbChannel[idx];
                matrix.Cr[yOffset + y, xOffset + x] = crChannel[idx];
            }
        }
    }

    private static void GetSubMatrixY(Matrix matrix, int yOffset, int xOffset, double[] subMatrix)
    {
        for (var j = 0; j < 8; j++)
        for (var i = 0; i < 8; i++)
            subMatrix[j * 8 + i] = matrix.Y[yOffset + j, xOffset + i];
    }

    private static void GetSubMatrixCb(Matrix matrix, int yOffset, int xOffset, double[] subMatrix)
    {
        for (var j = 0; j < 8; j++)
        for (var i = 0; i < 8; i++)
            subMatrix[j * 8 + i] = matrix.Cb[yOffset + j, xOffset + i];
    }

    private static void GetSubMatrixCr(Matrix matrix, int yOffset, int xOffset, double[] subMatrix)
    {
        for (var j = 0; j < 8; j++)
        for (var i = 0; i < 8; i++)
            subMatrix[j * 8 + i] = matrix.Cr[yOffset + j, xOffset + i];
    }

    private static byte[] ZigZagScan(byte[] channelFreqs)
    {
        int[] zigzag =
        {
            0,1,8,16,9,2,3,10,
            17,24,32,25,18,11,4,5,
            12,19,26,33,40,48,41,34,
            27,20,13,6,7,14,21,28,
            35,42,49,56,57,50,43,36,
            29,22,15,23,30,37,44,51,
            58,59,52,45,38,31,39,46,
            53,60,61,54,47,55,62,63
        };

        var result = new byte[64];

        for (int i = 0; i < 64; i++)
            result[i] = channelFreqs[zigzag[i]];

        return result;
    }

    private static byte[] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
    {
        int[] zigzag =
        {
            0,1,8,16,9,2,3,10,
            17,24,32,25,18,11,4,5,
            12,19,26,33,40,48,41,34,
            27,20,13,6,7,14,21,28,
            35,42,49,56,57,50,43,36,
            29,22,15,23,30,37,44,51,
            58,59,52,45,38,31,39,46,
            53,60,61,54,47,55,62,63
        };

        var result = new byte[64];

        for (int i = 0; i < 64; i++)
            result[zigzag[i]] = quantizedBytes[i];

        return result;
    }

    private static byte[] Quantize(double[] channelFreqs, int[] quantizationMatrix)
    {
        var result = new byte[64];

        for (int i = 0; i < 64; i++)
            result[i] = (byte)(channelFreqs[i] / quantizationMatrix[i]);

        return result;
    }

    private static double[] DeQuantize(byte[] quantizedBytes, int[] quantizationMatrix)
    {
        var result = new double[64];

        for (int i = 0; i < 64; i++)
            result[i] = ((sbyte)quantizedBytes[i]) * quantizationMatrix[i];

        return result;
    }

    private static int[] GetQuantizationMatrix(int quality)
    {
        if (quality < 1 || quality > 99)
            throw new ArgumentException("quality must be in [1,99] interval");

        var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

        int[] result =
        {
            16,11,10,16,24,40,51,61,
            12,12,14,19,26,58,60,55,
            14,13,16,24,40,57,69,56,
            14,17,22,29,51,87,80,62,
            18,22,37,56,68,109,103,77,
            24,35,55,64,81,104,113,92,
            49,64,78,87,103,121,120,101,
            72,92,95,98,112,100,103,99
        };

        for (int i = 0; i < 64; i++)
            result[i] = (multiplier * result[i] + 50) / 100;

        return result;
    }
}
