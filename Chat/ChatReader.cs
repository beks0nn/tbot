using System.Text.RegularExpressions;
using OpenCvSharp;
using Tesseract;
using Rect = OpenCvSharp.Rect;

namespace Bot.Chat;

public sealed class ChatReader : IDisposable
{
    private readonly TesseractEngine _engine;
    private byte[]? _previousHash;

    // Matches "Name: content" or "Name:" (empty message)
    private static readonly Regex MessagePattern = new(@"^(\w+)\s*:\s*(.*)$", RegexOptions.Compiled);

    // Known watermark / junk patterns to filter out
    private static readonly string[] WatermarkPatterns =
        ["Activat", "Go to Se", "Windows", "indows"];

    public ChatReader(string tessDataPath)
    {
        _engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
        _engine.SetVariable("tessedit_char_whitelist",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:!?',()-");
    }

    public List<ChatMessage> ReadChat(Mat frame, State.RectDto chatRect)
    {
        var results = new List<ChatMessage>();

        if (!chatRect.IsValid || frame.Empty())
            return results;

        var roi = chatRect.ToCvRect();
        if (roi.X + roi.Width > frame.Width || roi.Y + roi.Height > frame.Height)
            return results;

        using var chatRegion = new Mat(frame, roi);

        // Skip if chat area hasn't changed
        var hash = ComputeRegionHash(chatRegion);
        if (_previousHash != null && hash.SequenceEqual(_previousHash))
            return results;
        _previousHash = hash;

        // 1. Extract all text pixels by color into a binary mask
        using var textMask = ExtractTextMask(chatRegion);

        // 2. Upscale 3x with nearest-neighbor for pixel font
        using var upscaled = new Mat();
        Cv2.Resize(textMask, upscaled, new OpenCvSharp.Size(textMask.Width * 3, textMask.Height * 3),
            interpolation: InterpolationFlags.Nearest);

        // 3. OCR with per-line Y positions
        var ocrLines = RunOcrWithPositions(upscaled);
        if (ocrLines.Count == 0)
            return results;

        // 4. Detect colored text bands from the raw image.
        //    Each band = one visual chat row with a definite color.
        var colorBands = FindColorBands(chatRegion);

        // 5. Classify each OCR line by matching its Y position to the closest band.
        //    This is robust to OCR merging/splitting rows — Y position is ground truth.
        int chatScreenX = chatRect.X + chatRect.W / 3;
        int chatScreenY = chatRect.Y;

        foreach (var ocr in ocrLines)
        {
            if (ocr.Text.Length < 2 || IsWatermark(ocr.Text))
                continue;

            // Scale Y from 3x upscaled image back to original
            int originalY = ocr.Y / 3;

            // Find closest band by Y proximity
            var type = ChatMessageType.System;
            int bandY = originalY;
            int bestDist = int.MaxValue;
            foreach (var band in colorBands)
            {
                int dist = (originalY >= band.YStart && originalY <= band.YEnd)
                    ? 0
                    : Math.Min(Math.Abs(originalY - band.YStart), Math.Abs(originalY - band.YEnd));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    type = band.Type;
                    bandY = band.YCenter;
                }
            }

            var msg = ParseLine(ocr.Text, type, chatScreenX, chatScreenY + bandY);
            if (msg != null)
                results.Add(msg);
        }

        return results;
    }

    private record struct OcrLine(string Text, int Y);

