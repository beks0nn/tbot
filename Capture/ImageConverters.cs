using OpenCvSharp;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Bot.Capture;

public static class ImageConverters
{
    public static Bitmap ToBitmap(ReadOnlySpan<byte> raw, int width, int height, int stride)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.WriteOnly,
            bmp.PixelFormat);

        unsafe
        {
            // Copy raw buffer into bitmap memory
            fixed (byte* srcPtr = raw)
            {
                Buffer.MemoryCopy(
                    srcPtr,
                    (void*)bmpData.Scan0,
                    bmpData.Stride * bmpData.Height,
                    raw.Length);
            }
        }

        bmp.UnlockBits(bmpData);
        return bmp;
    }

    public static Mat ToMat(ReadOnlySpan<byte> raw, int width, int height, int stride)
    {
        // BGRA (32-bit) is default format
        var mat = new Mat(height, width, MatType.CV_8UC4);

        // Validate input length
        long required = (long)height * stride;
        if (raw.Length < required)
            throw new ArgumentException("raw buffer is too small for the specified dimensions and stride", nameof(raw));

        unsafe
        {
            fixed (byte* srcPtr = raw)
            {
                // If source stride matches Mat.Step, we can copy the whole block at once
                long matStep = mat.Step();
                byte* dstBase = (byte*)mat.DataStart.ToPointer();

                if (matStep == stride)
                {
                    Buffer.MemoryCopy(srcPtr, dstBase, matStep * height, required);
                }
                else
                {
                    // Copy row by row to handle differing strides (padding)
                    int copyPerRow = Math.Min((int)matStep, stride);    
                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = srcPtr + (long)y * stride;
                        byte* dstRow = dstBase + (long)y * matStep;
                        Buffer.MemoryCopy(srcRow, dstRow, matStep, copyPerRow);
                    }
                }
            }
        }

        return mat;
    }
}