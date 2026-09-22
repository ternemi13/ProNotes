using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CuadernoDigital.App.Services;

public static class ImageClipboardService
{
    private const string PngClipboardFormat = "PNG";
    private const string DeviceIndependentBitmapFormat = "DeviceIndependentBitmap";

    public static byte[]? TryGetPngBytesFromClipboard()
    {
        IDataObject? dataObject;
        try
        {
            dataObject = Clipboard.GetDataObject();
        }
        catch
        {
            return null;
        }

        if (dataObject is null)
        {
            return null;
        }

        return TryReadPngFormat(dataObject)
            ?? TryReadBitmapSource(dataObject)
            ?? TryReadDibFormat(dataObject)
            ?? TryReadFileDrop(dataObject);
    }

    private static byte[]? TryReadPngFormat(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(PngClipboardFormat))
        {
            return null;
        }

        var bytes = ReadClipboardBytes(dataObject.GetData(PngClipboardFormat));
        return bytes is null ? null : TryNormalizeImageBytes(bytes);
    }

    private static byte[]? TryReadBitmapSource(IDataObject dataObject)
    {
        try
        {
            if (!dataObject.GetDataPresent(DataFormats.Bitmap) && !Clipboard.ContainsImage())
            {
                return null;
            }

            var bitmap = Clipboard.GetImage();
            return bitmap is null ? null : EncodeBitmapToPng(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryReadDibFormat(IDataObject dataObject)
    {
        var dib = dataObject.GetDataPresent(DataFormats.Dib)
            ? ReadClipboardBytes(dataObject.GetData(DataFormats.Dib))
            : dataObject.GetDataPresent(DeviceIndependentBitmapFormat)
                ? ReadClipboardBytes(dataObject.GetData(DeviceIndependentBitmapFormat))
                : null;

        if (dib is null || dib.Length < 40)
        {
            return null;
        }

        try
        {
            return TryNormalizeImageBytes(CreateBitmapBytesFromDib(dib));
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryReadFileDrop(IDataObject dataObject)
    {
        try
        {
            if (!dataObject.GetDataPresent(DataFormats.FileDrop)
                || dataObject.GetData(DataFormats.FileDrop) is not string[] files)
            {
                return null;
            }

            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                var normalized = TryNormalizeImageBytes(File.ReadAllBytes(file));
                if (normalized is not null)
                {
                    return normalized;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static byte[]? ReadClipboardBytes(object? data)
    {
        switch (data)
        {
            case null:
                return null;
            case byte[] bytes:
                return bytes;
            case MemoryStream memoryStream:
                return memoryStream.ToArray();
            case Stream stream:
                using (stream)
                using (var copy = new MemoryStream())
                {
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }

                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
            default:
                return null;
        }
    }

    private static byte[] CreateBitmapBytesFromDib(byte[] dib)
    {
        var headerSize = BitConverter.ToInt32(dib, 0);
        var maskSize = CalculateBitfieldMaskSize(dib, headerSize);
        var colorTableSize = CalculateColorTableSize(dib, headerSize);
        var pixelOffset = 14 + headerSize + maskSize + colorTableSize;
        var fileSize = 14 + dib.Length;

        using var output = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(output);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(pixelOffset);
        writer.Write(dib);
        return output.ToArray();
    }

    private static int CalculateBitfieldMaskSize(byte[] dib, int headerSize)
    {
        if (dib.Length < 20 || headerSize != 40)
        {
            return 0;
        }

        var compression = BitConverter.ToInt32(dib, 16);
        return compression switch
        {
            3 => 12,
            6 => 16,
            _ => 0
        };
    }

    private static int CalculateColorTableSize(byte[] dib, int headerSize)
    {
        if (dib.Length < headerSize || headerSize < 16)
        {
            return 0;
        }

        var bitsPerPixel = BitConverter.ToUInt16(dib, 14);
        var colorsUsed = headerSize >= 40 ? BitConverter.ToInt32(dib, 32) : 0;
        if (colorsUsed > 0)
        {
            return colorsUsed * 4;
        }

        return bitsPerPixel <= 8 ? (1 << bitsPerPixel) * 4 : 0;
    }

    private static byte[]? TryNormalizeImageBytes(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return EncodeBitmapToPng(decoder.Frames[0]);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] EncodeBitmapToPng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