    private List<OcrLine> RunOcrWithPositions(Mat image)
    {
        var lines = new List<OcrLine>();
        var bytes = image.ToBytes(".png");
        using var pix = Pix.LoadFromMemory(bytes);
        using var page = _engine.Process(pix, PageSegMode.Auto);

        using var iter = page.GetIterator();
        if (iter == null) return lines;

        iter.Begin();
        do
        {
            var text = iter.GetText(PageIteratorLevel.TextLine);
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bounds))
            {
                int yCenter = bounds.Y1 + bounds.Height / 2;
                lines.Add(new OcrLine(text.Trim(), yCenter));
            }
        } while (iter.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    private readonly record struct ColorBand(ChatMessageType Type, int YStart, int YEnd)
    {
        public int YCenter => (YStart + YEnd) / 2;
    }

    /// <summary>
    /// Scan the image to find contiguous bands of colored text rows.
    /// Each band = one visual chat row with a definitive color.
    /// </summary>
    private static List<ColorBand> FindColorBands(Mat bgr)
    {
        var bands = new List<ColorBand>();
        ChatMessageType? current = null;
        int bandStart = 0;
        int gapCount = 0;
        const int MaxGap = 3; // 4px padding between rows breaks bands cleanly

        for (int y = 0; y < bgr.Height; y++)
        {
            var rowType = ClassifyRow(bgr, y);

            if (rowType == null)
            {
                gapCount++;
                if (current != null && gapCount > MaxGap)
                {
                    bands.Add(new ColorBand(current.Value, bandStart, y - gapCount));
                    current = null;
                }
            }
            else if (current == null)
            {
                current = rowType.Value;
                bandStart = y;
                gapCount = 0;
            }
            else if (rowType.Value == current.Value)
            {
                gapCount = 0;
            }
            else
            {
                // Different color = new message
                bands.Add(new ColorBand(current.Value, bandStart, y - gapCount - 1));
                current = rowType.Value;
                bandStart = y;
                gapCount = 0;
            }
        }

        if (current != null)
            bands.Add(new ColorBand(current.Value, bandStart, bgr.Height - 1));

        return bands;
    }

    /// <summary>
    /// Classify a single pixel row by counting colored text pixels.
    /// Returns null if the row is background (fewer than 3 colored pixels).
    /// </summary>
    private static ChatMessageType? ClassifyRow(Mat bgr, int y)
    {
        int yellow = 0, teal = 0, system = 0;

        for (int x = 0; x < bgr.Width; x += 2)
        {
            var pixel = bgr.At<Vec3b>(y, x);
            int b = pixel.Item0, g = pixel.Item1, r = pixel.Item2;

            if (r < 80 && g < 80 && b < 80) continue;

            if (r > 160 && g > 130 && b < 100) yellow++;
            else if (r < 120 && g > 140 && b > 140) teal++;
            else if (r > 160 && g < 80 && b < 80) system++;
            else if (r > 170 && g > 170 && b > 170) system++;
        }

        if (yellow + teal + system < 3) return null;

        if (teal > yellow && teal > system) return ChatMessageType.PlayerPrivate;
        if (yellow > system) return ChatMessageType.PlayerPublic;
        return ChatMessageType.System;
    }

    /// <summary>
    /// Extract all text pixels into a binary mask using color filtering.
    /// Yellow (public), Teal (private), White (system), Red (system) → white.
    /// Background texture → black.
    /// </summary>
    private static Mat ExtractTextMask(Mat bgr)
    {
        var mask = new Mat(bgr.Height, bgr.Width, MatType.CV_8UC1, Scalar.Black);

        for (int y = 0; y < bgr.Height; y++)
        {
            for (int x = 0; x < bgr.Width; x++)
            {
                var pixel = bgr.At<Vec3b>(y, x);
                int b = pixel.Item0, g = pixel.Item1, r = pixel.Item2;

                // Yellow: public chat (name + message)
                if (r > 160 && g > 130 && b < 100)
                {
                    mask.Set(y, x, (byte)255);
                    continue;
                }

                // Teal: private messages (name + message)
                if (r < 120 && g > 140 && b > 140)
                {
                    mask.Set(y, x, (byte)255);
                    continue;
                }

                // White: system messages
                if (r > 170 && g > 170 && b > 170)
                {
                    mask.Set(y, x, (byte)255);
                    continue;
                }

                // Red: system messages (errors, warnings)
                if (r > 160 && g < 80 && b < 80)
                {
                    mask.Set(y, x, (byte)255);
                }
            }
        }

        return mask;
    }

    private static ChatMessage? ParseLine(string text, ChatMessageType type, int screenX, int screenY)
    {
        var match = MessagePattern.Match(text);
        if (match.Success)
        {
            var name = match.Groups[1].Value;
            if (name.Length < 2)
                return null;

            return new ChatMessage
            {
                RawText = text,
                SenderName = name,
                Content = match.Groups[2].Value.Trim(),
                Type = type,
                ScreenX = screenX,
                ScreenY = screenY
            };
        }

        if (text.Length < 3)
            return null;

        return new ChatMessage
        {
            RawText = text,
            SenderName = "",
            Content = text,
            Type = ChatMessageType.System,
            ScreenX = screenX,
            ScreenY = screenY
        };
    }

    private static bool IsWatermark(string line)
    {
        foreach (var pattern in WatermarkPatterns)
        {
            if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static byte[] ComputeRegionHash(Mat region)
    {
        using var small = new Mat();
        Cv2.Resize(region, small, new OpenCvSharp.Size(64, 64), interpolation: InterpolationFlags.Area);
        using var gray = new Mat();
        Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);

        var hash = new byte[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                hash[y * 64 + x] = (byte)(gray.At<byte>(y, x) >> 4);
        return hash;
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
